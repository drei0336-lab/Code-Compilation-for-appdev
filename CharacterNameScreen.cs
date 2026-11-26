using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ARCHIV3D
{
    /// <summary>
    /// "Name your character" card. It opens as soon as the game scene (Main Map) starts, so right after the
    /// title screen's Play button. The player types a name and taps Start; only then can they move, look
    /// around and use the computers, bookshelves and NPCs.
    /// The name is saved with the student progress (LibraryProgress.PlayerName) and shows on My Scores.
    ///
    /// It starts by itself in any scene with a FirstPersonController and builds its own UI, borrowing the
    /// fonts and art of the scene's "ARCHIV3D Dialogue Screen" so it matches Filipe's talking box.
    /// Add this component to an object in Main Map only to change its text, colors or options
    /// (untick the component to turn the name screen off).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ARCHIV3D/Character Name Screen")]
    public class CharacterNameScreen : MonoBehaviour
    {
        [Header("When to ask")]
        [Tooltip("Ask every time the game starts (the last name is already filled in, so it's one tap). " +
                 "Off = ask only until a name has been saved on this device.")]
        public bool askEveryTime = true;

        [Header("Text")]
        public string tagText = "NEW STUDENT";
        public string heading = "Name your character";
        [TextArea(2, 4)]
        public string message = "This is what the library will call you. It also shows on your quiz scores.";
        public string placeholder = "Type a name...";
        public string startText = "START EXPLORING";
        [Tooltip("Shown for a moment after Start. {name} is replaced by the name.")]
        public string welcomeText = "Welcome, {name}!";
        [Range(2, 24)]
        public int maxLength = 16;

        [Header("Fonts (optional; empty = same as the Dialogue Screen)")]
        public TMP_FontAsset headingFont;
        public Material headingFontMaterial;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset labelFont;

        [Header("Art (optional; empty = same as the Dialogue Screen)")]
        public Sprite panelSprite;
        public Sprite borderSprite;
        public Sprite pillSprite;

        [Header("Colors")]
        public Color dimColor = new Color(0.02f, 0.015f, 0.01f, 0.55f);
        public Color panelColor = new Color32(34, 24, 16, 245);
        public Color borderColor = new Color(240f / 255f, 190f / 255f, 84f / 255f, 0.9f);
        public Color tagColor = new Color(16f / 255f, 13f / 255f, 11f / 255f, 0.92f);
        public Color fieldColor = new Color32(18, 13, 9, 255);
        public Color headingColor = new Color32(255, 226, 150, 255);
        public Color textColor = new Color32(255, 246, 224, 255);
        public Color buttonColor = new Color(240f / 255f, 190f / 255f, 84f / 255f, 1f);
        public Color buttonTextColor = new Color32(46, 28, 8, 255);
        public Color errorColor = new Color32(255, 140, 110, 255);

        [Header("Layout")]
        [Tooltip("Draw order. Above the game controls and the dialogue box, below the quiz computer.")]
        public int sortingOrder = 460;
        public float maxWidth = 960f;

        /// <summary>Raised when the player confirms a name (after it is saved).</summary>
        public static event Action<string> NameConfirmed;

        /// <summary>True while the name card is on screen (the player can't move yet).</summary>
        public static bool IsAsking => s_Instance != null && s_Instance.IsOpen;

        public bool IsOpen { get; private set; }

        static CharacterNameScreen s_Instance;

        // built UI
        bool _built;
        GameObject _canvasGO;
        CanvasGroup _group;
        RectTransform _card;
        TMP_Text _heading;
        TMP_InputField _input;
        Image _fieldBorder;
        TMP_Text _hint;
        TMP_Text _counter;
        Button _start;
        TMP_Text _startLabel;

        // state
        bool _closing;
        Coroutine _anim;
        Coroutine _shake;
        float _inputBlockedUntil;
        readonly System.Collections.Generic.List<Behaviour> _frozen = new System.Collections.Generic.List<Behaviour>();
        CursorLockMode _previousLock;
        bool _previousCursorVisible;
        CanvasGroup _hud;
        float _hudAlpha;
        bool _hudRaycasts, _hudInteractable;

        // ------------------------------------------------------------------ starting by itself

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            s_Instance = null;
            NameConfirmed = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // The first scene of a run (pressing Play in the Editor on Main Map, or a build that starts there).
        // sceneLoaded doesn't always fire for that one, so check it here too; Open() ignores a second call.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AfterFirstSceneLoad() => AskIfGameScene();

        // Every scene loaded later, e.g. Main Map after the title screen's Play button.
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            AskIfGameScene();
        }

        static void AskIfGameScene()
        {
            // Only game scenes (the ones with the player); the title screen has none.
            if (UnityEngine.Object.FindAnyObjectByType<FirstPersonController>() == null) return;

            var screen = GetOrCreate();
            if (!screen.isActiveAndEnabled) return;                         // component unticked = off
            if (!screen.askEveryTime && LibraryProgress.HasChosenName) return;
            screen.Open();
        }

        public static CharacterNameScreen GetOrCreate()
        {
            if (s_Instance != null) return s_Instance;
            s_Instance = UnityEngine.Object.FindAnyObjectByType<CharacterNameScreen>();
            if (s_Instance == null) s_Instance = new GameObject("ARCHIV3D Character Name Screen").AddComponent<CharacterNameScreen>();
            return s_Instance;
        }

        void Awake()
        {
            if (s_Instance == null) s_Instance = this;
        }

        void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Shows the card, freezes the player and hides the game controls until a name is confirmed.</summary>
        public void Open()
        {
            if (IsOpen) return;
            Build();

            _closing = false;
            string saved = LibraryProgress.HasChosenName ? Clean(LibraryProgress.PlayerName) : "";
            _input.SetTextWithoutNotify(saved);
            _heading.text = heading;
            _startLabel.text = startText;
            RefreshState(false);

            EnsureEventSystem();
            FitWidth();
            FreezePlayer(true);
            HideHud(true);
            _canvasGO.SetActive(true);
            IsOpen = true;
            _inputBlockedUntil = Time.unscaledTime + 0.2f;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Fade(true));
        }

        /// <summary>Saves the typed name and lets the player play. Does nothing if the box is empty.</summary>
        public void Confirm()
        {
            if (!IsOpen || _closing) return;
            string name = Clean(_input.text);
            if (name.Length == 0)
            {
                _hint.text = "Type a name first.";
                _hint.color = errorColor;
                if (_shake != null) StopCoroutine(_shake);
                _shake = StartCoroutine(Shake());
                if (!Application.isMobilePlatform) _input.ActivateInputField();
                return;
            }

            _closing = true;
            LibraryProgress.PlayerName = name;
            _input.SetTextWithoutNotify(name);
            _input.DeactivateInputField();
            _input.interactable = false;
            _start.interactable = false;
            _heading.text = string.IsNullOrEmpty(welcomeText) ? heading : welcomeText.Replace("{name}", NoParse(name));

            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(CloseAfterWelcome(name));
        }

        // ------------------------------------------------------------------ name rules

        /// <summary>Letters (any language), numbers, single spaces and - ' . _ ; no rich-text tags; trimmed to Max Length.</summary>
        public string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw.Trim())
            {
                if (!Allowed(c)) continue;
                if (c == ' ' && (sb.Length == 0 || sb[sb.Length - 1] == ' ')) continue;
                sb.Append(c);
            }
            string s = sb.ToString().Trim();
            if (s.Length > maxLength) s = s.Substring(0, maxLength).TrimEnd();
            return s;
        }

        static bool Allowed(char c) =>
            char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'' || c == '.' || c == '_';

        char ValidateChar(string text, int index, char c)
        {
            if (!Allowed(c)) return '\0';
            if (c == ' ' && (index == 0 || (index - 1 < text.Length && text[index - 1] == ' '))) return '\0';
            return c;
        }

        void RefreshState(bool typed)
        {
            int length = _input.text.Length;
            bool ok = Clean(_input.text).Length > 0;
            _counter.text = length + " / " + maxLength;
            _start.interactable = ok && !_closing;
            _startLabel.color = new Color(buttonTextColor.r, buttonTextColor.g, buttonTextColor.b, ok ? 1f : 0.55f);

            if (!typed || ok)
            {
                _hint.text = Application.isMobilePlatform
                    ? "Tap the box to type, then tap Start."
                    : "Type a name, then press Enter.";
                _hint.color = new Color(textColor.r, textColor.g, textColor.b, 0.55f);
            }
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen || _closing) return;
            if (Time.unscaledTime < _inputBlockedUntil) return;
            // Enter confirms even when the text box isn't focused (when it is, the box's own Submit does it).
            if (!_input.isFocused && EnterPressed()) Confirm();
        }

        static bool EnterPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) return true;
