using System;
using System.Collections;
using System.Collections.Generic;
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
    /// The talking box NPCs use: a walnut card with a gold border at the bottom-center of the screen,
    /// the speaker's name on a pill, text that types itself out, and Next / Close buttons.
    /// Tap anywhere (or press Space / Enter / E) to continue; Esc or the Android back button closes it.
    /// The UI is built when the game starts; the fonts, art and colors below can be changed in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ARCHIV3D/Dialogue Screen")]
    public class DialogueScreen : MonoBehaviour
    {
        [Header("Fonts (optional)")]
        [Tooltip("Speaker name. ARCHIV3D uses Pixelify.")]
        public TMP_FontAsset nameFont;
        [Tooltip("Optional material for the name (the Pixelify drop shadow).")]
        public Material nameFontMaterial;
        [Tooltip("What the NPC says. ARCHIV3D uses Outfit Regular.")]
        public TMP_FontAsset bodyFont;
        [Tooltip("Small labels (page count). ARCHIV3D uses Outfit Bold.")]
        public TMP_FontAsset labelFont;

        [Header("Art (optional)")]
        public Sprite panelSprite;
        public Sprite borderSprite;
        public Sprite pillSprite;
        public Sprite shadeSprite;
        public Sprite nextButtonSprite;
        public Sprite closeButtonSprite;
        public Sprite nextIcon;
        public Sprite closeIcon;

        [Header("Colors")]
        public Color panelColor = new Color32(34, 24, 16, 245);
        public Color borderColor = new Color(240f / 255f, 190f / 255f, 84f / 255f, 0.9f);
        public Color pillColor = new Color(16f / 255f, 13f / 255f, 11f / 255f, 0.92f);
        public Color nameColor = new Color32(255, 226, 150, 255);
        public Color textColor = new Color32(255, 246, 224, 255);
        public Color nextIconColor = new Color32(46, 28, 8, 255);

        [Header("Layout")]
        public int sortingOrder = 440;
        [Tooltip("Widest the box gets. On narrow screens it shrinks to fit.")]
        public float maxWidth = 1320f;
        public float boxHeight = 300f;
        public float bottomMargin = 56f;

        [Header("Typing")]
        [Tooltip("How fast the text types itself out. 0 = show each line at once.")]
        public float charactersPerSecond = 50f;

        [Header("Sound (optional)")]
        public AudioClip openSound;
        public AudioClip nextSound;

        [Header("While talking")]
        [Tooltip("Hide the joystick, Run and Interact buttons while the box is open.")]
        public bool hideGameControls = true;

        public bool IsOpen { get; private set; }

        /// <summary>True while the current line is still typing out (NPCs move their mouth then).</summary>
        public bool IsTyping { get; private set; }

        static DialogueScreen s_Instance;

        // built UI
        bool _built;
        GameObject _canvasGO;
        CanvasGroup _group;
        RectTransform _safe;
        RectTransform _box;
        TMP_Text _name;
        TMP_Text _body;
        TMP_Text _hint;
        TMP_Text _counter;
        Button _next;
        Image _nextIconImage;
        Button _close;
        AudioSource _audio;

        // state
        readonly List<string> _lines = new List<string>();
        int _index;
        Action _onClosed;
        Coroutine _typing;
        Coroutine _anim;
        float _inputBlockedUntil;
        bool _closing;
        CanvasGroup _hud;
        float _hudAlpha;
        bool _hudRaycasts, _hudInteractable;

        public static DialogueScreen GetOrCreate()
        {
            if (s_Instance != null) return s_Instance;
            s_Instance = UnityEngine.Object.FindAnyObjectByType<DialogueScreen>();
            if (s_Instance == null) s_Instance = new GameObject("ARCHIV3D Dialogue Screen").AddComponent<DialogueScreen>();
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

        // ------------------------------------------------------------------ public API

        /// <summary>Shows the lines one by one. <paramref name="onClosed"/> runs when the box closes.</summary>
        public void Open(string speaker, string speakerTitle, IList<string> lines, Action onClosed = null)
        {
            Build();
            _lines.Clear();
            if (lines != null)
                foreach (var l in lines)
                    if (!string.IsNullOrWhiteSpace(l)) _lines.Add(l.Trim());
            if (_lines.Count == 0) _lines.Add("...");

            _onClosed = onClosed;
            _index = 0;
            _closing = false;

            string n = string.IsNullOrWhiteSpace(speaker) ? "???" : speaker.ToUpperInvariant();
            _name.text = string.IsNullOrWhiteSpace(speakerTitle)
                ? n
                : n + "<size=62%><color=#FFF6E0AA>   " + speakerTitle.ToUpperInvariant() + "</color></size>";

            EnsureEventSystem();
            FitWidth();
            HideHud(true);
            _canvasGO.SetActive(true);
            IsOpen = true;
            _inputBlockedUntil = Time.unscaledTime + 0.25f;   // ignore the tap that opened us
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (_audio != null && openSound != null) _audio.PlayOneShot(openSound);
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Fade(true));
            ShowLine();
        }

        /// <summary>Finishes the line that is typing, or goes to the next line, or closes after the last one.</summary>
        public void Advance()
        {
            if (!IsOpen || _closing) return;
            if (IsTyping) { FinishTyping(); return; }
            if (_index < _lines.Count - 1)
            {
                _index++;
                if (_audio != null && nextSound != null) _audio.PlayOneShot(nextSound);
                ShowLine();
            }
            else Close();
        }

        public void Close()
        {
            if (!IsOpen || _closing) return;
            _closing = true;
            if (_typing != null) StopCoroutine(_typing);
            IsTyping = false;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Fade(false));
        }

        void FinishClose()
        {
            IsOpen = false;
            _closing = false;
            _canvasGO.SetActive(false);
            HideHud(false);
            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }

        // ------------------------------------------------------------------ lines

        void ShowLine()
        {
            if (_typing != null) StopCoroutine(_typing);
            bool last = _index >= _lines.Count - 1;

            _body.text = _lines[_index];
            _counter.text = (_index + 1) + " / " + _lines.Count;
            _nextIconImage.sprite = last && closeIcon != null ? closeIcon : nextIcon;
            _nextIconImage.color = nextIconColor;
            var nextLabel = _next.GetComponentInChildren<TMP_Text>(true);
            if (nextLabel != null) nextLabel.text = last ? "X" : ">";

            bool mobile = Application.isMobilePlatform;
            _hint.text = last
                ? (mobile ? "Tap to close" : "Click or press Space to close")
                : (mobile ? "Tap to continue" : "Click or press Space to continue");

            if (charactersPerSecond <= 0f || !isActiveAndEnabled)
            {
                _body.maxVisibleCharacters = 99999;
                IsTyping = false;
                return;
            }
            _typing = StartCoroutine(TypeLine());
        }

        IEnumerator TypeLine()
        {
            IsTyping = true;
            _body.maxVisibleCharacters = 0;
            _body.ForceMeshUpdate();
            int total = _body.textInfo.characterCount;
            float shown = 0f;
            while (shown < total)
            {
                shown += Time.unscaledDeltaTime * charactersPerSecond;
                _body.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            _body.maxVisibleCharacters = 99999;
            IsTyping = false;
            _typing = null;
        }

        void FinishTyping()
        {
            if (_typing != null) StopCoroutine(_typing);
            _typing = null;
            _body.maxVisibleCharacters = 99999;
            IsTyping = false;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen || _closing) return;

            // Gentle bob on the Next button once the line is fully shown
            float s = IsTyping ? 1f : 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f);
            _next.transform.parent.localScale = new Vector3(s, s, 1f);

            if (Time.unscaledTime < _inputBlockedUntil) return;
            if (KeyPressed(KeyCode.Escape)) { Close(); return; }
            if (KeyPressed(KeyCode.Space) || KeyPressed(KeyCode.Return) || KeyPressed(KeyCode.E) || KeyPressed(KeyCode.RightArrow))
                Advance();
        }

        void OnTap()
        {
            if (Time.unscaledTime < _inputBlockedUntil) return;
            Advance();
        }

        static bool KeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                switch (key)
                {
                    case KeyCode.Escape: if (kb.escapeKey.wasPressedThisFrame) return true; break;
                    case KeyCode.Space: if (kb.spaceKey.wasPressedThisFrame) return true; break;
                    case KeyCode.Return: if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) return true; break;
                    case KeyCode.E: if (kb.eKey.wasPressedThisFrame) return true; break;
                    case KeyCode.RightArrow: if (kb.rightArrowKey.wasPressedThisFrame) return true; break;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(key)) return true;
