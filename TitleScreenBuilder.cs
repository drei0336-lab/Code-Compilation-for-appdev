using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Builds the ARCHIV3D title screen in the TITLESCREEN scene:
    /// a slowly turning panorama of the library, the blocky logo with a bouncing splash line,
    /// Start / How to Play / Settings / Quit, pop-up panels, version + credits, and a fade + loading bar.
    /// </summary>
    public static class TitleScreenBuilder
    {
        public const string ScenePath = "Assets/Scenes/TITLESCREEN.unity";
        public const string RootName = "ARCHIV3D Title UI";
        const string ShadowMatPath = ArchiveAssets.FontDir + "/Pixelify SDF - Shadow.mat";

        static TMP_FontAsset Pixel, Bold, Regular;
        static Material Shadow;

        /// <summary>Loads the title screen's fonts and shadow material (also used by the pause menu builder).</summary>
        internal static void LoadStyle()
        {
            Pixel = ArchiveAssets.PixelFont;
            Bold = ArchiveAssets.BoldFont;
            Regular = ArchiveAssets.RegularFont;
            Shadow = ShadowMaterial(Pixel);
        }

        internal static TMP_FontAsset PixelFont => Pixel;
        internal static TMP_FontAsset BoldFont => Bold;
        internal static TMP_FontAsset RegularFont => Regular;
        internal static Material ShadowMat => Shadow;

        public static string Build(Scene scene)
        {
            LoadStyle();

            // Remove a previous build so this can be run again
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == RootName) UIBuild.Destroy(root);

            SetUpCamera(scene);
            EnsureEventSystem(scene);

            // ---------------------------------------------------------------- canvas
            var canvasRT = UIBuild.Rect(RootName, null);
            SceneManager.MoveGameObjectToScene(canvasRT.gameObject, scene);
            var canvas = canvasRT.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasRT.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            canvasRT.gameObject.AddComponent<GraphicRaycaster>();
            var title = canvasRT.gameObject.AddComponent<TitleScreen>();

            // Background shading over the panorama
            var vignette = UIBuild.Image("Vignette", canvasRT, ArchiveAssets.Vignette, Color.white);
            vignette.preserveAspect = false;
            UIBuild.Stretch(vignette.rectTransform);
            var shade = UIBuild.Image("Bottom Shade", canvasRT, ArchiveAssets.GradientBottom, ArchiveAssets.WithAlpha(Color.white, 0.9f));
            shade.preserveAspect = false;
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero; // darkest at the bottom

            var safe = UIBuild.Rect("Safe Area", canvasRT);
            UIBuild.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // ---------------------------------------------------------------- logo, subtitle, splash
            var logo = UIBuild.Image("Logo", safe, ArchiveAssets.Logo, Color.white);
            UIBuild.Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(1150, 228));

            var subtitle = UIBuild.Image("Subtitle", logo.transform, ArchiveAssets.Pill, ArchiveAssets.WithAlpha(ArchiveAssets.Ink, 0.84f));
            UIBuild.Place(subtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, 2), new Vector2(620, 62));
            var subText = UIBuild.Text("Text", subtitle.transform, "A 3D VIRTUAL LIBRARY", Pixel, 32, ArchiveAssets.GoldLight, TextAlignmentOptions.Center);
            UIBuild.Stretch(subText.rectTransform, 10, 4, 10, 0);
            subText.characterSpacing = 10;

            var splash = UIBuild.Text("Splash", logo.transform, "Shh! Library rules!", Pixel, 42, new Color32(255, 231, 92, 255), TextAlignmentOptions.Center);
            UIBuild.Place(splash.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(40, 20), new Vector2(560, 90));
            splash.rectTransform.localRotation = Quaternion.Euler(0, 0, 16);
            if (Shadow != null) splash.fontSharedMaterial = Shadow;
            splash.gameObject.AddComponent<SplashText>();

            // ---------------------------------------------------------------- menu buttons
            var menu = UIBuild.Rect("Menu", safe);
            UIBuild.Place(menu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, 60), new Vector2(700, 400));
            var v = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 22;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var play = MenuButton(menu, "Start Button", "START EXPLORING", true, 108);
            var howTo = MenuButton(menu, "How To Play Button", "HOW TO PLAY", false, 92);
            var row = UIBuild.Rect("Row", menu);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.minHeight = rowLE.preferredHeight = 92;
            rowLE.flexibleHeight = 0; // don't let the row soak up the menu's spare height
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 22;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            var settings = MenuButton(row, "Settings Button", "SETTINGS", false, 92);
            var quit = MenuButton(row, "Quit Button", "QUIT", false, 92);

            // ---------------------------------------------------------------- footer
            var version = UIBuild.Text("Version", safe, "ARCHIV3D v" + Application.version, Regular, 26,
                ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.8f), TextAlignmentOptions.BottomLeft);
            UIBuild.Place(version.rectTransform, Vector2.zero, Vector2.zero, new Vector2(32, 22), new Vector2(700, 40));
            var credits = UIBuild.Text("Credits", safe, "A thesis project · 2026", Regular, 26,
                ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.8f), TextAlignmentOptions.BottomRight);
            UIBuild.Place(credits.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-32, 22), new Vector2(800, 40));

            // ---------------------------------------------------------------- panels
            var howPanel = Panel(canvasRT, "How To Play Panel", "HOW TO PLAY", out var howCard, out var howBack);
            var body = UIBuild.Text("Body", howCard, HowToPlayText, Regular, 31, ArchiveAssets.Cream, TextAlignmentOptions.TopLeft);
            UIBuild.Stretch(body.rectTransform, 80, 150, 80, 140);
            body.lineSpacing = 18;

            var setPanel = Panel(canvasRT, "Settings Panel", "SETTINGS", out var setCard, out var setBack);
            var rows = UIBuild.Rect("Rows", setCard);
            UIBuild.Stretch(rows, 90, 170, 90, 170);
            var rv = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.spacing = 46;
            rv.childAlignment = TextAnchor.MiddleCenter;
            rv.childControlWidth = true;
            rv.childControlHeight = true;
            rv.childForceExpandWidth = true;
            rv.childForceExpandHeight = false;
            var volume = SettingRow(rows, "Volume", out var volumeValue);
            var look = SettingRow(rows, "Look sensitivity", out var lookValue);
            var graphics = SettingRow(rows, "Graphics", out var graphicsValue);
            WidenValue(graphicsValue);
            var hint = UIBuild.Text("Hint", setCard, GraphicsHint, Regular, 24,
                ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.55f), TextAlignmentOptions.Center);
            UIBuild.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 132), new Vector2(940, 34));

            // ---------------------------------------------------------------- fade + loading
            var fader = UIBuild.Image("Fader", canvasRT, null, Color.black, true);
            UIBuild.Stretch(fader.rectTransform);
            var faderGroup = fader.gameObject.AddComponent<CanvasGroup>();
            faderGroup.alpha = 0f;
            faderGroup.blocksRaycasts = false;

            var loading = UIBuild.Rect("Loading", fader.transform);
            UIBuild.Place(loading, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 160));
            var loadingGroup = loading.gameObject.AddComponent<CanvasGroup>();
            var loadText = UIBuild.Text("Text", loading, "Opening the library...", Pixel, 40, ArchiveAssets.Cream, TextAlignmentOptions.Center);
            UIBuild.Place(loadText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(700, 60));
            var track = UIBuild.Image("Bar", loading, ArchiveAssets.Pill, ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.16f));
            UIBuild.Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(640, 24));
            var fill = UIBuild.Image("Fill", track.transform, ArchiveAssets.Pill, ArchiveAssets.Gold);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            loading.gameObject.SetActive(false);

            // ---------------------------------------------------------------- hook up
            title.gameScene = "Main Map";
            title.playButton = play;
            title.howToPlayButton = howTo;
            title.settingsButton = settings;
            title.quitButton = quit;
            title.howToPlayPanel = howPanel;
            title.settingsPanel = setPanel;
            title.backButtons = new[] { howBack, setBack };
            title.volumeSlider = volume;
            title.volumeValue = volumeValue;
            title.lookSlider = look;
            title.lookValue = lookValue;
            title.graphicsSlider = graphics;
            title.graphicsValue = graphicsValue;
            title.fader = faderGroup;
            title.loadingGroup = loadingGroup;
            title.loadingFill = fill.rectTransform;
            title.versionText = version;
            EditorUtility.SetDirty(title);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = canvasRT.gameObject;
            return "Built the title screen in " + scene.name + ".";
        }

        internal const string HowToPlayText =
            "<color=#F0BE54><b>Move</b></color>   Drag the joystick on the left.  <alpha=#99>(WASD on a computer)<alpha=#FF>\n" +
            "<color=#F0BE54><b>Look</b></color>   Drag on the right side of the screen.  <alpha=#99>(mouse)<alpha=#FF>\n" +
            "<color=#F0BE54><b>Run</b></color>   Hold the RUN button.  <alpha=#99>(Shift)<alpha=#FF>\n" +
            "<color=#F0BE54><b>Use</b></color>   Look at a computer or bookshelf until the hand button appears, then tap it.\n" +
            "<color=#F0BE54><b>Quiz</b></color>   The black computers have quizzes. Try to get every answer right!";

        // ------------------------------------------------------------------ pieces

        static void SetUpCamera(Scene scene)
        {
            Camera cam = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cam = root.GetComponentInChildren<Camera>(true);
                if (cam != null) break;
            }
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(go, "Create camera");
                SceneManager.MoveGameObjectToScene(go, scene);
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            Undo.RecordObject(cam, "Title camera");
            Undo.RecordObject(cam.transform, "Title camera");
            cam.transform.position = Vector3.zero;
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.1f;

            var mat = PanoramaCapture.LoadMaterial();
            if (mat != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.skybox = mat;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(26, 19, 13, 255);
            }

            var pano = cam.GetComponent<PanoramaCamera>();
            if (pano == null) pano = Undo.AddComponent<PanoramaCamera>(cam.gameObject);
            pano.startYaw = PanoramaCapture.LoadYaw();
            pano.enabled = mat != null;
            EditorUtility.SetDirty(pano);
        }

        static void EnsureEventSystem(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<EventSystem>(true) != null) return;

            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static Material ShadowMaterial(TMP_FontAsset font)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(ShadowMatPath);
            if (existing != null) return existing;
            if (font == null || font.material == null) return null;

            var m = new Material(font.material) { name = "Pixelify SDF - Shadow" };
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0.05f, 0.03f, 0.01f, 0.85f));
            m.SetFloat("_UnderlayOffsetX", 0.9f);
            m.SetFloat("_UnderlayOffsetY", -0.9f);
            m.SetFloat("_UnderlayDilate", 0.15f);
            m.SetFloat("_UnderlaySoftness", 0.05f);
            AssetDatabase.CreateAsset(m, ShadowMatPath);
            AssetDatabase.SaveAssets();
            return m;
        }

        internal static Button MenuButton(Transform parent, string name, string label, bool primary, float height)
        {
            var rt = UIBuild.Rect(name, parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = height;
            le.flexibleWidth = 1;

            Color fillColor = primary ? ArchiveAssets.Gold : ArchiveAssets.WithAlpha(ArchiveAssets.Walnut, 0.88f);
            var fill = rt.gameObject.AddComponent<Image>();
            UIBuild.SetImage(fill, ArchiveAssets.RoundedFill, fillColor, true);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = fill;
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            Color borderColor = primary ? ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.9f) : ArchiveAssets.WithAlpha(ArchiveAssets.Gold, 0.55f);
            var border = UIBuild.Image("Border", rt, ArchiveAssets.RoundedBorder, borderColor);
            UIBuild.Stretch(border.rectTransform);

            var text = UIBuild.Text("Label", rt, label, Pixel, primary ? 46 : 38,
                primary ? (Color)new Color32(46, 28, 8, 255) : ArchiveAssets.Cream, TextAlignmentOptions.Center);
            UIBuild.Stretch(text.rectTransform, 12, 4, 12, 0);
            text.characterSpacing = 4;
            if (!primary && Shadow != null) text.fontSharedMaterial = Shadow;

            var juice = rt.gameObject.AddComponent<UIButtonJuice>();
            juice.fill = fill;
            juice.fillNormal = fillColor;
            juice.fillHover = primary ? ArchiveAssets.GoldLight : (Color)new Color32(70, 50, 32, 240);
            juice.border = border;
            juice.borderNormal = borderColor;
            juice.borderHover = primary ? Color.white : ArchiveAssets.Gold;
            juice.hoverScale = 1.03f;
            juice.pressedScale = 0.96f;
            return btn;
        }

        internal static CanvasGroup Panel(Transform parent, string name, string heading, out RectTransform card, out Button back)
        {
            var root = UIBuild.Rect(name, parent);
            UIBuild.Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var dim = UIBuild.Image("Dim", root, null, new Color(0, 0, 0, 0.62f), true);   // child 0
            UIBuild.Stretch(dim.rectTransform);

            var cardImg = UIBuild.Image("Card", root, ArchiveAssets.RoundedFill, ArchiveAssets.WithAlpha(ArchiveAssets.Walnut, 0.97f), true); // child 1
            card = cardImg.rectTransform;
            UIBuild.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 700));
            var border = UIBuild.Image("Border", card, ArchiveAssets.RoundedBorder, ArchiveAssets.WithAlpha(ArchiveAssets.Gold, 0.9f));
            UIBuild.Stretch(border.rectTransform);

            var title = UIBuild.Text("Title", card, heading, Pixel, 54, ArchiveAssets.GoldLight, TextAlignmentOptions.Center);
            UIBuild.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(900, 80));
            title.characterSpacing = 8;
            if (Shadow != null) title.fontSharedMaterial = Shadow;
            var line = UIBuild.Image("Line", card, null, ArchiveAssets.WithAlpha(ArchiveAssets.Gold, 0.35f));
            UIBuild.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -124), new Vector2(920, 3));

            var backHolder = UIBuild.Rect("Back", card);
            UIBuild.Place(backHolder, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(360, 88));
            back = MenuButton(backHolder, "Back Button", "BACK", true, 88);
            UIBuild.Stretch((RectTransform)back.transform);

            root.gameObject.SetActive(false);
            return group;
        }

        public const string GraphicsHint = "Saved on this device.  Graphics: Auto picks what suits the phone.";

        /// <summary>The Graphics row shows "Auto (Medium)", which needs a wider value column.</summary>
        public static void WidenValue(TMP_Text value)
        {
            var le = value.GetComponent<LayoutElement>();
            if (le != null) le.minWidth = le.preferredWidth = 210;
            value.enableAutoSizing = true;
            value.fontSizeMin = 20;
            value.fontSizeMax = value.fontSize;
        }

        internal static Slider SettingRow(Transform parent, string label, out TMP_Text value)
        {
            var row = UIBuild.Rect(label + " Row", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 64;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 30;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            var name = UIBuild.Text("Label", row, label, Bold, 34, ArchiveAssets.Cream, TextAlignmentOptions.MidlineLeft);
            var nle = name.gameObject.AddComponent<LayoutElement>();
            nle.minWidth = nle.preferredWidth = 300;

            var sliderRT = UIBuild.Rect("Slider", row);
            var sle = sliderRT.gameObject.AddComponent<LayoutElement>();
            sle.flexibleWidth = 1;
            sle.minWidth = 300;
            var slider = sliderRT.gameObject.AddComponent<Slider>();

            var track = UIBuild.Image("Track", sliderRT, ArchiveAssets.Pill, ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.18f), true);
            track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(0, 18);

            var fillArea = UIBuild.Rect("Fill Area", sliderRT);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 18);
            var fill = UIBuild.Image("Fill", fillArea, ArchiveAssets.Pill, ArchiveAssets.Gold);
            UIBuild.Stretch(fill.rectTransform);

            var handleArea = UIBuild.Rect("Handle Slide Area", sliderRT);
            UIBuild.Stretch(handleArea);
            var handle = UIBuild.Image("Handle", handleArea, ArchiveAssets.JoystickKnob, Color.white, true);
            handle.rectTransform.sizeDelta = new Vector2(58, 0);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };

            value = UIBuild.Text("Value", row, "100%", Bold, 32, ArchiveAssets.GoldLight, TextAlignmentOptions.MidlineRight);
            var vle = value.gameObject.AddComponent<LayoutElement>();
            vle.minWidth = vle.preferredWidth = 120;
            return slider;
        }
    }
}