#endif
            return false;
        }

        // ------------------------------------------------------------------ freezing the player

        void FreezePlayer(bool freeze)
        {
            if (freeze)
            {
                _frozen.Clear();
                var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
                if (player != null)
                {
                    StopWalkAnimation(player);
                    if (player.enabled) { player.enabled = false; _frozen.Add(player); }
                }
                var interactor = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>();
                if (interactor != null && interactor.enabled) { interactor.enabled = false; _frozen.Add(interactor); }

                _previousLock = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                foreach (var b in _frozen)
                    if (b != null) b.enabled = true;
                _frozen.Clear();
                Cursor.lockState = _previousLock;
                Cursor.visible = _previousCursorVisible;
            }
        }

        static void StopWalkAnimation(Component player)
        {
            var animator = player.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float)
                {
                    animator.SetFloat("Speed", 0f);
                    return;
                }
            }
        }

        /// <summary>Hides the joystick / Run / Interact canvas (same way the Dialogue Screen does).</summary>
        void HideHud(bool hide)
        {
            if (hide)
            {
                _hud = null;
                Canvas hudCanvas = null;
                var player = UnityEngine.Object.FindAnyObjectByType<FirstPersonController>();
                if (player != null && player.mobileJoystick != null) hudCanvas = player.mobileJoystick.GetComponentInParent<Canvas>(true);
                if (hudCanvas == null)
                {
                    var sprint = UnityEngine.Object.FindAnyObjectByType<SprintButtonHandler>();
                    if (sprint != null) hudCanvas = sprint.GetComponentInParent<Canvas>(true);
                }
                if (hudCanvas == null) return;
                hudCanvas = hudCanvas.rootCanvas;
                if (hudCanvas.gameObject == _canvasGO) return;

                _hud = hudCanvas.GetComponent<CanvasGroup>();
                if (_hud == null) _hud = hudCanvas.gameObject.AddComponent<CanvasGroup>();
                _hudAlpha = _hud.alpha;
                _hudRaycasts = _hud.blocksRaycasts;
                _hudInteractable = _hud.interactable;
                _hud.alpha = 0f;
                _hud.blocksRaycasts = false;
                _hud.interactable = false;
            }
            else if (_hud != null)
            {
                _hud.alpha = _hudAlpha;
                _hud.blocksRaycasts = _hudRaycasts;
                _hud.interactable = _hudInteractable;
                _hud = null;
            }
        }

        // ------------------------------------------------------------------ animation

        IEnumerator Fade(bool opening)
        {
            float dur = opening ? 0.25f : 0.2f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / dur);
                float e = opening ? 1f - (1f - x) * (1f - x) : x * x;
                _group.alpha = opening ? e : 1f - e;
                float sc = opening ? Mathf.Lerp(0.94f, 1f, e) : Mathf.Lerp(1f, 0.97f, e);
                _card.localScale = new Vector3(sc, sc, 1f);
                yield return null;
            }
            _group.alpha = opening ? 1f : 0f;
            _card.localScale = Vector3.one;
            _anim = null;

            if (opening)
            {
                // On a computer the cursor goes straight into the box; on a phone the keyboard opens when they tap it.
                // (A name that's already filled in is selected, so typing replaces it and Enter keeps it.)
                if (!Application.isMobilePlatform) _input.ActivateInputField();
            }
            else FinishClose();
        }

        IEnumerator CloseAfterWelcome(string name)
        {
            yield return new WaitForSecondsRealtime(0.7f);
            _anim = StartCoroutine(Fade(false));
        }

        void FinishClose()
        {
            IsOpen = false;
            _canvasGO.SetActive(false);
            HideHud(false);
            // Give control back one frame later so the tap on Start isn't read as a tap on the world.
            StartCoroutine(UnfreezeNextFrame());
            NameConfirmed?.Invoke(LibraryProgress.PlayerName);
        }

        IEnumerator UnfreezeNextFrame()
        {
            yield return null;
            FreezePlayer(false);
            _closing = false;
            _input.interactable = true;
        }

        IEnumerator Shake()
        {
            float t = 0f;
            const float dur = 0.35f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / dur);
                _card.anchoredPosition = new Vector2(Mathf.Sin(t * 60f) * 14f * k, CardY);
                yield return null;
            }
            _card.anchoredPosition = new Vector2(0f, CardY);
            _shake = null;
        }

        // ------------------------------------------------------------------ building

        const float CardY = 30f;

        void FitWidth()
        {
            // The canvas scales to a 1080-unit-tall screen, so this is the safe area's width in canvas units.
            float available = maxWidth;
            if (Screen.height > 0)
                available = Screen.safeArea.width * 1080f / Screen.height - 2f * 90f;
            _card.sizeDelta = new Vector2(Mathf.Clamp(available, 600f, maxWidth), _card.sizeDelta.y);
        }

        /// <summary>Empty font / art slots take the Dialogue Screen's, so both boxes look the same.</summary>
        void BorrowLook()
        {
            var d = UnityEngine.Object.FindAnyObjectByType<DialogueScreen>();
            if (d == null) return;
            if (headingFont == null) headingFont = d.nameFont;
            if (headingFontMaterial == null && headingFont == d.nameFont) headingFontMaterial = d.nameFontMaterial;
            if (bodyFont == null) bodyFont = d.bodyFont;
            if (labelFont == null) labelFont = d.labelFont;
            if (panelSprite == null) panelSprite = d.panelSprite;
            if (borderSprite == null) borderSprite = d.borderSprite;
            if (pillSprite == null) pillSprite = d.pillSprite;
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            BorrowLook();

            _canvasGO = new GameObject("ARCHIV3D Name Canvas", typeof(RectTransform));
            _canvasGO.SetActive(false); // stay off while the parts (and the text box) are put together
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
            _group = _canvasGO.AddComponent<CanvasGroup>();

            // Darkens the library behind and stops taps reaching the game
            var dim = NewImage("Dim", _canvasGO.transform, null, dimColor, true);
            Stretch(dim.rectTransform, 0, 0, 0, 0);

            var safe = NewRect("Safe Area", _canvasGO.transform);
            Stretch(safe, 0, 0, 0, 0);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // The card
            var cardImg = NewImage("Name Card", safe, panelSprite, panelColor, true);
            _card = cardImg.rectTransform;
            Place(_card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, CardY), new Vector2(maxWidth, 560));
            var v = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(64, 64, 72, 56);
            v.spacing = 16f;
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            var cardFit = cardImg.gameObject.AddComponent<ContentSizeFitter>();
            cardFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            cardFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var border = NewImage("Border", _card, borderSprite, borderSprite != null ? borderColor : new Color(0, 0, 0, 0), false);
            Stretch(border.rectTransform, 0, 0, 0, 0);
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // Tag on the card's top-left edge, like the speaker name on the dialogue box
            var tag = NewImage("Tag", _card, pillSprite, tagColor, false);
            var trt = tag.rectTransform;
            Place(trt, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(46, 2), new Vector2(260, 60));
            tag.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var th = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            th.padding = new RectOffset(30, 30, 4, 2);
            th.childAlignment = TextAnchor.MiddleCenter;
            th.childControlWidth = true;
            th.childControlHeight = true;
            th.childForceExpandWidth = false;
            th.childForceExpandHeight = true;
            var tfit = tag.gameObject.AddComponent<ContentSizeFitter>();
            tfit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tfit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            var tagLabel = NewText("Label", trt, headingFont, 30, headingColor, TextAlignmentOptions.Center);
            tagLabel.text = tagText;
            tagLabel.characterSpacing = 4;
            if (headingFontMaterial != null) tagLabel.fontSharedMaterial = headingFontMaterial;

            // Heading + message
            _heading = NewText("Heading", _card, headingFont, 58, headingColor, TextAlignmentOptions.Left);
            _heading.text = heading;
            if (headingFontMaterial != null) _heading.fontSharedMaterial = headingFontMaterial;
            FillWidth(_heading);

            var msg = NewText("Message", _card, bodyFont, 30, new Color(textColor.r, textColor.g, textColor.b, 0.82f), TextAlignmentOptions.Left);
            msg.text = message;
            msg.lineSpacing = 4;
            FillWidth(msg);

            Spacer(_card, 6f);

            // Text box
            BuildInput(_card);

            // Hint (left) + letter count (right)
            var row = NewRect("Hint Row", _card);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
            _hint = NewText("Hint", row, bodyFont, 24, textColor, TextAlignmentOptions.MidlineLeft);
            Stretch(_hint.rectTransform, 6, 0, 140, 0);
            _hint.enableAutoSizing = true;
            _hint.fontSizeMin = 16;
            _hint.fontSizeMax = 24;
            _counter = NewText("Count", row, labelFont != null ? labelFont : bodyFont, 22,
                new Color(headingColor.r, headingColor.g, headingColor.b, 0.7f), TextAlignmentOptions.MidlineRight);
            Stretch(_counter.rectTransform, 0, 0, 6, 0);
            _counter.characterSpacing = 2;

            Spacer(_card, 10f);

            // Start button, centered
            var buttonRow = NewRect("Button Row", _card);
            var bh = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bh.childAlignment = TextAnchor.MiddleCenter;
            bh.childControlWidth = true;
            bh.childControlHeight = true;
            bh.childForceExpandWidth = false;
            bh.childForceExpandHeight = false;
            _start = PillButton("Start Button", buttonRow, startText, Confirm, out _startLabel);
        }

        void BuildInput(Transform parent)
        {
            var fieldImg = NewImage("Name Box", parent, panelSprite, fieldColor, true);
            var le = fieldImg.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 104f;
            le.preferredHeight = 104f;

            _fieldBorder = NewImage("Box Border", fieldImg.transform, borderSprite, borderSprite != null ? FieldBorder(false) : new Color(0, 0, 0, 0), false);
            Stretch(_fieldBorder.rectTransform, 0, 0, 0, 0);

            var area = NewRect("Text Area", fieldImg.transform);
            Stretch(area, 34, 6, 34, 6);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = NewText("Placeholder", area, bodyFont, 42, new Color(textColor.r, textColor.g, textColor.b, 0.35f), TextAlignmentOptions.MidlineLeft);
            ph.text = placeholder;
            ph.fontStyle = FontStyles.Italic;
            Stretch(ph.rectTransform, 0, 0, 0, 0);

            var txt = NewText("Text", area, bodyFont, 42, textColor, TextAlignmentOptions.MidlineLeft);
            txt.richText = false;
            Stretch(txt.rectTransform, 0, 0, 0, 0);

            _input = fieldImg.gameObject.AddComponent<TMP_InputField>();
            _input.targetGraphic = fieldImg;
            _input.textViewport = area;
            _input.textComponent = txt;
            _input.placeholder = ph;
            if (bodyFont != null) _input.fontAsset = bodyFont;
            _input.pointSize = 42;
            _input.richText = false;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.contentType = TMP_InputField.ContentType.Standard;
            _input.characterLimit = maxLength;
            _input.onValidateInput = ValidateChar;
            _input.customCaretColor = true;
            _input.caretColor = headingColor;
            _input.caretWidth = 3;
            _input.caretBlinkRate = 0.85f;
            _input.selectionColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.35f);
            _input.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = _input.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.fadeDuration = 0.08f;
            _input.colors = colors;

            _input.onValueChanged.AddListener(_ => RefreshState(true));
            _input.onSubmit.AddListener(_ => Confirm());
            _input.onSelect.AddListener(_ => _fieldBorder.color = FieldBorder(true));
            _input.onDeselect.AddListener(_ => _fieldBorder.color = FieldBorder(false));
        }

        Color FieldBorder(bool focused) =>
            borderSprite == null ? new Color(0, 0, 0, 0)
                : new Color(borderColor.r, borderColor.g, borderColor.b, focused ? 1f : 0.45f);

        Button PillButton(string name, Transform parent, string label, Action onClick, out TMP_Text text)
        {
            var img = NewImage(name, parent, pillSprite, buttonColor, true);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 470f;
            le.preferredHeight = 96f;
            le.minHeight = 96f;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = Color.white;
            c.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(0.55f, 0.5f, 0.45f, 0.6f);
            c.fadeDuration = 0.06f;
            btn.colors = c;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => onClick());

            text = NewText("Label", img.transform, labelFont != null ? labelFont : bodyFont, 34, buttonTextColor, TextAlignmentOptions.Center);
            text.text = label;
            text.characterSpacing = 3;
            Stretch(text.rectTransform, 24, 0, 24, 0);

            var juice = img.gameObject.AddComponent<UIButtonJuice>();
            juice.hoverScale = 1.04f;
            juice.pressedScale = 0.94f;
            return btn;
        }

        // ------------------------------------------------------------------ small helpers

        static string NoParse(string s) => "<noparse>" + s + "</noparse>";

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

        /// <summary>Text takes the card's width and wraps, instead of growing to its one-line width.</summary>
        static void FillWidth(TMP_Text t)
        {
            var le = t.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            le.flexibleWidth = 1f;
        }

        static void Spacer(Transform parent, float height)
        {
            var rt = NewRect("Spacer", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 0;
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
            t.textWrappingMode = TextWrappingModes.Normal;   // texts made from code don't always pick up the TMP default
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
