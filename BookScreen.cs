using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARCHIV3D
{
    /// <summary>
    /// The open-book screen used by the bookshelves: a two-page spread with a title page,
    /// page turns (arrows, swipe, or arrow keys), and a book list when a shelf has several books.
    /// Text that doesn't fit on a page continues on the next page(s) automatically.
    /// A year bookshelf (YearBookshelf) lists its semester shelves, then that semester's subjects, then the books.
    /// Reading progress is saved through LibraryProgress.
    /// The UI is built when the game starts; the fonts, art and colors below can be changed in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ARCHIV3D/Book Screen")]
    public class BookScreen : MonoBehaviour
    {
        [Header("Fonts (optional)")]
        public TMP_FontAsset headingFont;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset italicFont;
        public TMP_FontAsset labelFont;
        public TMP_FontAsset uiFont;

        [Header("Art (optional)")]
        public Sprite coverSprite;
        public Sprite leftPageSprite;
        public Sprite rightPageSprite;
        public Sprite spineSprite;
        public Sprite ornamentSprite;
        public Sprite roundButtonSprite;
        public Sprite arrowLeftSprite;
        public Sprite arrowRightSprite;
        public Sprite closeSprite;
        public Sprite roundedSprite;

        [Header("Colors")]
        public Color inkColor = new Color32(52, 34, 20, 255);
        public Color accentColor = new Color32(150, 92, 32, 255);
        public Color uiTextColor = new Color32(255, 246, 224, 255);
        public Color dimColor = new Color(0f, 0f, 0f, 0.78f);

        [Header("Bookmarks")]
        [Tooltip("Color of the bookmark ribbons: on bookmarked pages and on the Bookmarks button of the shelf list.")]
        public Color bookmarkColor = new Color32(196, 52, 52, 255);

        [Header("Sound (optional)")]
        public AudioClip pageTurnSound;

        [Header("Text")]
        [Tooltip("Size of the page text. Text that doesn't fit continues on the next page.")]
        public float bodyFontSize = 28f;
        [Tooltip("Size of each page's heading.")]
        public float headingFontSize = 40f;

        [Header("Layout")]
        public int sortingOrder = 450;
        [Tooltip("Seconds for one page turn.")]
        public float turnSeconds = 0.34f;

        public bool IsOpen { get; private set; }

        static BookScreen s_Instance;

        // ------------------------------------------------------------------ built UI
        class PageView
        {
            public RectTransform root;
            public Image paper;
            public RectTransform content;
            public TMP_Text heading;
            public Image picture;
            public LayoutElement pictureLayout;
            public TMP_Text body;
            public RectTransform titleGroup;
            public TMP_Text label, title, subtitle, description;
            public TMP_Text number;
            public TMP_Text center;
            public int index = -1;            // which page it shows (0 = title page, 1..n = pages)
            public RectTransform ribbonRoot;  // bookmark ribbon at the page's top outer corner (tap area)
            public Image ribbon;
            public TMP_Text ribbonPlus;
        }

        bool _built;
        GameObject _canvasGO;
        RectTransform _bookArea;
        RectTransform _book;
        PageView _left, _right, _turn;
        Button _prev, _next, _close;
        TMP_Text _indicator;
        RectTransform _chooser;
        RectTransform _chooserList;
        TMP_Text _chooserTitle;
        TMP_Text _chooserHint;
        ScrollRect _chooserScroll;
        Button _chooserBack;
        RectTransform _bookmarksTab;   // the "Bookmarks" ribbon hanging from the list's top-right corner
        TMP_Text _bookmarksCount;
        AudioSource _audio;
        static Sprite s_RibbonFill, s_RibbonOutline, s_TabFill;

        // ------------------------------------------------------------------ state
        string _shelfName = "Bookshelf";
        readonly List<SemesterShelf> _semesters = new List<SemesterShelf>();   // the bookshelf's shelves
        readonly List<LearningMaterial> _looseBooks = new List<LearningMaterial>(); // books not in a subject
        SubjectData _subject;          // subject of the open book (null for loose books)
        LearningMaterial _bookData;    // the open book
        readonly Stack<Action> _history = new Stack<Action>(); // screens to go Back to
        Action _currentList;           // the list on screen now (where Bookmarks comes back to)

        // Page indicator at the bottom, and a short message that replaces it for a moment ("Page 5 bookmarked").
        string _pagesLabel = "";
        string _toast = "";
        float _toastUntil;

        /// <summary>One printed page. A long BookPage becomes several sheets.</summary>
        class Sheet
        {
            public BookPage page;
            public bool first;     // first sheet of its BookPage: shows the heading and picture
            public string text;
            public int pageIndex;  // which BookPage of the book (0 = first)
            public int part;       // which sheet of that BookPage (0 = first)
        }
        readonly List<Sheet> _sheets = new List<Sheet>();
        Vector2 _paginatedFor;     // page size the sheets were measured for
        int _spread;
        bool _turning;
        Action _onClosed;

        public static BookScreen GetOrCreate()
        {
            if (s_Instance != null) return s_Instance;
            s_Instance = UnityEngine.Object.FindAnyObjectByType<BookScreen>();
            if (s_Instance == null) s_Instance = new GameObject("ARCHIV3D Book Screen").AddComponent<BookScreen>();
            return s_Instance;
        }

        void Awake()
        {
            if (s_Instance == null) s_Instance = this;
            Build();
            _canvasGO.SetActive(false);
        }

        void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (_toastUntil > 0f && Time.unscaledTime >= _toastUntil)
            {
                _toastUntil = 0f;
                RefreshIndicator();
            }
            if (KeyPressed(KeyCode.Escape)) { Back(); return; }
            if (_bookData == null || _turning) return;

            // Screen size changed (rotation, resize): measure the pages again.
            if ((_left.content.rect.size - _paginatedFor).sqrMagnitude > 4f)
            {
                Paginate();
                _spread = Mathf.Clamp(_spread, 0, SpreadCount - 1);
                ShowVirtual(_left, 2 * _spread);
                ShowVirtual(_right, 2 * _spread + 1);
                UpdateNav();
            }

            if (KeyPressed(KeyCode.RightArrow)) NextSpread();
            else if (KeyPressed(KeyCode.LeftArrow)) PreviousSpread();
        }

        static bool KeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (key == KeyCode.Escape && kb.escapeKey.wasPressedThisFrame) return true;
                if (key == KeyCode.RightArrow && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) return true;
                if (key == KeyCode.LeftArrow && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(key)) return true;
#endif
            return false;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Opens the book screen for a shelf. With several books, a book list is shown first.</summary>
        public void Open(string shelfName, IList<ShelfBook> books, Action onClosed = null)
        {
            var materials = new List<LearningMaterial>();
            if (books != null)
                foreach (var b in books)
                    if (b != null) materials.Add(b.ToMaterial());
            OpenShelf(shelfName, (IList<SemesterShelf>)null, materials, onClosed);
        }

        /// <summary>Opens a set of subjects as one shelf.</summary>
        public void OpenShelf(string title, IList<SubjectData> subjects, IList<LearningMaterial> looseBooks, Action onClosed = null)
        {
            var shelf = new SemesterShelf { label = title, subjects = subjects != null ? new List<SubjectData>(subjects) : new List<SubjectData>() };
            OpenShelf(title, new List<SemesterShelf> { shelf }, looseBooks, onClosed);
        }

        /// <summary>
        /// Opens a year bookshelf: its semester shelves, then the chosen semester's subjects, then that subject's books.
        /// Steps with only one choice are skipped. Loose books (old shelf book components) show up as "Other books".
        /// </summary>
        public void OpenShelf(string title, IList<SemesterShelf> semesters, IList<LearningMaterial> looseBooks, Action onClosed = null)
        {
            Build();
            _shelfName = string.IsNullOrWhiteSpace(title) ? "Bookshelf" : title;
            _semesters.Clear();
            if (semesters != null)
                foreach (var shelf in semesters)
                {
                    if (shelf == null) continue;
                    var subjects = shelf.ValidSubjects;
                    if (subjects.Count == 0) continue;
                    foreach (var subject in subjects) subject.EnsureIds();
                    _semesters.Add(shelf);
                }
            _looseBooks.Clear();
            if (looseBooks != null)
                foreach (var m in looseBooks)
                    if (m != null) _looseBooks.Add(m);
            _onClosed = onClosed;
            _history.Clear();
            _subject = null;

            EnsureEventSystem();
            _canvasGO.SetActive(true);
            IsOpen = true;
            _turning = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (_semesters.Count == 0) EnterBooks(null, _looseBooks);
            else if (_semesters.Count == 1 && _looseBooks.Count == 0) EnterSemester(_semesters[0]);
            else ShowSemesterList();
        }

        public void Close()
        {
            if (!IsOpen) return;
            StopAllCoroutines();
            _turning = false;
            IsOpen = false;
            _canvasGO.SetActive(false);
            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }

        /// <summary>Android back / Esc: book -> book list -> subject list -> semester list -> close.</summary>
        public void Back()
        {
            if (_turning)
            {
                StopAllCoroutines();
                _turning = false;
                _turn.root.gameObject.SetActive(false);
            }
            if (_history.Count > 0) _history.Pop()();
            else Close();
        }

        public void NextSpread()
        {
            if (_turning || _bookData == null || _spread >= SpreadCount - 1) return;
            StartCoroutine(Turn(+1));
        }

        public void PreviousSpread()
        {
            if (_turning || _bookData == null || _spread <= 0) return;
            StartCoroutine(Turn(-1));
        }

        // ------------------------------------------------------------------ screens

        /// <summary>The year bookshelf's shelves, one button per semester.</summary>
        void ShowSemesterList()
        {
            BeginList(_shelfName, "Choose a semester");
            _currentList = ShowSemesterList;
            foreach (var semester in _semesters)
            {
                var shelf = semester;
                var subjects = shelf.ValidSubjects;
                int finished = 0, total = 0;
                foreach (var s in subjects)
                {
                    LibraryProgress.SubjectSummary(s, out int f, out _, out int t);
                    finished += f;
                    total += t;
                }
                string info = (subjects.Count == 1 ? "1 subject" : subjects.Count + " subjects") +
                              (total > 0 ? "  ·  " + finished + " of " + total + " books finished" : "");
                ChooserButton(string.IsNullOrWhiteSpace(shelf.label) ? SemesterShelf.LabelFor(shelf.semester) : shelf.label,
                    info, SemesterColor(shelf.semester), () =>
                    {
                        _history.Push(ShowSemesterList);
                        EnterSemester(shelf);
                    });
            }
            if (_looseBooks.Count > 0)
                ChooserButton("Other books", _looseBooks.Count == 1 ? "1 book" : _looseBooks.Count + " books",
                    new Color32(70, 44, 26, 255), () =>
                    {
                        _history.Push(ShowSemesterList);
                        EnterBooks(null, _looseBooks);
                    });
            EndList();
        }

        static Color SemesterColor(int semester) =>
            semester == 2 ? new Color32(58, 52, 106, 255) : semester == 3 ? new Color32(90, 66, 28, 255) : new Color32(36, 72, 86, 255);

        /// <summary>Opens a semester shelf: straight into its subject if it has one, otherwise its subject list.</summary>
        void EnterSemester(SemesterShelf shelf)
        {
            var subjects = shelf.ValidSubjects;
            if (subjects.Count == 1) EnterBooks(subjects[0], subjects[0].materials);
            else ShowSubjectList(shelf);
        }

        /// <summary>A semester shelf's subjects, one button per subject.</summary>
        void ShowSubjectList(SemesterShelf shelf)
        {
            string label = string.IsNullOrWhiteSpace(shelf.label) ? SemesterShelf.LabelFor(shelf.semester) : shelf.label;
            bool single = _semesters.Count <= 1;
            BeginList(single ? _shelfName : label, (single ? "" : _shelfName + "  ·  ") + "Choose a subject");
            _currentList = () => ShowSubjectList(shelf);
            foreach (var subject in shelf.ValidSubjects)
            {
                var s = subject;
                LibraryProgress.SubjectSummary(s, out int finished, out _, out int total);
                var info = new List<string>();
                if (!string.IsNullOrWhiteSpace(s.subjectCode)) info.Add(s.subjectCode.Trim());
                info.Add(total == 0 ? "no books yet" : total == 1 ? "1 book" : total + " books");
                if (total > 0) info.Add(finished + " finished");
                ChooserButton(s.DisplayName, string.Join("  ·  ", info), s.color, () =>
                {
                    _history.Push(() => ShowSubjectList(shelf));
                    EnterBooks(s, s.materials);
                });
            }
            if (single && _looseBooks.Count > 0)
                ChooserButton("Other books", _looseBooks.Count == 1 ? "1 book" : _looseBooks.Count + " books",
                    new Color32(70, 44, 26, 255), () =>
                    {
                        _history.Push(() => ShowSubjectList(shelf));
                        EnterBooks(null, _looseBooks);
                    });
            EndList();
        }

        /// <summary>Opens a subject: straight into its book if it has one, otherwise its book list.</summary>
        void EnterBooks(SubjectData subject, IList<LearningMaterial> books)
        {
            var list = new List<LearningMaterial>();
            if (books != null)
                foreach (var m in books)
                    if (m != null) list.Add(m);
            if (list.Count == 1) OpenBook(subject, list[0]);
            else ShowBookList(subject, list);
        }

        void ShowBookList(SubjectData subject, List<LearningMaterial> books)
        {
            _subject = subject;
            string title = subject != null ? subject.DisplayName : _shelfName;
            BeginList(title, books.Count == 0 ? "" : "Choose a book");
            _currentList = () => ShowBookList(subject, books);
            if (books.Count == 0)
            {
                var empty = NewText("Empty", _chooserList, uiFont, 28, uiTextColor, TextAlignmentOptions.Center);
                empty.text = "No books on this shelf yet.\n<size=80%><alpha=#AA>In Unity, select the subject in " +
                             "Assets/ARCHIV3D/Subjects and press + under Materials.</size>";
            }
            foreach (var book in books)
            {
                var b = book;
                var info = new List<string>();
                if (!string.IsNullOrWhiteSpace(b.subtitle)) info.Add(b.subtitle.Trim());
                info.Add(ProgressLabel(b));
                ChooserButton(b.DisplayTitle, string.Join("  ·  ", info), b.coverColor, () =>
                {
                    _history.Push(() => ShowBookList(subject, books));
                    OpenBook(subject, b);
                });
            }
            EndList();
        }

        static string ProgressLabel(LearningMaterial m)
        {
            var p = LibraryProgress.GetProgress(m.materialId);
            if (p == null) return "New";
            if (p.completed) return "Finished";
            if (p.totalPages > 0) return "Page " + Mathf.Max(1, p.pagesRead) + " of " + p.totalPages;
            return "Started";
        }

        void BeginList(string title, string hint)
        {
            _bookData = null;
            _bookArea.gameObject.SetActive(false);
            _prev.gameObject.SetActive(false);
            _next.gameObject.SetActive(false);
            _indicator.text = "";
            _chooser.gameObject.SetActive(true);
            _chooserTitle.text = title;
            _chooserHint.text = hint;
            _chooserHint.gameObject.SetActive(!string.IsNullOrEmpty(hint));
            _chooserBack.gameObject.SetActive(_history.Count > 0);
            RefreshBookmarksTab(true);

            for (int i = _chooserList.childCount - 1; i >= 0; i--)
            {
                var child = _chooserList.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        void EndList()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_chooserList);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_chooser);
            _chooserScroll.verticalNormalizedPosition = 1f;
            _chooserScroll.velocity = Vector2.zero;
        }

        void OpenBook(SubjectData subject, LearningMaterial book, BookmarkRecord goTo = null)
        {
            _subject = subject;
            ShowBook(book, goTo);
            if (book != null) LibraryProgress.MaterialOpened(subject, book);
        }

        /// <summary>Shows a book from its title page, or from a bookmarked page.</summary>
        void ShowBook(LearningMaterial book, BookmarkRecord goTo = null)
        {
            _chooser.gameObject.SetActive(false);
            _bookArea.gameObject.SetActive(true);
            _bookData = book;
            _spread = 0;
            _toastUntil = 0f;
            _turn.root.gameObject.SetActive(false);

            Canvas.ForceUpdateCanvases();
            Paginate();
            int target = goTo != null ? PageOf(goTo) : -1;
            if (target > 0) _spread = Mathf.Clamp(target / 2, 0, SpreadCount - 1);
            ShowVirtual(_left, 2 * _spread);
            ShowVirtual(_right, 2 * _spread + 1);
            UpdateNav();
            if (target > 0) Toast("Bookmark: page " + target);
        }

        /// <summary>
        /// The printed page (1..n) a bookmark points to. Pages are measured for the screen, so a bookmark remembers
        /// which entry of the book's Pages list it was on and which part of it, not just the page number.
        /// </summary>
        int PageOf(BookmarkRecord b)
        {
            int best = -1;
            for (int i = 0; i < _sheets.Count; i++)
            {
                var s = _sheets[i];
                if (s.pageIndex != b.pageIndex || s.part > b.part) continue;
                best = i;
                if (s.part == b.part) break;
            }
            if (best < 0 && b.pageNumber > 0 && _sheets.Count > 0) best = Mathf.Clamp(b.pageNumber - 1, 0, _sheets.Count - 1);
            return best < 0 ? -1 : best + 1;
        }

        // ------------------------------------------------------------------ bookmarks

        /// <summary>The Bookmarks button: opens the list of bookmarked pages; Back returns to the list before it.</summary>
        void OpenBookmarks()
        {
            if (_currentList != null) _history.Push(_currentList);
            ShowBookmarks();
        }

        void ShowBookmarks() => ShowBookmarks(false);

        /// <summary>Every bookmarked page, grouped by book. Tap one to open the book there; X removes it.</summary>
        void ShowBookmarks(bool keepScroll)
        {
            float scroll = _chooserScroll.verticalNormalizedPosition;
            var all = new List<BookmarkRecord>();
            foreach (var b in LibraryProgress.Bookmarks)
                if (b != null) all.Add(b);

            BeginList("Bookmarks", all.Count == 0 ? "" :
                (all.Count == 1 ? "1 bookmarked page" : all.Count + " bookmarked pages") + "  ·  Tap one to open it");
            _currentList = ShowBookmarks;
            RefreshBookmarksTab(false);

            if (all.Count == 0)
            {
                var empty = NewText("Empty", _chooserList, uiFont, 28, uiTextColor, TextAlignmentOptions.Center);
                empty.textWrappingMode = TextWrappingModes.Normal;
                empty.text = "No bookmarks yet.\n<size=80%><alpha=#AA>Open a book and tap the ribbon at the top corner " +
                             "of a page to bookmark it. Your bookmarked pages show up here.</size>";
            }

            // Group by book: year, subject, book title; inside a book, in page order.
            var books = FindAllBooks();
            all.Sort((a, b) =>
            {
                int c = a.yearLevel.CompareTo(b.yearLevel);
                if (c == 0) c = string.Compare(a.subjectName, b.subjectName, StringComparison.OrdinalIgnoreCase);
                if (c == 0) c = string.Compare(a.materialTitle, b.materialTitle, StringComparison.OrdinalIgnoreCase);
                if (c == 0) c = string.CompareOrdinal(a.materialId, b.materialId);
                if (c == 0) c = a.pageIndex.CompareTo(b.pageIndex);
                if (c == 0) c = a.part.CompareTo(b.part);
                return c;
            });

            string lastBook = null;
            foreach (var b in all)
            {
                books.TryGetValue(b.materialId ?? "", out var found);
                if (b.materialId != lastBook)
                {
                    lastBook = b.materialId;
                    var parts = new List<string>();
                    if (b.yearLevel > 0) parts.Add(YearBookshelf.Ordinal(b.yearLevel) + " Year");
                    string subject = found.subject != null ? found.subject.DisplayName : b.subjectName;
                    if (!string.IsNullOrWhiteSpace(subject)) parts.Add(subject);
                    parts.Add(found.material != null ? found.material.DisplayTitle : b.materialTitle);
                    SectionLabel(string.Join("  ·  ", parts));
                }
                BookmarkRow(b, found.subject, found.material);
            }

            EndList();
            if (keepScroll) _chooserScroll.verticalNormalizedPosition = scroll;
        }

        /// <summary>Every book in the scene by its ID (all year bookshelves and shelf books), so any bookmark can open.</summary>
        Dictionary<string, (SubjectData subject, LearningMaterial material)> FindAllBooks()
        {
            var books = new Dictionary<string, (SubjectData, LearningMaterial)>();
            void AddSubject(SubjectData s)
            {
                if (s == null || s.materials == null) return;
                s.EnsureIds();
                foreach (var m in s.materials)
                    if (m != null && !string.IsNullOrEmpty(m.materialId) && !books.ContainsKey(m.materialId))
                        books[m.materialId] = (s, m);
            }
            foreach (var shelf in _semesters)
                foreach (var s in shelf.ValidSubjects) AddSubject(s);
            foreach (var year in UnityEngine.Object.FindObjectsByType<YearBookshelf>(FindObjectsInactive.Exclude))
                foreach (var s in year.Subjects) AddSubject(s);
            foreach (var m in _looseBooks)
                if (m != null && !string.IsNullOrEmpty(m.materialId) && !books.ContainsKey(m.materialId))
                    books[m.materialId] = (null, m);
            foreach (var sb in UnityEngine.Object.FindObjectsByType<ShelfBook>(FindObjectsInactive.Exclude))
            {
                var m = sb.ToMaterial();
                if (!books.ContainsKey(m.materialId)) books[m.materialId] = (null, m);
            }
            return books;
        }

        /// <summary>Small gold heading above a book's bookmarks.</summary>
        void SectionLabel(string text)
        {
            var t = NewText("Book", _chooserList, uiFont, 22, new Color32(240, 190, 84, 255), TextAlignmentOptions.BottomLeft);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.characterSpacing = 3;
            t.text = text.ToUpperInvariant();
            t.margin = new Vector4(8, 14, 8, 0);
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 48;
        }

        /// <summary>One bookmarked page: ribbon, topic and page, the first words; tap to open, X to remove.</summary>
        void BookmarkRow(BookmarkRecord b, SubjectData subject, LearningMaterial material)
        {
            bool available = material != null;
            Color color = available ? material.coverColor : new Color32(60, 50, 44, 255);
            var img = NewImage("Bookmark", _chooserList, roundedSprite, color, true);
            img.type = roundedSprite != null && roundedSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 112;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.interactable = available;
            if (available)
                btn.onClick.AddListener(() =>
                {
                    _history.Push(ShowBookmarks);
                    OpenBook(subject, material, b);
                });

            var ribbon = NewImage("Ribbon", img.transform, s_RibbonFill, bookmarkColor, false);
            Place(ribbon.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(42, 0), new Vector2(30, 62));

            string topic = string.IsNullOrWhiteSpace(b.heading) ? "Page " + b.pageNumber : b.heading.Trim();
            string detail = available
                ? "Page " + b.pageNumber + (string.IsNullOrWhiteSpace(b.excerpt) ? "" : "  ·  \"" + b.excerpt + "\"")
                : "This book is no longer on the shelves";
            var t = NewText("Label", img.transform, headingFont, 32, uiTextColor, TextAlignmentOptions.Left);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.text = topic + "\n<size=60%><alpha=#BB><noparse>" + detail.Replace("</noparse>", "") + "</noparse></size>";
            Stretch(t.rectTransform, 82, 8, 116, 8);
            if (!available) t.alpha = 0.6f;

            // Remove (X) on the right
            var removeImg = NewImage("Remove", img.transform, roundedSprite, new Color(0f, 0f, 0f, 0.32f), true);
            removeImg.type = roundedSprite != null && roundedSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            Place(removeImg.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18, 0), new Vector2(80, 80));
            var remove = removeImg.gameObject.AddComponent<Button>();
            remove.targetGraphic = removeImg;
            remove.navigation = new Navigation { mode = Navigation.Mode.None };
            remove.onClick.AddListener(() =>
            {
                LibraryProgress.RemoveBookmark(b);
                ShowBookmarks(true);
            });
            var x = NewText("X", removeImg.transform, uiFont, 32, uiTextColor, TextAlignmentOptions.Center);
            x.text = "X";
            Stretch(x.rectTransform, 0, 0, 0, 0);
            removeImg.gameObject.AddComponent<UIButtonJuice>().pressedScale = 0.9f;

            img.gameObject.AddComponent<UIButtonJuice>().pressedScale = 0.97f;
        }

        /// <summary>Shows/hides the Bookmarks ribbon on the list and updates its count.</summary>
        void RefreshBookmarksTab(bool show)
        {
            if (_bookmarksTab == null) return;
            _bookmarksTab.gameObject.SetActive(show);
            if (_bookmarksCount != null) _bookmarksCount.text = LibraryProgress.Bookmarks.Count.ToString();
        }

        /// <summary>The ribbon on a page: tap to bookmark that page, tap again to remove the bookmark.</summary>
        void ToggleBookmark(PageView pv)
        {
            if (_turning || _bookData == null || pv.index < 1 || pv.index > PageCount) return;
            var sheet = _sheets[pv.index - 1];
            var existing = LibraryProgress.FindBookmark(_bookData.materialId, sheet.pageIndex, sheet.part);
            if (existing != null)
            {
                LibraryProgress.RemoveBookmark(existing);
                Toast("Bookmark removed from page " + pv.index);
            }
            else
            {
                string heading = sheet.page != null ? sheet.page.heading : "";
                LibraryProgress.AddBookmark(_subject, _bookData, sheet.pageIndex, sheet.part, pv.index, heading, Excerpt(sheet.text));
                Toast("Page " + pv.index + " bookmarked  ·  find it under Bookmarks on the shelf");
            }
            RefreshRibbon(_left);
            RefreshRibbon(_right);
        }

        void RefreshRibbon(PageView pv)
        {
            if (pv.ribbonRoot == null) return;
            bool show = _bookData != null && !string.IsNullOrEmpty(_bookData.materialId) && pv.index >= 1 && pv.index <= PageCount;
            pv.ribbonRoot.gameObject.SetActive(show);
            if (!show) return;
            var sheet = _sheets[pv.index - 1];
            bool on = LibraryProgress.IsBookmarked(_bookData.materialId, sheet.pageIndex, sheet.part);
            pv.ribbon.sprite = on ? s_RibbonFill : s_RibbonOutline;
            pv.ribbon.color = on ? bookmarkColor : new Color(inkColor.r, inkColor.g, inkColor.b, 0.45f);
            pv.ribbonPlus.gameObject.SetActive(!on);
        }

        /// <summary>The first words of a page, without formatting tags.</summary>
        static string Excerpt(string text, int max = 70)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string s = Regex.Replace(AnyTag.Replace(text, " "), @"\s+", " ").Trim();
            if (s.Length <= max) return s;
            int cut = s.LastIndexOf(' ', max);
            if (cut < max / 2) cut = max;
            return s.Substring(0, cut).TrimEnd(',', ';', ':', '.', ' ') + "...";
        }

        void Toast(string message)
        {
            _toast = message;
            _toastUntil = Time.unscaledTime + 2.2f;
            RefreshIndicator();
        }

        /// <summary>"Pages 3–4 of 10", a short message after a bookmark change, or a tip while nothing is bookmarked.</summary>
        void RefreshIndicator()
        {
            if (_bookData == null) { _indicator.text = ""; return; }
            if (_toastUntil > 0f) { _indicator.text = _toast; return; }
            bool tip = PageCount > 0 && LibraryProgress.Bookmarks.Count == 0;
            _indicator.text = _pagesLabel + (tip ? "   <size=80%><alpha=#99>·  Tap the ribbon on a page to bookmark it</size>" : "");
        }

        /// <summary>Printed pages (a long BookPage counts as several).</summary>
        int PageCount => _bookData != null ? _sheets.Count : 0;
        // Title page + pages + "The End", two per spread, rounded up. (Rounding down lost the last spread when the
        // page count was odd, so "The End" never showed and those books could never be marked finished.)
        int SpreadCount => Mathf.Max(1, (PageCount + 3) / 2);

        void UpdateNav()
        {
            bool hasBook = _bookData != null;
            _prev.gameObject.SetActive(hasBook);
            _next.gameObject.SetActive(hasBook);
            _prev.interactable = hasBook && _spread > 0;
            _next.interactable = hasBook && _spread < SpreadCount - 1;
            SetAlpha(_prev, _prev.interactable ? 1f : 0.3f);
            SetAlpha(_next, _next.interactable ? 1f : 0.3f);

            int n = PageCount;
            if (!hasBook) { _pagesLabel = ""; RefreshIndicator(); return; }
            if (n == 0) { _pagesLabel = "No pages yet"; RefreshIndicator(); return; }
            int a = Mathf.Max(1, 2 * _spread);
            int b = Mathf.Min(n, 2 * _spread + 1);
            _pagesLabel = a > n ? "The End" : a >= b ? "Page " + a + " of " + n : "Pages " + a + "–" + b + " of " + n;
            RefreshIndicator();

            // Progress: furthest page shown, and whether "The End" is on screen.
            LibraryProgress.PageReached(_subject, _bookData, Mathf.Min(n, 2 * _spread + 1), n, 2 * _spread + 1 >= n + 1);
        }

        static void SetAlpha(Button button, float a)
        {
            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = a;
        }

        /// <summary>0 = title page, 1..n = the book's pages, n+1 = "The End".</summary>
        void ShowVirtual(PageView pv, int index)
        {
            int n = PageCount;
            pv.index = index;
            RefreshRibbon(pv);
            pv.content.gameObject.SetActive(false);
            pv.titleGroup.gameObject.SetActive(false);
            pv.center.gameObject.SetActive(false);
            pv.number.text = "";

            if (_bookData == null)
            {
                if (index == 0) ShowTitle(pv, _shelfName, "", "");
                else ShowCenter(pv, "This bookshelf has no book yet.\n\n<size=75%>In Unity, add subjects and books to the shelf.</size>");
                return;
            }

            if (index == 0)
            {
                ShowTitle(pv, _bookData.DisplayTitle, _bookData.subtitle, _bookData.description);
            }
            else if (index <= n)
            {
                var sheet = _sheets[index - 1];
                LayoutSheet(pv, sheet.page, sheet.first, sheet.text);
                pv.number.text = index.ToString();
            }
            else if (index == n + 1 && n > 0)
            {
                ShowCenter(pv, "~ The End ~");
            }
            else if (n == 0 && index == 1)
            {
                ShowCenter(pv, "This book has no pages yet.\n\n<size=75%>In Unity, open the book and press + under Pages.</size>");
            }
        }

        void ShowTitle(PageView pv, string title, string subtitle, string description)
        {
            pv.titleGroup.gameObject.SetActive(true);
            pv.label.text = (_subject != null ? _subject.DisplayName : _shelfName).ToUpperInvariant();
            pv.title.text = title;
            pv.subtitle.gameObject.SetActive(!string.IsNullOrWhiteSpace(subtitle));
            pv.subtitle.text = subtitle;
            pv.description.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
            pv.description.text = description;
            LayoutRebuilder.ForceRebuildLayoutImmediate(pv.titleGroup);
        }

        void ShowCenter(PageView pv, string text)
        {
            pv.center.gameObject.SetActive(true);
            pv.center.text = text;
        }

        /// <summary>Puts a sheet's heading, picture and text on a page and lays it out.</summary>
        void LayoutSheet(PageView pv, BookPage page, bool first, string text)
        {
            pv.content.gameObject.SetActive(true);
            bool hasHeading = first && page != null && !string.IsNullOrWhiteSpace(page.heading);
            pv.heading.gameObject.SetActive(hasHeading);
            if (hasHeading) pv.heading.text = page.heading;

            bool hasPicture = first && page != null && page.picture != null;
            pv.picture.gameObject.SetActive(hasPicture);
            if (hasPicture)
            {
                pv.picture.sprite = page.picture;
                float h = pv.content.rect.height;
                pv.pictureLayout.preferredHeight = Mathf.Max(120f, (h > 1f ? h : 650f) * 0.42f);
            }

            pv.body.text = text ?? "";
            pv.body.pageToDisplay = 1;
            LayoutRebuilder.ForceRebuildLayoutImmediate(pv.content);
        }

        // ------------------------------------------------------------------ pagination

        /// <summary>
        /// Splits every BookPage into as many printed pages as its text needs.
        /// Measured on the hidden turning page, which has the same size and fonts as the real pages.
        /// </summary>
        void Paginate()
        {
            _sheets.Clear();
            _paginatedFor = _left.content.rect.size;
            if (_bookData == null || _bookData.pages == null) return;

            var pv = _turn;
            bool wasActive = pv.root.gameObject.activeSelf;
            Vector3 scale = pv.root.localScale;
            ApplySide(pv, true);
            pv.root.localScale = Vector3.one;
            pv.root.gameObject.SetActive(true);
            pv.titleGroup.gameObject.SetActive(false);
            pv.center.gameObject.SetActive(false);
            try
            {
                for (int pageIndex = 0; pageIndex < _bookData.pages.Count; pageIndex++)
                {
                    var page = _bookData.pages[pageIndex];
                    string remaining = page != null && page.text != null ? page.text : "";
                    bool first = true;
                    int part = 0;
                    for (int guard = 0; guard < 200; guard++)
                    {
                        LayoutSheet(pv, page, first, "");
                        string fit = FitOnPage(pv.body, remaining, out string rest);
                        if (fit.Length == 0 && !first && HasVisibleText(rest))
                        {
                            // Not even one line fits on an empty page (very small screen): don't loop forever.
                            fit = rest;
                            rest = "";
                        }
                        _sheets.Add(new Sheet { page = page, first = first, text = fit, pageIndex = pageIndex, part = part++ });
                        first = false;
                        remaining = rest;
                        if (!HasVisibleText(remaining)) break;
                    }
                }
            }
            finally
            {
                pv.body.text = "";
                pv.root.localScale = scale;
                pv.root.gameObject.SetActive(wasActive);
            }
        }

        /// <summary>
        /// Returns the part of <paramref name="text"/> that fits in the body box; the rest (with any
        /// bold/italic/color tags that were still open carried over) goes to <paramref name="rest"/>.
        /// </summary>
        static string FitOnPage(TMP_Text body, string text, out string rest)
        {
            rest = "";
            if (string.IsNullOrEmpty(text)) return "";
            if (body.rectTransform.rect.height < body.fontSize * 1.2f)
            {
                rest = text;            // no room at all (heading and picture filled the page)
                return "";
            }

            body.text = text;
            body.ForceMeshUpdate(true, true);
            var info = body.textInfo;
            if (info.pageCount <= 1 || info.characterCount == 0) return text;

            int last = Mathf.Clamp(info.pageInfo[0].lastCharacterIndex, 0, info.characterCount - 1);
            var ci = info.characterInfo[last];
            int cut = Mathf.Clamp(ci.index + Mathf.Max(1, ci.stringLength), 0, text.Length);

            // Closing tags and spaces right after the cut stay with this page,
            // so the next page doesn't start with a blank line.
            while (cut < text.Length)
            {
                char c = text[cut];
                if (char.IsWhiteSpace(c)) { cut++; continue; }
                if (c == '<' && cut + 1 < text.Length && text[cut + 1] == '/')
                {
                    int close = text.IndexOf('>', cut);
                    if (close > cut && IsTag(text.Substring(cut, close - cut + 1))) { cut = close + 1; continue; }
                }
                break;
            }

            string fit = text.Substring(0, cut).TrimEnd();
            rest = text.Substring(cut);
            if (HasVisibleText(rest)) rest = OpenTagsAtEnd(fit) + rest;
            else rest = "";
            return fit;
        }

        static readonly HashSet<string> Tags = new HashSet<string>
        {
            "b", "i", "u", "s", "color", "size", "font", "mark", "alpha", "sup", "sub", "smallcaps", "uppercase",
            "lowercase", "allcaps", "cspace", "mspace", "indent", "line-height", "line-indent", "margin", "align",
            "voffset", "width", "rotate", "link", "style", "nobr", "font-weight", "gradient", "material",
            "br", "sprite", "space", "page", "pos", "noparse",
        };
        static readonly HashSet<string> VoidTags = new HashSet<string> { "br", "sprite", "space", "page", "pos" };

        static string TagName(string tag, out bool closing)
        {
            closing = tag.Length > 2 && tag[1] == '/';
            int start = closing ? 2 : 1;
            int end = start;
            while (end < tag.Length - 1 && tag[end] != '=' && tag[end] != ' ' && tag[end] != '>') end++;
            string name = tag.Substring(start, end - start).ToLowerInvariant();
            if (name.StartsWith("#")) name = "color";
            return name;
        }

        static bool IsTag(string tag) => tag.Length > 2 && Tags.Contains(TagName(tag, out _));

        /// <summary>The tags still open at the end of a piece of text, ready to re-open on the next page.</summary>
        static string OpenTagsAtEnd(string text)
        {
            var open = new List<(string name, string tag)>();
            bool noparse = false;
            int i = 0;
            while (i < text.Length)
            {
                int lt = text.IndexOf('<', i);
                if (lt < 0) break;
                int gt = text.IndexOf('>', lt + 1);
                if (gt < 0) break;
                string tag = text.Substring(lt, gt - lt + 1);
                i = gt + 1;
                string name = TagName(tag, out bool closing);
                if (!Tags.Contains(name)) { i = lt + 1; continue; }
                if (name == "noparse") { noparse = !closing; continue; }
                if (noparse || VoidTags.Contains(name)) continue;
                if (!closing) open.Add((name, tag));
                else
                    for (int k = open.Count - 1; k >= 0; k--)
                        if (open[k].name == name) { open.RemoveAt(k); break; }
            }
            var sb = new StringBuilder();
            foreach (var t in open) sb.Append(t.tag);
            if (noparse) sb.Append("<noparse>");
            return sb.ToString();
        }

        static readonly Regex AnyTag = new Regex("<[^<>]*>");

        static bool HasVisibleText(string s) =>
            !string.IsNullOrEmpty(s) && AnyTag.Replace(s, "").Trim().Length > 0;

        // ------------------------------------------------------------------ page turning

        IEnumerator Turn(int dir)
        {
            _turning = true;
            bool forward = dir > 0;
            int from = _spread, to = _spread + dir;

            PageView under = forward ? _right : _left;
            PageView landing = forward ? _left : _right;

            // The turning sheet starts on top of the side being turned, showing the old page...
            ApplySide(_turn, forward);
            _turn.root.gameObject.SetActive(true);
            _turn.root.localScale = Vector3.one;
            ShowVirtual(_turn, forward ? 2 * from + 1 : 2 * from);
            // ...while the page underneath already shows what comes next.
            ShowVirtual(under, forward ? 2 * to + 1 : 2 * to);

            if (pageTurnSound != null && _audio != null) _audio.PlayOneShot(pageTurnSound);

            float half = Mathf.Max(0.05f, turnSeconds * 0.5f);
            yield return ScaleSheet(_turn, 1f, 0f, half, true);

            // Flip the sheet over to the other side, now showing the new page there.
            ApplySide(_turn, !forward);
            ShowVirtual(_turn, forward ? 2 * to : 2 * to + 1);
            yield return ScaleSheet(_turn, 0f, 1f, half, false);

            ShowVirtual(landing, forward ? 2 * to : 2 * to + 1);
            _turn.root.gameObject.SetActive(false);
            _spread = to;
            UpdateNav();
            _turning = false;
        }

        IEnumerator ScaleSheet(PageView pv, float from, float to, float seconds, bool closing)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / seconds);
                float e = closing ? x * x : 1f - (1f - x) * (1f - x);
                float s = Mathf.Lerp(from, to, e);
                pv.root.localScale = new Vector3(Mathf.Max(0.0001f, s), 1f, 1f);
                float shade = Mathf.Lerp(0.72f, 1f, s);
                pv.paper.color = new Color(shade, shade, shade, 1f);
                yield return null;
            }
            pv.root.localScale = new Vector3(Mathf.Max(0.0001f, to), 1f, 1f);
            pv.paper.color = Color.white;
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            if (_built) return;
            _built = true;

            // Bookmark ribbons (drawn in code, white, so they take bookmarkColor / the ink color)
            if (s_RibbonFill == null) s_RibbonFill = MakeRibbon(64, 132, 0.2f, false);
            if (s_RibbonOutline == null) s_RibbonOutline = MakeRibbon(64, 132, 0.2f, true);
            if (s_TabFill == null) s_TabFill = MakeRibbon(132, 184, 0.17f, false);

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            _canvasGO = new GameObject("ARCHIV3D Book Canvas", typeof(RectTransform));
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            _canvasGO.AddComponent<GraphicRaycaster>();

            var dim = NewImage("Dim", _canvasGO.transform, null, dimColor, true);
            Stretch(dim.rectTransform, 0, 0, 0, 0);

            var safe = NewRect("Safe Area", _canvasGO.transform);
            Stretch(safe, 0, 0, 0, 0);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Book
            _bookArea = NewRect("Book Area", safe);
            Stretch(_bookArea, 170, 120, 170, 60);
            var bookImg = NewImage("Book", _bookArea, coverSprite, coverSprite != null ? Color.white : (Color)new Color32(104, 46, 30, 255), true);
            _book = bookImg.rectTransform;
            _book.anchorMin = _book.anchorMax = new Vector2(0.5f, 0.5f);
            var fitter = _book.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1.72f;
            var swipe = _book.gameObject.AddComponent<BookSwipe>();
            swipe.onSwipeLeft = NextSpread;
            swipe.onSwipeRight = PreviousSpread;

            var pages = NewRect("Pages", _book);
            Stretch(pages, 32, 32, 32, 32);
            _left = BuildPage("Left Page", pages, false);
            _right = BuildPage("Right Page", pages, true);

            var spine = NewImage("Spine", pages, spineSprite, spineSprite != null ? Color.white : new Color(0.24f, 0.14f, 0.07f, 0.35f), false);
            spine.preserveAspect = false;
            spine.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            spine.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            spine.rectTransform.sizeDelta = new Vector2(spineSprite != null ? 76 : 6, 0);

            _turn = BuildPage("Turning Page", pages, true);
            _turn.root.gameObject.SetActive(false);

            // Arrows, close, page indicator
            _prev = RoundButton("Previous", safe, arrowLeftSprite, "<", new Vector2(0f, 0.5f), new Vector2(92, 0), 112, PreviousSpread);
            _next = RoundButton("Next", safe, arrowRightSprite, ">", new Vector2(1f, 0.5f), new Vector2(-92, 0), 112, NextSpread);
            _close = RoundButton("Close", safe, closeSprite, "X", new Vector2(1f, 1f), new Vector2(-72, -66), 100, Back);

            _indicator = NewText("Page Indicator", safe, uiFont, 28, uiTextColor, TextAlignmentOptions.Center);
            Place(_indicator.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(900, 50));

            // Subject list / book list
            _chooser = NewRect("Book List", safe);
            Place(_chooser, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 880));
            var chooserBg = _chooser.gameObject.AddComponent<Image>();
            chooserBg.sprite = roundedSprite;
            chooserBg.type = roundedSprite != null && roundedSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            chooserBg.color = new Color32(34, 24, 16, 245);
            var cv = _chooser.gameObject.AddComponent<VerticalLayoutGroup>();
            cv.padding = new RectOffset(50, 50, 44, 50);
            cv.spacing = 26;
            cv.childAlignment = TextAnchor.UpperCenter;
            cv.childControlWidth = true;
            cv.childControlHeight = true;
            cv.childForceExpandWidth = true;
            cv.childForceExpandHeight = false;
            _chooserTitle = NewText("Title", _chooser, labelFont != null ? labelFont : headingFont, 46, new Color32(255, 226, 150, 255), TextAlignmentOptions.Center);
            _chooserTitle.characterSpacing = 4;
            _chooserTitle.margin = new Vector4(120, 0, 120, 0);   // room for the Back button
            _chooserHint = NewText("Hint", _chooser, uiFont, 26, new Color(uiTextColor.r, uiTextColor.g, uiTextColor.b, 0.7f), TextAlignmentOptions.Center);
            _chooserHint.text = "Choose a book";
            _chooserHint.margin = new Vector4(130, 0, 130, 0);   // clear of the Bookmarks ribbon

            // Scrolling list, so a shelf can hold any number of subjects or books.
            var viewport = NewImage("Viewport", _chooser, null, new Color(1f, 1f, 1f, 0f), true);
            var vle = viewport.gameObject.AddComponent<LayoutElement>();
            vle.minHeight = 160;
            vle.flexibleHeight = 1;
            viewport.gameObject.AddComponent<RectMask2D>();
            _chooserList = NewRect("Items", viewport.transform);
            _chooserList.anchorMin = new Vector2(0f, 1f);
            _chooserList.anchorMax = new Vector2(1f, 1f);
            _chooserList.pivot = new Vector2(0.5f, 1f);
            _chooserList.offsetMin = Vector2.zero;
            _chooserList.offsetMax = Vector2.zero;
            var lv = _chooserList.gameObject.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 18;
            lv.childControlWidth = true;
            lv.childControlHeight = true;
            lv.childForceExpandWidth = true;
            lv.childForceExpandHeight = false;
            _chooserList.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _chooserScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _chooserScroll.content = _chooserList;
            _chooserScroll.viewport = viewport.rectTransform;
            _chooserScroll.horizontal = false;
            _chooserScroll.vertical = true;
            _chooserScroll.movementType = ScrollRect.MovementType.Elastic;
            _chooserScroll.scrollSensitivity = 40f;

            // Back (to the subject list), top-left of the panel.
            var backImg = NewImage("Back", _chooser, roundedSprite, new Color32(70, 44, 26, 255), true);
            backImg.type = roundedSprite != null && roundedSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            backImg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(backImg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26, -26), new Vector2(150, 62));
            _chooserBack = backImg.gameObject.AddComponent<Button>();
            _chooserBack.targetGraphic = backImg;
            _chooserBack.navigation = new Navigation { mode = Navigation.Mode.None };
            _chooserBack.onClick.AddListener(Back);
            var backText = NewText("Label", backImg.transform, uiFont, 28, uiTextColor, TextAlignmentOptions.Center);
            backText.text = "Back";
            Stretch(backText.rectTransform, 0, 0, 0, 0);
            backImg.gameObject.AddComponent<UIButtonJuice>().pressedScale = 0.94f;
            _chooserBack.gameObject.SetActive(false);

            BuildBookmarksTab();

            _chooser.gameObject.SetActive(false);
        }

        /// <summary>
        /// "Bookmarks": a ribbon hanging from the top-right corner of the shelf list (semesters, subjects, books).
        /// Shows how many pages are bookmarked; opens the list of bookmarked pages.
        /// </summary>
        void BuildBookmarksTab()
        {
            _bookmarksTab = NewRect("Bookmarks Tab", _chooser);
            _bookmarksTab.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(_bookmarksTab, new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-98, 14), new Vector2(132, 184));

            var shadow = NewImage("Shadow", _bookmarksTab, s_TabFill, new Color(0f, 0f, 0f, 0.35f), false);
            Place(shadow.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(5, -7), new Vector2(132, 184));

            var img = NewImage("Bookmarks Button", _bookmarksTab, s_TabFill, bookmarkColor, true);
            Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(132, 184));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = btn.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(OpenBookmarks);

            _bookmarksCount = NewText("Count", img.transform, labelFont != null ? labelFont : uiFont, 44, uiTextColor, TextAlignmentOptions.Center);
            Place(_bookmarksCount.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(120, 56));
            _bookmarksCount.text = "0";

            var label = NewText("Label", img.transform, uiFont, 23, uiTextColor, TextAlignmentOptions.Center);
            Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -102), new Vector2(124, 34));
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14;
            label.fontSizeMax = 23;
            label.fontStyle = FontStyles.Bold;
            label.text = "Bookmarks";

            var juice = img.gameObject.AddComponent<UIButtonJuice>();
            juice.hoverScale = 1.04f;
            juice.pressedScale = 0.94f;
        }

        /// <summary>
        /// A ribbon bookmark with a V-notch at the bottom, drawn into a white texture (so Image.color tints it).
        /// Filled: slightly shaded with a stitched line near each edge. Outline: just the edge, for an empty bookmark.
        /// </summary>
        static Sprite MakeRibbon(int w, int h, float notch, bool outline)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = outline ? "ARCHIV3D Ribbon Outline" : "ARCHIV3D Ribbon",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            const int ss = 4;                                   // 4x4 samples per pixel: smooth edges
            float notchPx = notch * h;
            float border = Mathf.Max(3f, w * 0.085f);
            float stitchX = w * 0.15f;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int cover = 0, inner = 0;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float fx = x + (sx + 0.5f) / ss, fy = y + (sy + 0.5f) / ss;
                            if (InRibbon(fx, fy, w, h, notchPx, 0f)) cover++;
                            if (outline && InRibbon(fx, fy, w, h, notchPx, border)) inner++;
                        }
                    float a = (cover - inner) / (float)(ss * ss);
                    float shade = 1f;
                    if (!outline)
                    {
                        shade = 0.84f + 0.08f * (y / (float)h);                       // a little lighter at the top
                        bool stitchCol = Mathf.Abs(x + 0.5f - stitchX) < 1f || Mathf.Abs(x + 0.5f - (w - stitchX)) < 1f;
                        bool dash = (y / Mathf.Max(3, h / 24)) % 2 == 0;
                        if (stitchCol && dash && InRibbon(x + 0.5f, y + 0.5f, w, h, notchPx, stitchX * 0.9f)) shade = 1f;
                    }
                    byte v = (byte)Mathf.RoundToInt(shade * 255f);
                    px[y * w + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = tex.name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        /// <summary>Inside the ribbon shape (shrunk by <paramref name="inset"/> pixels): a strip with a V cut into its bottom.</summary>
        static bool InRibbon(float x, float y, int w, int h, float notchPx, float inset)
        {
            if (x < inset || x > w - inset || y > h - inset) return false;
            float u = x / w;
            float bottom = notchPx * (1f - Mathf.Abs(2f * u - 1f));   // 0 at the two tails, deepest in the middle
            return y >= bottom + inset * 1.4f;
        }

        PageView BuildPage(string name, Transform parent, bool right)
        {
            var pv = new PageView();
            pv.paper = NewImage(name, parent, null, Color.white, false);
            pv.root = pv.paper.rectTransform;

            pv.content = NewRect("Content", pv.root);
            var v = pv.content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 22;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            pv.heading = NewText("Heading", pv.content, headingFont, headingFontSize, inkColor, TextAlignmentOptions.TopLeft);
            pv.heading.enableAutoSizing = false;

            pv.picture = NewImage("Picture", pv.content, null, Color.white, false);
            pv.picture.preserveAspect = true;
            pv.pictureLayout = pv.picture.gameObject.AddComponent<LayoutElement>();
            pv.pictureLayout.preferredHeight = 260;

            // Fixed size: text that doesn't fit is moved to the next page instead of shrinking or spilling.
            pv.body = NewText("Text", pv.content, bodyFont, bodyFontSize, inkColor, TextAlignmentOptions.TopLeft);
            pv.body.enableAutoSizing = false;
            pv.body.lineSpacing = 8;
            pv.body.overflowMode = TextOverflowModes.Page;
            pv.body.pageToDisplay = 1;
            var bodyLayout = pv.body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.minHeight = 0;
            bodyLayout.preferredHeight = 0;
            bodyLayout.flexibleHeight = 1;

            pv.titleGroup = NewRect("Title Page", pv.root);
            var tv = pv.titleGroup.gameObject.AddComponent<VerticalLayoutGroup>();
            tv.spacing = 18;
            tv.childAlignment = TextAnchor.MiddleCenter;
            tv.childControlWidth = true;
            tv.childControlHeight = true;
            tv.childForceExpandWidth = true;
            tv.childForceExpandHeight = false;
            pv.label = NewText("Shelf", pv.titleGroup, labelFont, 26, accentColor, TextAlignmentOptions.Center);
            pv.label.characterSpacing = 8;
            pv.title = NewText("Title", pv.titleGroup, headingFont, 60, inkColor, TextAlignmentOptions.Center);
            pv.title.enableAutoSizing = true;
            pv.title.fontSizeMin = 34;
            pv.title.fontSizeMax = 60;
            var ornament = NewImage("Ornament", pv.titleGroup, ornamentSprite, ornamentSprite != null ? Color.white : accentColor, false);
            ornament.preserveAspect = true;
            var ole = ornament.gameObject.AddComponent<LayoutElement>();
            ole.preferredHeight = ornamentSprite != null ? 34 : 3;
            pv.subtitle = NewText("Subtitle", pv.titleGroup, italicFont, 32, accentColor, TextAlignmentOptions.Center);
            pv.description = NewText("Description", pv.titleGroup, bodyFont, 28, inkColor, TextAlignmentOptions.Center);
            pv.description.lineSpacing = 8;

            pv.center = NewText("Message", pv.root, italicFont, 32, accentColor, TextAlignmentOptions.Center);

            pv.number = NewText("Page Number", pv.root, italicFont, 24, accentColor, TextAlignmentOptions.Center);
            Place(pv.number.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 34), new Vector2(200, 40));

            // Bookmark ribbon at the page's top outer corner: an empty outline with "+", red once bookmarked.
            // The tap area is bigger than the ribbon so it's easy to hit on a phone.
            var hit = NewImage("Bookmark Ribbon", pv.root, null, new Color(0f, 0f, 0f, 0f), true);
            pv.ribbonRoot = hit.rectTransform;
            var ribbonButton = hit.gameObject.AddComponent<Button>();
            ribbonButton.transition = Selectable.Transition.None;
            ribbonButton.navigation = new Navigation { mode = Navigation.Mode.None };
            ribbonButton.onClick.AddListener(() => ToggleBookmark(pv));
            pv.ribbon = NewImage("Ribbon", hit.transform, s_RibbonOutline, Color.white, false);
            Place(pv.ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(48, 99));
            pv.ribbonPlus = NewText("Plus", pv.ribbon.transform, uiFont, 34, new Color(inkColor.r, inkColor.g, inkColor.b, 0.6f), TextAlignmentOptions.Center);
            pv.ribbonPlus.text = "+";
            Place(pv.ribbonPlus.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(48, 48));
            hit.gameObject.AddComponent<UIButtonJuice>().pressedScale = 0.88f;
            pv.ribbonRoot.gameObject.SetActive(false);

            ApplySide(pv, right);
            return pv;
        }

        void ApplySide(PageView pv, bool right)
        {
            var rt = pv.root;
            rt.anchorMin = right ? new Vector2(0.5f, 0f) : Vector2.zero;
            rt.anchorMax = right ? Vector2.one : new Vector2(0.5f, 1f);
            rt.pivot = right ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            Sprite s = right ? rightPageSprite : leftPageSprite;
            pv.paper.sprite = s;
            pv.paper.type = s != null && s.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            if (s == null) pv.paper.color = new Color32(250, 242, 222, 255);

            float spineSide = 84, outerSide = 70;
            float l = right ? spineSide : outerSide, r = right ? outerSide : spineSide;
            Stretch(pv.content, l, 92, r, 70);
            Stretch(pv.titleGroup, l, 92, r, 70);
            Stretch((RectTransform)pv.center.transform, l, 92, r, 70);

            // Ribbon in the outer margin, poking a little above the page like a real ribbon marker.
            if (pv.ribbonRoot != null)
                Place(pv.ribbonRoot, right ? new Vector2(1f, 1f) : new Vector2(0f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(right ? -40 : 40, 22), new Vector2(80, 124));
        }

        Button RoundButton(string name, Transform parent, Sprite icon, string fallbackText, Vector2 anchor, Vector2 pos, float size, Action onClick)
        {
            var img = NewImage(name, parent, roundButtonSprite, roundButtonSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.6f), true);
            img.preserveAspect = true;
            Place(img.rectTransform, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.7f, 0.8f, 1f, 1f);
            colors.disabledColor = Color.white;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => onClick());

            if (icon != null)
            {
                var ic = NewImage("Icon", img.transform, icon, Color.white, false);
                ic.preserveAspect = true;
                Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.5f, size * 0.5f));
            }
            else
            {
                var t = NewText("Label", img.transform, uiFont, size * 0.42f, Color.white, TextAlignmentOptions.Center);
                t.text = fallbackText;
                Stretch(t.rectTransform, 0, 0, 0, 0);
            }
            img.gameObject.AddComponent<UIButtonJuice>().pressedScale = 0.9f;
            return btn;
        }

        void ChooserButton(string title, string subtitle, Color color, Action onClick)
        {
            var img = NewImage("Book Button", _chooserList, roundedSprite, color, true);
            img.type = roundedSprite != null && roundedSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 104;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => onClick());

            var t = NewText("Label", img.transform, headingFont, 36, uiTextColor, TextAlignmentOptions.Left);
            t.text = string.IsNullOrWhiteSpace(subtitle) ? title : title + "\n<size=60%><alpha=#BB>" + subtitle + "</size>";
            Stretch(t.rectTransform, 34, 8, 34, 8);
            img.gameObject.AddComponent<UIButtonJuice>().pressedScale = 0.97f;
        }

        // ------------------------------------------------------------------ small helpers

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool raycast)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.richText = true;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }

        static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