#endif
            return false;
        }

        // ------------------------------------------------------------------ open / close animation

        IEnumerator Fade(bool opening)
        {
            float dur = opening ? 0.2f : 0.14f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float x = Mathf.Clamp01(t / dur);
                float e = opening ? 1f - (1f - x) * (1f - x) : x * x;
                _group.alpha = opening ? e : 1f - e;
                float sc = opening ? Mathf.Lerp(0.94f, 1f, e) : Mathf.Lerp(1f, 0.97f, e);
                _box.localScale = new Vector3(sc, sc, 1f);
                _box.anchoredPosition = new Vector2(0, bottomMargin + (opening ? (1f - e) * -18f : e * -10f));
                yield return null;
            }
            _group.alpha = opening ? 1f : 0f;
            _box.localScale = Vector3.one;
            _box.anchoredPosition = new Vector2(0, bottomMargin);
            _anim = null;
            if (!opening) FinishClose();
        }

        // ------------------------------------------------------------------ game controls

        void HideHud(bool hide)
        {
            if (!hideGameControls) return;
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

        // ------------------------------------------------------------------ building

        void FitWidth()
        {
            // The canvas scales to a 1080-unit-tall screen, so this is the safe area's width in canvas units.
            float available = maxWidth;
            if (Screen.height > 0)
                available = Screen.safeArea.width * 1080f / Screen.height - 2f * 90f;
            float w = Mathf.Clamp(available, 600f, maxWidth);
            _box.sizeDelta = new Vector2(w, boxHeight);
        }

        void Build()
        {
            if (_built) return;
            _built = true;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            _canvasGO = new GameObject("ARCHIV3D Dialogue Canvas", typeof(RectTransform));
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

            // Whole screen catches taps: tap anywhere to continue
            var catcher = NewImage("Tap Catcher", _canvasGO.transform, null, new Color(0, 0, 0, 0), true);
            Stretch(catcher.rectTransform, 0, 0, 0, 0);
            var catchBtn = catcher.gameObject.AddComponent<Button>();
            catchBtn.transition = Selectable.Transition.None;
            catchBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            catchBtn.onClick.AddListener(OnTap);

            // Soft dark gradient at the bottom so the text always reads
            var shade = NewImage("Shade", _canvasGO.transform, shadeSprite, shadeSprite != null ? new Color(1, 1, 1, 0.92f) : new Color(0.03f, 0.02f, 0.02f, 0.55f), false);
            shade.type = Image.Type.Simple;
            shade.preserveAspect = false;
            var srt = shade.rectTransform;
            srt.anchorMin = new Vector2(0, 0);
            srt.anchorMax = new Vector2(1, 0);
            srt.pivot = new Vector2(0.5f, 0);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = new Vector2(0, 560);

            _safe = NewRect("Safe Area", _canvasGO.transform);
            Stretch(_safe, 0, 0, 0, 0);
            _safe.gameObject.AddComponent<SafeAreaFitter>();

            // The box
            var boxImg = NewImage("Dialogue Box", _safe, panelSprite, panelColor, false);
            _box = boxImg.rectTransform;
            Place(_box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, bottomMargin), new Vector2(maxWidth, boxHeight));

            var border = NewImage("Border", _box, borderSprite, borderSprite != null ? borderColor : new Color(0, 0, 0, 0), false);
            Stretch(border.rectTransform, 0, 0, 0, 0);

            // Speaker name on a pill, sitting on the box's top-left edge
            var pill = NewImage("Name Tag", _box, pillSprite, pillColor, false);
            var prt = pill.rectTransform;
            Place(prt, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(46, 2), new Vector2(260, 64));
            var h = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(32, 32, 4, 2);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            var fit = pill.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            _name =NewText("Name", prt, nameFont, 34, nameColor, TextAlignmentOptions.Center);
            _name.characterSpacing = 4;
            if (nameFontMaterial != null) _name.fontSharedMaterial = nameFontMaterial;

            // What they say
            _body = NewText("Text", _box, bodyFont, 36, textColor, TextAlignmentOptions.TopLeft);
            Stretch(_body.rectTransform, 64, 82, 64, 58);
            _body.enableAutoSizing = true;
            _body.fontSizeMin = 24;
            _body.fontSizeMax = 36;
            _body.lineSpacing = 6;

            // Bottom row: hint on the left, page count + Next on the right
            _hint = NewText("Hint", _box, bodyFont, 22, new Color(textColor.r, textColor.g, textColor.b, 0.55f), TextAlignmentOptions.MidlineLeft);
            Place(_hint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(64, 44), new Vector2(700, 40));

            _counter = NewText("Count", _box, labelFont != null ? labelFont : bodyFont, 24, new Color(nameColor.r, nameColor.g, nameColor.b, 0.8f), TextAlignmentOptions.MidlineRight);
            Place(_counter.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-144, 52), new Vector2(200, 40));
            _counter.characterSpacing = 2;

            var nextHolder = NewRect("Next", _box);
            Place(nextHolder, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-80, 54), new Vector2(96, 96));
            _next = RoundButton("Next Button", nextHolder, nextButtonSprite, new Color(0.94f, 0.75f, 0.33f, 1f), nextIcon, nextIconColor, ">", 96, Advance, out _nextIconImage);

            var closeHolder = NewRect("Close", _box);
            Place(closeHolder, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-34, -2), new Vector2(72, 72));
            _close = RoundButton("Close Button", closeHolder, closeButtonSprite, new Color(0.1f, 0.08f, 0.06f, 0.9f), closeIcon, Color.white, "X", 72, Close, out _);
        }

        Button RoundButton(string name, Transform parent, Sprite sprite, Color fallback, Sprite icon, Color iconColor, string fallbackText,
            float size, Action onClick, out Image iconImage)
        {
            var img = NewImage(name, parent, sprite, sprite != null ? Color.white : fallback, true);
            img.preserveAspect = true;
            Stretch(img.rectTransform, 0, 0, 0, 0);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.fadeDuration = 0.05f;
            btn.colors = colors;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.onClick.AddListener(() => onClick());

            iconImage = NewImage("Icon", img.transform, icon, iconColor, false);
            iconImage.preserveAspect = true;
            Place(iconImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.48f, size * 0.48f));
            if (icon == null)
            {
                iconImage.color = new Color(0, 0, 0, 0);
                var t = NewText("Label", img.transform, labelFont, size * 0.42f, iconColor, TextAlignmentOptions.Center);
                t.text = fallbackText;
                Stretch(t.rectTransform, 0, 0, 0, 0);
            }
            var juice = img.gameObject.AddComponent<UIButtonJuice>();
            juice.hoverScale = 1.06f;
            juice.pressedScale = 0.9f;
            return btn;
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
