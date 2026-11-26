using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARCHIV3D
{
    /// <summary>
    /// An invisible wall (a solid Box Collider the player can't walk through). When the player comes close,
    /// a red barrier grid fades in on it with "NOT ACCESSIBLE", and a short message shows at the top of the
    /// screen. Used on the library's front door so players stay inside the library.
    /// The collider sits on the "Ignore Raycast" layer, so it never blocks tapping or looking at things.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [AddComponentMenu("ARCHIV3D/Library Boundary (Invisible Wall)")]
    public class LibraryBoundary : MonoBehaviour
    {
        [Header("Message")]
        public string title = "NOT ACCESSIBLE";
        public string message = "You can't leave the library.";

        [Header("When it shows (meters from the wall)")]
        [Tooltip("The barrier starts to fade in at this distance.")]
        public float showDistance = 3.5f;
        [Tooltip("The barrier is fully visible at this distance and closer.")]
        public float fullDistance = 1.2f;
        [Tooltip("The message at the top of the screen shows at this distance and closer.")]
        public float messageDistance = 2.2f;

        [Header("Barrier look")]
        [Tooltip("The red grid (uses the ARCHIV3D/Barrier Grid material). Hidden until the player comes close.")]
        public Renderer gridRenderer;
        [Range(0f, 1f)] public float gridMaxAlpha = 1f;
        [Tooltip("The \"NOT ACCESSIBLE\" text on the barrier.")]
        public TMP_Text wallLabel;

        [Header("Screen message (optional)")]
        public bool showScreenMessage = true;
        public TMP_FontAsset titleFont;
        public Material titleFontMaterial;
        public TMP_FontAsset messageFont;
        public Sprite pillSprite;

        BoxCollider _box;
        Transform _player;
        float _alpha, _hudAlpha;
        MaterialPropertyBlock _mpb;

        static CanvasGroup s_Hud;
        static TMP_Text s_HudTitle, s_HudMessage;
        static LibraryBoundary s_HudOwner;

        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int FocusPosId = Shader.PropertyToID("_FocusPos");
        static readonly int FocusRadiusId = Shader.PropertyToID("_FocusRadius");

        void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _mpb = new MaterialPropertyBlock();
            Apply(0f, Vector3.zero);
        }

        void OnDisable()
        {
            _alpha = 0f;
            _hudAlpha = 0f;
            Apply(0f, Vector3.zero);
            if (s_HudOwner == this && s_Hud != null) s_Hud.alpha = 0f;
        }

        void Update()
        {
            if (_player == null)
            {
                var fpc = FindAnyObjectByType<FirstPersonController>();
                if (fpc != null) _player = fpc.transform;
                else if (Camera.main != null) _player = Camera.main.transform;
                if (_player == null) return;
            }

            // Distance from the player to the nearest point of the wall
            Vector3 p = _player.position + Vector3.up * 0.5f;
            float d = Vector3.Distance(p, _box.ClosestPoint(p));
            float dt = Time.unscaledDeltaTime;

            float target = 1f - Mathf.InverseLerp(fullDistance, showDistance, d);
            _alpha = Mathf.MoveTowards(_alpha, target, dt * 3f);
            Vector3 focus = Camera.main != null ? Camera.main.transform.position : p;
            Apply(_alpha, focus);

            if (showScreenMessage)
            {
                bool show = d <= messageDistance && !PauseMenu.IsPaused;
                _hudAlpha = Mathf.MoveTowards(_hudAlpha, show ? 1f : 0f, dt * 5f);
                if (_hudAlpha > 0f || s_HudOwner == this) ShowHud(_hudAlpha);
            }
        }

        void Apply(float a, Vector3 focus)
        {
            bool on = a > 0.005f;
            if (gridRenderer != null)
            {
                if (gridRenderer.enabled != on) gridRenderer.enabled = on;
                if (on)
                {
                    gridRenderer.GetPropertyBlock(_mpb);
                    _mpb.SetFloat(AlphaId, a * gridMaxAlpha);
                    _mpb.SetVector(FocusPosId, focus);
                    _mpb.SetFloat(FocusRadiusId, 2.5f);
                    gridRenderer.SetPropertyBlock(_mpb);
                }
            }
            if (wallLabel != null)
            {
                if (wallLabel.enabled != on) wallLabel.enabled = on;
                wallLabel.alpha = a;
            }
        }

        // ------------------------------------------------------------------ screen message

        void ShowHud(float a)
        {
            if (s_Hud == null) BuildHud();
            if (s_Hud == null) return;
            if (a > 0f) s_HudOwner = this;
            if (s_HudOwner != this) return;
            s_HudTitle.text = title;
            s_HudMessage.text = message;
            s_Hud.alpha = a;
            if (a <= 0f) s_HudOwner = null;
        }

        void BuildHud()
        {
            var go = new GameObject("ARCHIV3D Boundary Message", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;   // above the HUD, below books / dialogue
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            s_Hud = go.AddComponent<CanvasGroup>();
            s_Hud.alpha = 0f;
            s_Hud.blocksRaycasts = false;
            s_Hud.interactable = false;

            var safe = new GameObject("Safe Area", typeof(RectTransform));
            safe.transform.SetParent(go.transform, false);
            var srt = (RectTransform)safe.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            safe.AddComponent<SafeAreaFitter>();

            var pill = new GameObject("Message", typeof(RectTransform));
            pill.transform.SetParent(safe.transform, false);
            var prt = (RectTransform)pill.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.anchoredPosition = new Vector2(0f, -120f);
            var img = pill.AddComponent<Image>();
            img.sprite = pillSprite;
            img.type = pillSprite != null && pillSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = new Color(16f / 255f, 13f / 255f, 11f / 255f, 0.9f);
            img.raycastTarget = false;
            var v = pill.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(48, 48, 14, 16);
            v.spacing = 2f;
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            var fit = pill.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            s_HudTitle = NewText("Title", pill.transform, titleFont, 34f, new Color32(255, 112, 96, 255));
            s_HudTitle.characterSpacing = 4f;
            if (titleFontMaterial != null) s_HudTitle.fontSharedMaterial = titleFontMaterial;
            s_HudMessage = NewText("Text", pill.transform, messageFont, 24f, new Color32(255, 246, 224, 230));
        }

        static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }
    }
}
