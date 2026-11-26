using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BlackCompuQuiz
{
    /// <summary>
    /// The BlackCompu "computer screen". It opens on the quiz main menu, where the student picks a
    /// subject (grouped by year level and semester), then one of that subject's quizzes; then it asks
    /// the questions and shows the score.
    /// It builds its own UI when the game starts, so you don't need a Canvas or prefab.
    /// Add this component to any object yourself only if you want to change the colors/font/sounds.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("BlackCompu/Quiz Screen")]
    public class QuizScreen : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Optional. Leave empty to use TextMeshPro's default font.")]
        public TMP_FontAsset font;
        public Color dimColor = new Color(0f, 0f, 0f, 0.8f);
        public Color bezelColor = new Color32(14, 14, 16, 255);
        public Color screenColor = new Color32(8, 20, 14, 255);
        public Color accentColor = new Color32(64, 255, 140, 255);
        public Color textColor = new Color32(220, 255, 230, 255);
        public Color buttonColor = new Color32(24, 52, 36, 255);
        public Color correctColor = new Color32(36, 150, 76, 255);
        public Color wrongColor = new Color32(165, 48, 48, 255);
        [Tooltip("Draw order. Higher shows on top of your other UI.")]
        public int sortingOrder = 500;
        public Vector2 referenceResolution = new Vector2(1280, 720);

        [Header("Quiz main menu")]
        [Tooltip("List subjects that have no quiz yet (greyed out, \"No quiz yet\"). " +
                 "Turn off to show only subjects students can take a quiz in.")]
        public bool showSubjectsWithoutQuizzes = true;

        [Tooltip("Subjects are found on the scene's year bookshelves. Add subjects here to also list ones " +
                 "that aren't on a bookshelf (or to use the menu in a scene without bookshelves).")]
        public List<ARCHIV3D.SubjectData> extraSubjects = new List<ARCHIV3D.SubjectData>();

        [Header("Scores")]
        [Range(0, 100)]
        [Tooltip("A quiz counts as Passed once the student's best score reaches this percent. " +
                 "Shown on the results screen and on My Scores.")]
        public int passingScore = 75;

        [Tooltip("How many of the latest attempts My Scores lists under Recent attempts.")]
        [Min(0)]
        public int recentAttemptsShown = 10;

        [Header("Sounds (optional)")]
        public AudioClip buttonSound;
        public AudioClip correctSound;
        public AudioClip wrongSound;
        public AudioClip finishSound;

        public bool IsOpen { get; private set; }
        public event Action Closed;

        static QuizScreen s_Instance;

        enum State { SubjectMenu, SubjectQuizzes, Question, Results, Review, Scores }

        /// <summary>One answer given during the current attempt, for the Review answers screen.</summary>
        class GivenAnswer
        {
            public QuizQuestion question;
            public string given;
            public string right;
            public bool correct;
        }

        /// <summary>One subject button on the quiz main menu.</summary>
        class SubjectEntry
        {
            public ARCHIV3D.SubjectData subject;
            public int year;
            public int semester;
            public string semesterLabel;
            public readonly List<IQuiz> quizzes = new List<IQuiz>();
        }

        // UI built at runtime
        bool _built;
        GameObject _canvasGO;
        RectTransform _safeArea;
        Button _backButton;
        TextMeshProUGUI _titleText;
        TextMeshProUGUI _progressText;
        ScrollRect _scroll;
        RectTransform _viewport;
        RectTransform _content;
        AudioSource _audio;
        Rect _lastSafeArea;

        // Menu state
        State _state;
        string _computerName = "BlackCompu";
        readonly List<ComputerQuiz> _computerQuizzes = new List<ComputerQuiz>(); // this computer's quizzes with no subject
        readonly List<SubjectEntry> _subjects = new List<SubjectEntry>();
        readonly List<int> _years = new List<int>();
        static int s_SelectedYear;          // remembered between visits, like a tab
        SubjectEntry _currentSubject;       // subject whose quizzes are shown (null = computer's own quizzes)
        Action _onClosed;

        // Quiz state
        IQuiz _quiz;
        readonly List<QuizQuestion> _questions = new List<QuizQuestion>();
        int _index;
        int _score;
        bool _answered;
        readonly List<KeyValuePair<int, Button>> _choiceButtons = new List<KeyValuePair<int, Button>>();
        TMP_InputField _input;
        Button _submitButton;
        TextMeshProUGUI _feedback;
        Button _nextButton;
        readonly List<GivenAnswer> _given = new List<GivenAnswer>();

        // Result of the attempt just finished (shown on the results screen, kept for Review answers)
        ARCHIV3D.QuizStats _statsBefore;     // this quiz's scores before the attempt
        ARCHIV3D.QuizStats _statsAfter;      // ...and after it
        int _resultPercent;
        int _resultSeconds;
        int _resultAttempt;

        const float HeaderHeight = 76f;

        // ------------------------------------------------------------------ events (progress tracking)

        /// <summary>A quiz was started (a computer's quiz or a subject's quiz).</summary>
        public static event Action<IQuiz> QuizStarted;
        /// <summary>A question was answered: quiz, question, the answer given, whether it was right.</summary>
        public static event Action<IQuiz, QuizQuestion, string, bool> QuestionAnswered;
        /// <summary>A quiz was finished: quiz, score, number of questions.</summary>
        public static event Action<IQuiz, int, int> QuizFinished;

        // ------------------------------------------------------------------ setup

        public static QuizScreen GetOrCreate()
        {
            if (s_Instance != null) return s_Instance;
            s_Instance = BlackCompuUtil.FindAny<QuizScreen>();
            if (s_Instance == null)
                s_Instance = new GameObject("BlackCompu Quiz Screen").AddComponent<QuizScreen>();
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
            if (Screen.safeArea != _lastSafeArea) ApplySafeArea();
            if (BackPressed()) Back();
        }

        static bool BackPressed()
        {
            // Android's back button arrives as Escape in both input systems.
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Shows the computer screen on the quiz main menu (choose a subject).
        /// The computer's own quizzes that have a Subject set are listed under that subject;
        /// the ones without a subject are listed under "On this computer".
        /// </summary>
        public void Open(string computerName, IList<ComputerQuiz> quizzes, Action onClosed = null)
        {
            Build();
            _computerName = string.IsNullOrWhiteSpace(computerName) ? "BlackCompu" : computerName;
            _computerQuizzes.Clear();
            if (quizzes != null)
                foreach (var q in quizzes)
                    if (q != null && q.subject == null && !_computerQuizzes.Contains(q)) _computerQuizzes.Add(q);
            _onClosed = onClosed;

            CollectSubjects();

            EnsureEventSystem();
            _canvasGO.SetActive(true);
            IsOpen = true;
            ApplySafeArea();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ShowSubjectMenu();
        }

        /// <summary>
        /// Opens just the quiz main menu (no computer of its own), e.g. from a UI Button.
        /// Freezing the player while it is open is up to the caller.
        /// </summary>
        public void OpenQuizMenu(string title = "Quizzes", Action onClosed = null)
        {
            Open(title, null, onClosed);
        }

        /// <summary>Closes the computer screen.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _canvasGO.SetActive(false);
            var callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
            Closed?.Invoke();
        }

        /// <summary>
        /// Same as the Android back button: goes back one step, or closes.
        /// Question / results -> the subject's quizzes -> the quiz main menu -> close.
        /// </summary>
        public void Back()
        {
            switch (_state)
            {
                case State.SubjectMenu:
                    Close();
                    break;
                case State.SubjectQuizzes:
                case State.Scores:
                    ShowSubjectMenu();
                    break;
                case State.Review:
                    ShowResults();
                    break;
                default:
                    if (_currentSubject != null) ShowSubjectQuizzes(_currentSubject);
                    else ShowSubjectMenu();
                    break;
            }
        }

        // ------------------------------------------------------------------ quiz main menu

        /// <summary>
        /// Finds every subject to list: the subjects on the scene's year bookshelves (in shelf order),
        /// the Extra Subjects, and any subject a computer quiz is linked to. Each subject gets its own
        /// quizzes (Subject asset > Quizzes) plus every computer quiz in the scene whose Subject is set to it.
        /// </summary>
        void CollectSubjects()
        {
            _subjects.Clear();
            _years.Clear();

            var shelves = new List<ARCHIV3D.YearBookshelf>(
                FindObjectsByType<ARCHIV3D.YearBookshelf>(FindObjectsInactive.Exclude));
            shelves.Sort((a, b) => a.yearLevel != b.yearLevel
                ? a.yearLevel.CompareTo(b.yearLevel)
                : string.CompareOrdinal(a.name, b.name));

            foreach (var shelf in shelves)
            {
                foreach (var semester in shelf.ShelvesToShow)
                {
                    string label = string.IsNullOrWhiteSpace(semester.label)
                        ? ARCHIV3D.SemesterShelf.LabelFor(semester.semester)
                        : semester.label;
                    foreach (var subject in semester.ValidSubjects)
                        AddSubject(subject, shelf.yearLevel, semester.semester, label);
                }
            }

            if (extraSubjects != null)
                foreach (var subject in extraSubjects)
                    if (subject != null)
                        AddSubject(subject, subject.yearLevel, subject.semester, ARCHIV3D.SemesterShelf.LabelFor(subject.semester));

            // Subjects' own quizzes first...
            foreach (var entry in _subjects)
                foreach (var quiz in entry.subject.PlayableQuizzes)
                    entry.quizzes.Add(quiz);

            // ...then computer quizzes linked to a subject (from every computer in the scene).
            var computerQuizzes = new List<ComputerQuiz>(
                FindObjectsByType<ComputerQuiz>(FindObjectsInactive.Exclude));
            computerQuizzes.Sort((a, b) => string.CompareOrdinal(a.QuizId, b.QuizId));
            foreach (var quiz in computerQuizzes)
            {
                if (quiz.subject == null || quiz.UsableQuestionCount == 0) continue;
                var entry = AddSubject(quiz.subject, quiz.subject.yearLevel, quiz.subject.semester,
                    ARCHIV3D.SemesterShelf.LabelFor(quiz.subject.semester));
                if (!entry.quizzes.Contains(quiz)) entry.quizzes.Add(quiz);
            }

            foreach (var entry in _subjects)
            {
                if (!showSubjectsWithoutQuizzes && entry.quizzes.Count == 0) continue;
                if (!_years.Contains(entry.year)) _years.Add(entry.year);
            }
            _years.Sort();

            // Keep the year tab the student used last time; otherwise the first year that has a quiz.
            if (!_years.Contains(s_SelectedYear))
            {
                s_SelectedYear = _years.Count > 0 ? _years[0] : 0;
                foreach (var entry in _subjects)
                {
                    if (entry.quizzes.Count == 0) continue;
                    s_SelectedYear = entry.year;
                    break;
                }
                if (!_years.Contains(s_SelectedYear) && _years.Count > 0) s_SelectedYear = _years[0];
            }
        }

        SubjectEntry AddSubject(ARCHIV3D.SubjectData subject, int year, int semester, string semesterLabel)
        {
            foreach (var e in _subjects)
                if (e.subject == subject) return e;
            var entry = new SubjectEntry
            {
                subject = subject,
                year = Mathf.Max(1, year),
                semester = semester,
                semesterLabel = semesterLabel,
            };
            _subjects.Add(entry);
            return entry;
        }

        /// <summary>The quiz main menu: year tabs, then that year's subjects grouped by semester.</summary>
        void ShowSubjectMenu()
        {
            _state = State.SubjectMenu;
            _currentSubject = null;
            ClearContent();
            SetHeader(_computerName, "Quiz Menu", false);

            bool anySubjects = _years.Count > 0;
            var titleRow = NewRect("Title Row", _content);
            var rowLayout = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 16f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            var heading = NewText(titleRow, anySubjects ? "Choose a subject" : "Quizzes", 30, accentColor, TextAlignmentOptions.Left, true);
            var headingLayout = heading.GetComponent<LayoutElement>();
            headingLayout.preferredWidth = 0f;
            headingLayout.flexibleWidth = 1f;
            var scoresButton = NewButton(titleRow, "My Scores", buttonColor, 56f, ShowScores, TextAlignmentOptions.Center);
            scoresButton.name = "My Scores Button";
            var scoresLayout = scoresButton.GetComponent<LayoutElement>();
            scoresLayout.minWidth = 200f;
            scoresLayout.preferredWidth = 200f;
            scoresLayout.flexibleWidth = 0f;

            if (!anySubjects)
            {
                NewText(_content, _subjects.Count == 0
                        ? "No subjects found.\n\nIn Unity, run Tools > ARCHIV3D > Set Up Year Subject Shelves, then add a quiz " +
                          "to a subject: Assets/ARCHIV3D/Subjects > (year) > (subject) > Quizzes > +."
                        : "No subject has a quiz yet.\n\nIn Unity, open Assets/ARCHIV3D/Subjects > (year) > (subject) and press + " +
                          "under Quizzes, then add its questions.",
                    26, textColor, TextAlignmentOptions.Left, false);
            }
            else
            {
                if (_years.Count > 1) BuildYearTabs();

                // This year's subjects, grouped by semester (in the order the shelves list them).
                var semesters = new List<string>();
                var yearSubjects = new List<SubjectEntry>();
                foreach (var entry in _subjects)
                {
                    if (entry.year != s_SelectedYear) continue;
                    if (!showSubjectsWithoutQuizzes && entry.quizzes.Count == 0) continue;
                    yearSubjects.Add(entry);
                    if (!semesters.Contains(entry.semesterLabel)) semesters.Add(entry.semesterLabel);
                }

                int columns = Columns;
                foreach (string semester in semesters)
                {
                    SectionHeading(semester);
                    RectTransform row = null;
                    int inRow = 0;
                    foreach (var entry in yearSubjects)
                    {
                        if (entry.semesterLabel != semester) continue;
                        if (row == null || inRow >= columns)
                        {
                            row = NewRow(_content);
                            inRow = 0;
                        }
                        SubjectCard(NewCell(row), entry);
                        inRow++;
                    }
                    if (row != null) FillRow(row, inRow, columns);
                }

                if (yearSubjects.Count == 0)
                    NewText(_content, "No quizzes for " + YearLabel(s_SelectedYear) + " yet.", 26, textColor,
                        TextAlignmentOptions.Left, false);
            }

            // This computer's own quizzes that aren't linked to a subject.
            if (_computerQuizzes.Count > 0)
            {
                SectionHeading("On this computer");
                foreach (var quiz in _computerQuizzes) QuizButton(quiz, null);
            }

            NewButton(_content, "Close", bezelColor, 72f, Close, TextAlignmentOptions.Center);
            FinishLayout(false);
        }

        void SectionHeading(string text)
        {
            var heading = NewText(_content, (text ?? "").ToUpperInvariant(), 20,
                Color.Lerp(accentColor, textColor, 0.35f), TextAlignmentOptions.Left, true);
            heading.characterSpacing = 6f;
            heading.margin = new Vector4(4f, 10f, 0f, 0f);
        }

        void BuildYearTabs()
        {
            var tabs = NewRect("Year Tabs", _content);
            var hl = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;

            foreach (int year in _years)
            {
                int y = year;
                bool selected = y == s_SelectedYear;
                var tab = NewButton(tabs, YearLabel(y), selected ? accentColor : buttonColor, 64f, () =>
                {
                    if (s_SelectedYear == y) return;
                    s_SelectedYear = y;
                    ShowSubjectMenu();
                }, TextAlignmentOptions.Center);
                tab.name = "Tab " + YearLabel(y);
                var layout = tab.GetComponent<LayoutElement>();
                layout.minWidth = 0f;
                layout.preferredWidth = 0f;   // equal-width tabs
                layout.flexibleWidth = 1f;
                if (selected)
                {
                    var label = tab.GetComponentInChildren<TextMeshProUGUI>();
                    label.color = screenColor;
                    label.fontStyle = FontStyles.Bold;
                }
            }
        }

        /// <summary>One subject's button: color strip, name, then code / quiz count / best score.</summary>
        void SubjectCard(RectTransform cell, SubjectEntry entry)
        {
            var subject = entry.subject;
            int count = entry.quizzes.Count;

            string info = string.IsNullOrWhiteSpace(subject.subjectCode) ? "" : subject.subjectCode.Trim() + "  |  ";
            if (count == 0) info += "No quiz yet";
            else
            {
                info += count == 1 ? "1 quiz" : count + " quizzes";
                int taken = 0, sumBest = 0;
                foreach (var q in entry.quizzes)
                {
                    var stats = ARCHIV3D.LibraryProgress.GetQuizStats(q.QuizId);
                    if (!stats.Taken) continue;
                    taken++;
                    sumBest += stats.best;
                }
                if (taken > 0) info += "  |  " + taken + " taken, avg " + Mathf.RoundToInt((float)sumBest / taken) + "%";
            }

            var btn = NewButton(cell, "<b>" + subject.DisplayName + "</b>\n<size=75%>" + info + "</size>",
                buttonColor, 104f, () => ShowSubjectQuizzes(entry), TextAlignmentOptions.Left);
            btn.name = "Subject " + subject.DisplayName;

            // A strip in the subject's own color (same color as its button on the bookshelf).
            var hl = btn.GetComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = false;
            hl.spacing = 16f;
            hl.padding = new RectOffset(16, 20, 14, 14);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            label.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var strip = NewImage("Color", btn.transform, subject.color);
            strip.raycastTarget = false;
            strip.transform.SetAsFirstSibling();
            var stripLayout = strip.gameObject.AddComponent<LayoutElement>();
            stripLayout.minWidth = 8f;
            stripLayout.preferredWidth = 8f;
            stripLayout.flexibleWidth = 0f;

            if (count == 0)
            {
                btn.interactable = false;
                ((Image)btn.targetGraphic).color = Color.Lerp(buttonColor, screenColor, 0.55f);
                label.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
                strip.color = new Color(subject.color.r, subject.color.g, subject.color.b, 0.4f);
            }
        }

        /// <summary>A subject's quizzes, one button per quiz.</summary>
        void ShowSubjectQuizzes(SubjectEntry entry)
        {
            if (entry == null) { ShowSubjectMenu(); return; }
            _state = State.SubjectQuizzes;
            _currentSubject = entry;
            ClearContent();
            var subject = entry.subject;
            SetHeader(subject.DisplayName, string.IsNullOrWhiteSpace(subject.subjectCode) ? "" : subject.subjectCode.Trim(), true);

            NewText(_content, entry.quizzes.Count == 1 ? "Tap the quiz to start" : "Choose a quiz",
                30, accentColor, TextAlignmentOptions.Left, true);
            NewText(_content, YearLabel(entry.year) + "  |  " + entry.semesterLabel +
                              (string.IsNullOrWhiteSpace(subject.description) ? "" : "\n" + subject.description.Trim()),
                22, new Color(textColor.r, textColor.g, textColor.b, 0.7f), TextAlignmentOptions.Left, false);

            foreach (var quiz in entry.quizzes) QuizButton(quiz, entry);
            if (entry.quizzes.Count == 0)
                NewText(_content, "No quiz for this subject yet.", 26, textColor, TextAlignmentOptions.Left, false);

            NewButton(_content, "Back to subjects", buttonColor, 72f, ShowSubjectMenu, TextAlignmentOptions.Center);
            NewButton(_content, "Close", bezelColor, 72f, Close, TextAlignmentOptions.Center);
            FinishLayout(false);
        }

        void QuizButton(IQuiz quiz, SubjectEntry from)
        {
            var q = quiz;
            int count = q.UsableQuestionCount;
            string info = count == 0 ? "no questions yet" : count == 1 ? "1 question" : count + " questions";
            info += "  |  " + StatsLine(ARCHIV3D.LibraryProgress.GetQuizStats(q.QuizId));
            if (!string.IsNullOrWhiteSpace(q.Description)) info += "\n" + q.Description.Trim();

            var btn = NewButton(_content, "<b>" + q.DisplayTitle + "</b>\n<size=75%>" + info + "</size>",
                buttonColor, 96f, () => { _currentSubject = from; StartQuiz(q); }, TextAlignmentOptions.Left);
            btn.interactable = count > 0;
        }

        /// <summary>Two subjects side by side on a wide (landscape) screen, one per row when it's tall.</summary>
        static int Columns => Screen.width >= Screen.height * 1.2f ? 2 : 1;

        static string YearLabel(int year) => ARCHIV3D.YearBookshelf.Ordinal(Mathf.Max(1, year)) + " Year";

        RectTransform NewRow(Transform parent)
        {
            var row = NewRect("Row", parent);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;
            hl.childAlignment = TextAnchor.UpperLeft;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;
            return row;
        }

        /// <summary>
        /// A column of a row. Every cell has the same width no matter how long its text is,
        /// and grows as tall as the button inside it.
        /// </summary>
        static RectTransform NewCell(Transform row)
        {
            var cell = NewRect("Cell", row);
            var vl = cell.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = true;
            var layout = cell.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.preferredWidth = 0f;
            layout.flexibleWidth = 1f;
            return cell;
        }

        /// <summary>Pads a half-full last row with empty cells so its buttons keep the same width.</summary>
        static void FillRow(RectTransform row, int used, int columns)
        {
            for (int i = used; i < columns; i++) NewCell(row).name = "Empty Cell";
        }

        // ------------------------------------------------------------------ quiz

        void StartQuiz(IQuiz quiz)
        {
            _quiz = quiz;
            _questions.Clear();
            var all = quiz.Questions;
            int questionCount = all != null ? all.Count : 0;
            for (int i = 0; i < questionCount; i++)
            {
                var q = all[i];
                if (q != null && q.IsUsable) _questions.Add(q);
                else Debug.LogWarning("[BlackCompu] Skipping question " + (i + 1) + " in \"" + quiz.DisplayTitle +
                                      "\" because it isn't filled in completely.", quiz.Context);
            }
            if (_questions.Count == 0)
            {
                if (_currentSubject != null) ShowSubjectQuizzes(_currentSubject);
                else ShowSubjectMenu();
                return;
            }
            if (quiz.ShuffleQuestions) Shuffle(_questions);

            _index = 0;
            _score = 0;
            _given.Clear();
            QuizStarted?.Invoke(quiz);
            ShowQuestion();
        }

        void ShowQuestion()
        {
            _state = State.Question;
            _answered = false;
            _choiceButtons.Clear();
            _input = null;
            _submitButton = null;

            var q = _questions[_index];
            ClearContent();
            SetHeader(_quiz.DisplayTitle, (_index + 1) + " / " + _questions.Count, true);

            NewText(_content, q.question, 32, textColor, TextAlignmentOptions.Left, true);

            if (q.type == QuestionType.MultipleChoice)
            {
                var order = new List<int>();
                for (int i = 0; i < q.choices.Count; i++) order.Add(i);
                if (_quiz.ShuffleChoices) Shuffle(order);

                foreach (int choiceIndex in order)
                {
                    int captured = choiceIndex;
                    var btn = NewButton(_content, q.choices[choiceIndex], buttonColor, 80f,
                        () => AnswerChoice(captured), TextAlignmentOptions.Left);
                    _choiceButtons.Add(new KeyValuePair<int, Button>(captured, btn));
                }
            }
            else
            {
                _input = NewInput(_content, "Type your answer...");
                _input.onSubmit.AddListener(_ => SubmitTyped());
                _submitButton = NewButton(_content, "Check answer", buttonColor, 80f, SubmitTyped, TextAlignmentOptions.Center);
            }

            _feedback = NewText(_content, "", 28, textColor, TextAlignmentOptions.Left, false);
            _feedback.gameObject.SetActive(false);

            bool last = _index == _questions.Count - 1;
            _nextButton = NewButton(_content, last ? "See results" : "Next", bezelColor, 80f, Next, TextAlignmentOptions.Center);
            _nextButton.gameObject.SetActive(false);

            FinishLayout(false);
        }

        void AnswerChoice(int choiceIndex)
        {
            if (_answered || _state != State.Question) return;
            _answered = true;

            var q = _questions[_index];
            bool correct = choiceIndex == q.correctChoice;

            foreach (var pair in _choiceButtons)
            {
                var btn = pair.Value;
                btn.interactable = false;
                var img = (Image)btn.targetGraphic;
                if (pair.Key == q.correctChoice) img.color = correctColor;
                else if (pair.Key == choiceIndex) img.color = wrongColor;
                else img.color = Color.Lerp(buttonColor, screenColor, 0.6f);
            }

            _given.Add(new GivenAnswer { question = q, given = q.choices[choiceIndex], right = q.choices[q.correctChoice], correct = correct });
            QuestionAnswered?.Invoke(_quiz, q, q.choices[choiceIndex], correct);
            ShowFeedback(correct, q.choices[q.correctChoice], q.explanation);
        }

        void SubmitTyped()
        {
            if (_answered || _state != State.Question || _input == null) return;
            string typed = _input.text;
            if (string.IsNullOrWhiteSpace(typed)) return;
            _answered = true;

            var q = _questions[_index];
            string given = Normalize(typed);
            bool correct = false;
            string firstAnswer = "";
            foreach (var a in q.acceptedAnswers)
            {
                if (string.IsNullOrWhiteSpace(a)) continue;
                if (firstAnswer.Length == 0) firstAnswer = a.Trim();
                if (Normalize(a) == given) { correct = true; break; }
            }

            _input.interactable = false;
            ((Image)_input.targetGraphic).color = correct ? correctColor : wrongColor;
            _submitButton.gameObject.SetActive(false);

            _given.Add(new GivenAnswer { question = q, given = typed.Trim(), right = firstAnswer, correct = correct });
            QuestionAnswered?.Invoke(_quiz, q, typed.Trim(), correct);
            ShowFeedback(correct, firstAnswer, q.explanation);
        }

        void ShowFeedback(bool correct, string rightAnswer, string explanation)
        {
            if (correct) _score++;
            PlayClip(correct ? correctSound : wrongSound);

            string text = correct
                ? "<b>Correct!</b>"
                : "<b>Not quite.</b> The answer is: <b>" + rightAnswer + "</b>";
            if (!string.IsNullOrWhiteSpace(explanation))
                text += "\n<color=#" + ColorUtility.ToHtmlStringRGB(textColor) + ">" + explanation + "</color>";

            _feedback.color = correct ? accentColor : new Color(1f, 0.6f, 0.6f);
            _feedback.text = text;
            _feedback.gameObject.SetActive(true);
            _nextButton.gameObject.SetActive(true);
            FinishLayout(true);
        }

        void Next()
        {
            _index++;
            if (_index < _questions.Count) ShowQuestion();
            else FinishQuiz();
        }

        /// <summary>Saves the attempt (through QuizFinished / LibraryProgress) once, then shows the results.</summary>
        void FinishQuiz()
        {
            int total = _questions.Count;
            _resultPercent = total > 0 ? Mathf.RoundToInt(100f * _score / total) : 0;
            _statsBefore = ARCHIV3D.LibraryProgress.GetQuizStats(_quiz.QuizId);
            QuizFinished?.Invoke(_quiz, _score, total);
            _statsAfter = ARCHIV3D.LibraryProgress.GetQuizStats(_quiz.QuizId);

            _resultAttempt = _statsAfter.attempts;
            _resultSeconds = 0;
            var latest = ARCHIV3D.LibraryProgress.RecentQuizResults(1);
            if (latest.Count > 0 && latest[0].quizId == _quiz.QuizId)
            {
                _resultSeconds = latest[0].seconds;
                _resultAttempt = latest[0].attempt;
            }

            PlayClip(finishSound);
            ShowResults();
        }

        /// <summary>The score of the attempt just finished, how it compares, and what to do next.</summary>
        void ShowResults()
        {
            _state = State.Results;
            ClearContent();
            SetHeader(_quiz.DisplayTitle, "Done", true);

            int total = _questions.Count;
            int percent = _resultPercent;
            string message =
                percent >= 100 ? "Perfect score!" :
                percent >= 80 ? "Great job!" :
                percent >= 50 ? "Nice work. Keep practicing!" :
                "Keep trying, you'll get it!";

            NewText(_content, _score + " / " + total, 72, accentColor, TextAlignmentOptions.Center, true);
            NewText(_content, percent + "%  |  " + message, 28, textColor, TextAlignmentOptions.Center, false);

            // New best / passed
            string badges = "";
            if (_statsBefore != null && _statsBefore.Taken && percent > _statsBefore.best)
                badges += "<color=#" + Hex(accentColor) + "><b>NEW BEST!</b></color>  (was " + _statsBefore.best + "%)\n";
            bool passedNow = percent >= passingScore;
            bool passedEver = _statsAfter != null && _statsAfter.best >= passingScore;
            if (passedNow)
                badges += "<color=#" + Hex(accentColor) + "><b>PASSED</b></color>  (passing score " + passingScore + "%)";
            else if (passedEver)
                badges += "Not passed this time, but you already passed this quiz (best " + _statsAfter.best + "%)";
            else
                badges += "<color=#" + Hex(Color.Lerp(wrongColor, Color.white, 0.45f)) + "><b>NOT PASSED YET</b></color>  " +
                          "(you need " + passingScore + "%)";
            NewText(_content, badges, 26, textColor, TextAlignmentOptions.Center, false);

            // Attempt / time / best / average
            var parts = new List<string> { "Attempt #" + Mathf.Max(1, _resultAttempt) };
            if (_resultSeconds > 0) parts.Add("Time " + Duration(_resultSeconds));
            if (_statsAfter != null && _statsAfter.Taken)
            {
                parts.Add("Best " + _statsAfter.best + "%");
                if (_statsAfter.attempts > 1) parts.Add("Average " + Mathf.RoundToInt(_statsAfter.average) + "%");
            }
            if (_statsBefore != null && _statsBefore.Taken)
            {
                int change = percent - _statsBefore.last;
                parts.Add("Last time " + _statsBefore.last + "%" + (change > 0 ? " (+" + change + ")" : change < 0 ? " (" + change + ")" : ""));
            }
            NewText(_content, string.Join("  |  ", parts), 22, new Color(textColor.r, textColor.g, textColor.b, 0.75f),
                TextAlignmentOptions.Center, false);

            var quiz = _quiz;
            if (_given.Count > 0)
            {
                int wrongCount = _given.Count - _score;
                NewButton(_content, "Review answers  (" + (wrongCount <= 0 ? "all correct" : wrongCount + " wrong") + ")",
                    buttonColor, 80f, ShowReview, TextAlignmentOptions.Center);
            }
            NewButton(_content, "Try again", buttonColor, 80f, () => StartQuiz(quiz), TextAlignmentOptions.Center);
            if (_currentSubject != null && _currentSubject.quizzes.Count > 1)
            {
                var subject = _currentSubject;
                NewButton(_content, "More " + subject.subject.DisplayName + " quizzes", buttonColor, 80f,
                    () => ShowSubjectQuizzes(subject), TextAlignmentOptions.Center);
            }
            NewButton(_content, "My Scores", buttonColor, 80f, ShowScores, TextAlignmentOptions.Center);
            NewButton(_content, "Back to subjects", buttonColor, 80f, ShowSubjectMenu, TextAlignmentOptions.Center);
            NewButton(_content, "Close", bezelColor, 72f, Close, TextAlignmentOptions.Center);

            FinishLayout(false);
        }

        /// <summary>Every question of the attempt just finished: the student's answer, and the right one if it was wrong.</summary>
        void ShowReview()
        {
            _state = State.Review;
            ClearContent();
            SetHeader(_quiz.DisplayTitle, "Review  " + _score + " / " + _given.Count, true);

            string right = Hex(Color.Lerp(correctColor, Color.white, 0.35f));
            string wrong = Hex(Color.Lerp(wrongColor, Color.white, 0.45f));
            string dim = Hex(Color.Lerp(textColor, screenColor, 0.35f));

            for (int i = 0; i < _given.Count; i++)
            {
                var a = _given[i];
                var panel = NewPanel(_content, a.correct ? Color.Lerp(buttonColor, correctColor, 0.18f)
                                                         : Color.Lerp(buttonColor, wrongColor, 0.22f));
                string text = "<b>" + (i + 1) + ". " + a.question.question.Trim() + "</b>\n" +
                              "Your answer: <color=#" + (a.correct ? right : wrong) + "><b>" + Safe(a.given) + "</b>  " +
                              (a.correct ? "(correct)" : "(wrong)") + "</color>";
                if (!a.correct) text += "\nCorrect answer: <color=#" + right + "><b>" + Safe(a.right) + "</b></color>";
                if (!string.IsNullOrWhiteSpace(a.question.explanation))
                    text += "\n<size=85%><color=#" + dim + ">" + a.question.explanation.Trim() + "</color></size>";
                NewText(panel, text, 24, textColor, TextAlignmentOptions.Left, false);
            }

            NewButton(_content, "Back to results", buttonColor, 72f, ShowResults, TextAlignmentOptions.Center);
            FinishLayout(false);
        }

        /// <summary>
        /// My Scores: overall summary, every quiz's best / last score and tries (by subject),
        /// and the latest attempts. Saved on the device by LibraryProgress.
        /// </summary>
        void ShowScores()
        {
            _state = State.Scores;
            _currentSubject = null;
            ClearContent();
            // The character's name (typed when the game started) instead of "Student".
            SetHeader("My Scores", Safe(ARCHIV3D.LibraryProgress.PlayerName), true);

            // Every quiz the menu knows about, by subject (all years), then this computer's own.
            int quizzes = 0, taken = 0, passed = 0, attempts = 0, sumBest = 0;
            var withQuizzes = new List<SubjectEntry>();
            foreach (var entry in _subjects)
            {
                if (entry.quizzes.Count == 0) continue;
                withQuizzes.Add(entry);
            }
            withQuizzes.Sort((a, b) => a.year != b.year ? a.year.CompareTo(b.year) : _subjects.IndexOf(a).CompareTo(_subjects.IndexOf(b)));

            var all = new List<IQuiz>();
            foreach (var entry in withQuizzes) all.AddRange(entry.quizzes);
            foreach (var q in _computerQuizzes) all.Add(q);
            foreach (var q in all)
            {
                var st = ARCHIV3D.LibraryProgress.GetQuizStats(q.QuizId);
                quizzes++;
                attempts += st.attempts;
                if (!st.Taken) continue;
                taken++;
                sumBest += st.best;
                if (st.best >= passingScore) passed++;
            }

            // Summary tiles
            var tiles = NewRow(_content);
            SummaryTile(NewCell(tiles), taken + " / " + quizzes, "quizzes taken");
            SummaryTile(NewCell(tiles), passed.ToString(), "passed (" + passingScore + "%+)");
            SummaryTile(NewCell(tiles), taken > 0 ? Mathf.RoundToInt((float)sumBest / taken) + "%" : "-", "average best score");
            SummaryTile(NewCell(tiles), attempts.ToString(), attempts == 1 ? "attempt" : "attempts");

            if (quizzes == 0)
                NewText(_content, "No quizzes yet.", 26, textColor, TextAlignmentOptions.Left, false);

            // Per subject
            foreach (var entry in withQuizzes)
            {
                var subject = entry.subject;
                SectionHeading(YearLabel(entry.year) + "  |  " + subject.DisplayName);
                foreach (var q in entry.quizzes) ScoreRow(q, entry);
            }
            if (_computerQuizzes.Count > 0)
            {
                SectionHeading("On this computer");
                foreach (var q in _computerQuizzes) ScoreRow(q, null);
            }

            // Recent attempts
            if (recentAttemptsShown > 0)
            {
                SectionHeading("Recent attempts");
                var recent = ARCHIV3D.LibraryProgress.RecentQuizResults(recentAttemptsShown);
                if (recent.Count == 0)
                {
                    NewText(_content, "No quiz taken yet. Pick a subject and take a quiz; your scores will show here.",
                        24, textColor, TextAlignmentOptions.Left, false);
                }
                else
                {
                    var panel = NewPanel(_content, Color.Lerp(buttonColor, screenColor, 0.4f));
                    var lines = new List<string>();
                    foreach (var r in recent)
                    {
                        string pass = r.percent >= passingScore ? "<color=#" + Hex(accentColor) + ">" : "";
                        string endPass = pass.Length > 0 ? "</color>" : "";
                        lines.Add(FriendlyTime(r.time) +
                                  "<pos=24%>" + Safe(r.quizTitle) + SubjectTag(r) +
                                  "<pos=74%>" + pass + r.score + "/" + r.total + "  " + r.percent + "%" + endPass +
                                  (r.seconds > 0 ? "<pos=90%>" + Duration(r.seconds) : ""));
                    }
                    NewText(panel, string.Join("\n", lines), 22, textColor, TextAlignmentOptions.Left, false).lineSpacing = 18f;
                }
            }

            NewButton(_content, "Back to subjects", buttonColor, 72f, ShowSubjectMenu, TextAlignmentOptions.Center);
            NewButton(_content, "Close", bezelColor, 72f, Close, TextAlignmentOptions.Center);
            FinishLayout(false);
        }

        void ScoreRow(IQuiz quiz, SubjectEntry entry)
        {
            var st = ARCHIV3D.LibraryProgress.GetQuizStats(quiz.QuizId);
            string label = "<b>" + quiz.DisplayTitle + "</b>\n<size=75%>" + StatsLine(st) + "</size>";
            var btn = NewButton(_content, label, buttonColor, 84f, () =>
            {
                if (entry != null) ShowSubjectQuizzes(entry);
                else { _currentSubject = null; StartQuiz(quiz); }
            }, TextAlignmentOptions.Left);
            if (!st.Taken)
            {
                ((Image)btn.targetGraphic).color = Color.Lerp(buttonColor, screenColor, 0.45f);
                btn.GetComponentInChildren<TextMeshProUGUI>().color = new Color(textColor.r, textColor.g, textColor.b, 0.6f);
            }
        }

        void SummaryTile(RectTransform cell, string value, string caption)
        {
            var panel = NewPanel(cell, Color.Lerp(buttonColor, screenColor, 0.25f));
            panel.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            NewText(panel, value, 40, accentColor, TextAlignmentOptions.Center, true);
            NewText(panel, caption, 20, new Color(textColor.r, textColor.g, textColor.b, 0.75f), TextAlignmentOptions.Center, false);
        }

        /// <summary>"best 80% (4/5)  |  last 60%  |  3 tries  |  PASSED", or "not taken yet".</summary>
        string StatsLine(ARCHIV3D.QuizStats st)
        {
            if (st == null || !st.Taken) return "not taken yet";
            string line = "best " + st.best + "%";
            if (st.bestTotal > 0) line += " (" + st.bestScore + "/" + st.bestTotal + ")";
            if (st.attempts > 1) line += "  |  last " + st.last + "%";
            line += "  |  " + (st.attempts == 1 ? "1 try" : st.attempts + " tries");
            if (st.best >= passingScore) line += "  |  <color=#" + Hex(accentColor) + "><b>PASSED</b></color>";
            return line;
        }

        string SubjectTag(ARCHIV3D.QuizResultRecord r)
        {
            foreach (var e in _subjects)
            {
                if (e.subject == null || e.subject.subjectId != r.subjectId) continue;
                string code = string.IsNullOrWhiteSpace(e.subject.subjectCode) ? e.subject.DisplayName : e.subject.subjectCode.Trim();
                return "  <size=80%>(" + Safe(code) + ")</size>";
            }
            return string.IsNullOrWhiteSpace(r.subjectName) ? "" : "  <size=80%>(" + Safe(r.subjectName) + ")</size>";
        }

        /// <summary>A plain colored box whose children are stacked and padded (like a button, but not clickable).</summary>
        RectTransform NewPanel(Transform parent, Color color)
        {
            var img = NewImage("Panel", parent, color);
            img.raycastTarget = false;
            var vl = img.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(22, 22, 16, 16);
            vl.spacing = 4f;
            vl.childAlignment = TextAnchor.UpperLeft;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            img.gameObject.AddComponent<LayoutElement>();
            return img.rectTransform;
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Stops a student's typed answer from being read as rich-text tags.</summary>
        static string Safe(string s) => string.IsNullOrEmpty(s) ? "" : "<noparse>" + s.Replace("</noparse>", "") + "</noparse>";

        static string Duration(int seconds)
        {
            if (seconds < 60) return seconds + "s";
            return (seconds / 60) + "m " + (seconds % 60).ToString("00") + "s";
        }

        /// <summary>"2026-11-12 17:48:03" -> "Nov 12, 5:48 PM" (or "Today, 5:48 PM").</summary>
        static string FriendlyTime(string saved)
        {
            if (!DateTime.TryParseExact(saved, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var t))
                return saved ?? "";
            string clock = t.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            if (t.Date == DateTime.Today) return "Today, " + clock;
            if (t.Date == DateTime.Today.AddDays(-1)) return "Yesterday, " + clock;
            return t.ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture) + ", " + clock;
        }

        // ------------------------------------------------------------------ UI building

        void Build()
        {
            if (_built) return;
            _built = true;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            // Canvas
            _canvasGO = new GameObject("BlackCompu Canvas", typeof(RectTransform));
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGO.AddComponent<GraphicRaycaster>();

            // Dark background that also blocks taps on the menu behind it
            var dim = NewImage("Dim", _canvasGO.transform, dimColor);
            Stretch(dim.rectTransform);

            _safeArea = NewRect("Safe Area", _canvasGO.transform);
            Stretch(_safeArea);

            // Monitor bezel + screen
            var bezel = NewImage("Bezel", _safeArea, bezelColor);
            bezel.rectTransform.anchorMin = new Vector2(0.03f, 0.04f);
            bezel.rectTransform.anchorMax = new Vector2(0.97f, 0.96f);
            bezel.rectTransform.offsetMin = Vector2.zero;
            bezel.rectTransform.offsetMax = Vector2.zero;

            var screen = NewImage("Screen", bezel.transform, screenColor);
            Stretch(screen.rectTransform, 14f);

            // Header: title, progress, close
            var header = NewRect("Header", screen.transform);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, HeaderHeight);
            var hl = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(24, 12, 8, 8);
            hl.spacing = 16f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = true;

            _backButton = NewButton(header, "<", buttonColor, 56f, Back, TextAlignmentOptions.Center);
            _backButton.name = "Back Button";
            var backLayout = _backButton.GetComponent<LayoutElement>();
            backLayout.minWidth = 64f;
            backLayout.preferredWidth = 64f;
            backLayout.flexibleWidth = 0f; // stay a small square; the title takes the spare width
            _backButton.gameObject.SetActive(false);

            _titleText = NewText(header, "BlackCompu", 30, accentColor, TextAlignmentOptions.Left, true);
            _titleText.enableAutoSizing = true;
            _titleText.fontSizeMin = 18;
            _titleText.fontSizeMax = 30;
            _titleText.overflowMode = TextOverflowModes.Ellipsis;
            var titleLayout = _titleText.GetComponent<LayoutElement>();
            titleLayout.minWidth = 0f;
            titleLayout.preferredWidth = 0f; // take only the space left over, so long titles shrink instead of pushing the X off
            titleLayout.flexibleWidth = 1f;

            _progressText = NewText(header, "", 24, textColor, TextAlignmentOptions.Right, false);

            var closeBtn = NewButton(header, "X", buttonColor, 56f, Close, TextAlignmentOptions.Center);
            var closeLayout = closeBtn.GetComponent<LayoutElement>();
            closeLayout.minWidth = 64f;
            closeLayout.preferredWidth = 64f;
            closeLayout.flexibleWidth = 0f;

            var line = NewImage("Header Line", screen.transform, accentColor);
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.anchoredPosition = new Vector2(0f, -HeaderHeight);
            line.rectTransform.sizeDelta = new Vector2(0f, 2f);
            line.raycastTarget = false;

            // Scrollable body
            var scrollRect = NewRect("Scroll", screen.transform);
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = Vector2.zero;
            scrollRect.offsetMax = new Vector2(0f, -HeaderHeight - 2f);
            _scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30f;

            var viewportImg = NewImage("Viewport", scrollRect, new Color(0f, 0f, 0f, 0f));
            viewportImg.canvasRenderer.cullTransparentMesh = false;
            _viewport = viewportImg.rectTransform;
            Stretch(_viewport);
            _viewport.gameObject.AddComponent<RectMask2D>();

            _content = NewRect("Content", _viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            var vl = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(28, 28, 24, 28);
            vl.spacing = 14f;
            vl.childAlignment = TextAnchor.UpperCenter;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = _viewport;
            _scroll.content = _content;
        }

        void SetHeader(string title, string progress, bool showBack)
        {
            _backButton.gameObject.SetActive(showBack);
            _titleText.text = title;
            _progressText.text = progress;
            _progressText.gameObject.SetActive(!string.IsNullOrEmpty(progress));
        }

        void ClearContent()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i).gameObject;
                child.SetActive(false); // hidden right away so layout ignores it
                Destroy(child);
            }
        }

        void FinishLayout(bool scrollToBottom)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.velocity = Vector2.zero;
            bool overflows = _content.rect.height > _viewport.rect.height;
            if (scrollToBottom && overflows) _scroll.verticalNormalizedPosition = 0f;
            else _content.anchoredPosition = Vector2.zero;
        }

        void ApplySafeArea()
        {
            _lastSafeArea = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = _lastSafeArea.position;
            Vector2 max = _lastSafeArea.position + _lastSafeArea.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            _safeArea.anchorMin = min;
            _safeArea.anchorMax = max;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>Creates an EventSystem if the scene has none (buttons need one).</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (BlackCompuUtil.FindAny<EventSystem>() != null) return;

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

        static Image NewImage(string name, Transform parent, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        TextMeshProUGUI NewText(Transform parent, string text, float size, Color color,
            TextAlignmentOptions alignment, bool bold)
        {
            var rt = NewRect("Text", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.richText = true;
            t.raycastTarget = false;
            if (bold) t.fontStyle = FontStyles.Bold;
            rt.gameObject.AddComponent<LayoutElement>();
            return t;
        }

        Button NewButton(Transform parent, string label, Color color, float minHeight, Action onClick,
            TextAlignmentOptions alignment)
        {
            var img = NewImage("Button", parent, color);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 1f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.8f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white; // keep the green/red answer colors visible
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var layout = img.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = minHeight;

            // Lets the button grow taller when its text wraps onto more lines
            var hl = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(22, 22, 14, 14);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;

            var text = NewText(img.transform, label, 26, textColor, alignment, false);
            text.name = "Label";

            btn.onClick.AddListener(() =>
            {
                PlayClip(buttonSound);
                onClick?.Invoke();
            });
            return btn;
        }

        TMP_InputField NewInput(Transform parent, string placeholder)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = "Answer Input";
            go.layer = parent.gameObject.layer;
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var input = go.GetComponent<TMP_InputField>();
            ((Image)input.targetGraphic).color = new Color(0f, 0f, 0f, 0.55f);
            var colors = input.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            input.colors = colors;

            if (font != null) input.fontAsset = font;
            input.pointSize = 28;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 200;
            input.caretColor = accentColor;
            input.customCaretColor = true;
            input.selectionColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);

            input.textComponent.color = textColor;
            input.textComponent.alignment = TextAlignmentOptions.Left;
            var ph = input.placeholder as TextMeshProUGUI;
            if (ph != null)
            {
                ph.text = placeholder;
                ph.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
                ph.alignment = TextAlignmentOptions.Left;
            }

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 72f;
            layout.preferredHeight = 72f;
            return input;
        }

        // ------------------------------------------------------------------ helpers

        void PlayClip(AudioClip clip)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip);
        }


        static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var parts = s.Trim().ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
