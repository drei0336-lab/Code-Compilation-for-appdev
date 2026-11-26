using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Builds the in-game pause menu in Main Map, in the same format as the title screen (it uses the title screen
    /// builder's own buttons, panels and setting rows): the logo with a "GAME PAUSED" ribbon and a splash line,
    /// RESUME / HOW TO PLAY / SETTINGS / MAIN MENU, the How to Play and Settings panels, the version and the
    /// player's name at the bottom, and a fade + loading bar back to the title screen.
    /// Also puts a pause button at the top-right of the game controls.
    /// </summary>
    public static class PauseMenuBuilder
    {
        public const string RootName = "ARCHIV3D Pause Menu";
        public const string ButtonName = "Pause Button";

        static readonly string[] Splashes =
        {
            "Take a breather!",
            "Bookmark your spot!",
            "The books will wait!",
            "Stretch break!",
            "Shh... paused!",
            "Back in a sec!",
            "Drink some water!",
            "Rest your eyes!",
            "We'll be right here!",
            "Paused, not finished!",
        };

        const string HowToPlayText = TitleScreenBuilder.HowToPlayText + "\n" +
            "<color=#F0BE54><b>Bookmark</b></color>   Tap the ribbon on a book page; find it under Bookmarks on the shelf.\n" +
            "<color=#F0BE54><b>Pause</b></color>   The pause button at the top right.  <alpha=#99>(Esc)<alpha=#FF>";

        [MenuItem("Tools/ARCHIV3D/Build Pause Menu (Main Map)", false, 22)]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then try again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ArchiveHud.MainMapPath)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ArchiveHud.MainMapPath) == null)
                {
                    EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find " + ArchiveHud.MainMapPath + ".", "OK");
                    return;
                }
                scene = EditorSceneManager.OpenScene(ArchiveHud.MainMapPath, OpenSceneMode.Single);
            }

            string result = Build(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("ARCHIV3D", result + "\n\nPress Play, then the pause button (top right) or Esc.", "OK");
        }

        public static string Build(Scene scene)
        {
            TitleScreenBuilder.LoadStyle();
            var pixel = TitleScreenBuilder.PixelFont;
            var regular = TitleScreenBuilder.RegularFont;
            var bold = TitleScreenBuilder.BoldFont;
            var shadow = TitleScreenBuilder.ShadowMat;

            // Remove a previous build so this can be run again
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == RootName) UIBuild.Destroy(root);

            // ---------------------------------------------------------------- canvas (above the game controls)
            var canvasRT = UIBuild.Rect(RootName, null);
            SceneManager.MoveGameObjectToScene(canvasRT.gameObject, scene);
            var canvas = canvasRT.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 420;   // over the controls; books (450), the name screen (460) and computers (500) go over it
            var scaler = canvasRT.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            canvasRT.gameObject.AddComponent<GraphicRaycaster>();
            var pause = canvasRT.gameObject.AddComponent<PauseMenu>();

            // ---------------------------------------------------------------- the pause screen (title screen layout)
            var menuRT = UIBuild.Rect("Menu", canvasRT);
            UIBuild.Stretch(menuRT);
            var menuGroup = menuRT.gameObject.AddComponent<CanvasGroup>();

            // The game, frozen behind, darkened like the title screen's panorama
            var dim = UIBuild.Image("Dim", menuRT, null, new Color(0.03f, 0.02f, 0.01f, 0.5f), true);
            UIBuild.Stretch(dim.rectTransform);
            var vignette = UIBuild.Image("Vignette", menuRT, ArchiveAssets.Vignette, Color.white);
            vignette.preserveAspect = false;
            UIBuild.Stretch(vignette.rectTransform);
            var shade = UIBuild.Image("Bottom Shade", menuRT, ArchiveAssets.GradientBottom, ArchiveAssets.WithAlpha(Color.white, 0.9f));
            shade.preserveAspect = false;
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

            var safe = UIBuild.Rect("Safe Area", menuRT);
            UIBuild.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Logo, ribbon, splash
            var logo = UIBuild.Image("Logo", safe, ArchiveAssets.Logo, Color.white);
            UIBuild.Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(1150, 228));

            var subtitle = UIBuild.Image("Subtitle", logo.transform, ArchiveAssets.Pill, ArchiveAssets.WithAlpha(ArchiveAssets.Ink, 0.84f));
            UIBuild.Place(subtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, 2), new Vector2(620, 62));
            var subText = UIBuild.Text("Text", subtitle.transform, "GAME PAUSED", pixel, 32, ArchiveAssets.GoldLight, TextAlignmentOptions.Center);
            UIBuild.Stretch(subText.rectTransform, 10, 4, 10, 0);
            subText.characterSpacing = 10;

            var splash = UIBuild.Text("Splash", logo.transform, Splashes[0], pixel, 42, new Color32(255, 231, 92, 255), TextAlignmentOptions.Center);
            UIBuild.Place(splash.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(40, 20), new Vector2(560, 90));
            splash.rectTransform.localRotation = Quaternion.Euler(0, 0, 16);
            if (shadow != null) splash.fontSharedMaterial = shadow;
            splash.gameObject.AddComponent<SplashText>().splashes = (string[])Splashes.Clone();

            // Buttons: same sizes and order as the title screen
            var menu = UIBuild.Rect("Buttons", safe);
            UIBuild.Place(menu, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, 60), new Vector2(700, 400));
            var v = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 22;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            var resume = TitleScreenBuilder.MenuButton(menu, "Resume Button", "RESUME", true, 108);
            var howTo = TitleScreenBuilder.MenuButton(menu, "How To Play Button", "HOW TO PLAY", false, 92);
            var row = UIBuild.Rect("Row", menu);
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.minHeight = rowLE.preferredHeight = 92;
            rowLE.flexibleHeight = 0;
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 22;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            var settings = TitleScreenBuilder.MenuButton(row, "Settings Button", "SETTINGS", false, 92);
            var mainMenu = TitleScreenBuilder.MenuButton(row, "Main Menu Button", "MAIN MENU", false, 92);

            // Footer: version (left), who's playing (right)
            var version = UIBuild.Text("Version", safe, "ARCHIV3D v" + Application.version, regular, 26,
                ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.8f), TextAlignmentOptions.BottomLeft);
            UIBuild.Place(version.rectTransform, Vector2.zero, Vector2.zero, new Vector2(32, 22), new Vector2(700, 40));
            var player = UIBuild.Text("Player", safe, "Playing as Student", regular, 26,
                ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.8f), TextAlignmentOptions.BottomRight);
            UIBuild.Place(player.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-32, 22), new Vector2(800, 40));

            // ---------------------------------------------------------------- panels (same as the title screen)
            var howPanel = TitleScreenBuilder.Panel(canvasRT, "How To Play Panel", "HOW TO PLAY", out var howCard, out var howBack);
            var body = UIBuild.Text("Body", howCard, HowToPlayText, regular, 31, ArchiveAssets.Cream, TextAlignmentOptions.TopLeft);
            UIBuild.Stretch(body.rectTransform, 80, 150, 80, 140);
            body.lineSpacing = 18;
            body.enableAutoSizing = true;     // two more lines than the title screen's
            body.fontSizeMin = 22;
            body.fontSizeMax = 31;

            var setPanel = TitleScreenBuilder.Panel(canvasRT, "Settings Panel", "SETTINGS", out var setCard, out var setBack);
            var rows = UIBuild.Rect("Rows", setCard);
            UIBuild.Stretch(rows, 90, 170, 90, 170);
            var rv = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rv.spacing = 46;
            rv.childAlignment = TextAnchor.MiddleCenter;
            rv.childControlWidth = true;
            rv.childControlHeight = true;
            rv.childForceExpandWidth = true;
            rv.childForceExpandHeight = false;
            var volume = TitleScreenBuilder.SettingRow(rows, "Volume", out var volumeValue);
            var look = TitleScreenBuilder.SettingRow(rows, "Look sensitivity", out var lookValue);
            var graphics = TitleScreenBuilder.SettingRow(rows, "Graphics", out var graphicsValue);
            TitleScreenBuilder.WidenValue(graphicsValue);
            var hint = UIBuild.Text("Hint", setCard, TitleScreenBuilder.GraphicsHint, regular, 24,
                ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.55f), TextAlignmentOptions.Center);
            UIBuild.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 132), new Vector2(940, 34));

            // ---------------------------------------------------------------- fade + loading (back to the title screen)
            var fader = UIBuild.Image("Fader", canvasRT, null, Color.black, true);
            UIBuild.Stretch(fader.rectTransform);
            var faderGroup = fader.gameObject.AddComponent<CanvasGroup>();
            faderGroup.alpha = 0f;
            faderGroup.blocksRaycasts = false;

            var loading = UIBuild.Rect("Loading", fader.transform);
            UIBuild.Place(loading, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 160));
            var loadingGroup = loading.gameObject.AddComponent<CanvasGroup>();
            var loadText = UIBuild.Text("Text", loading, "Back to the main menu...", pixel, 40, ArchiveAssets.Cream, TextAlignmentOptions.Center);
            UIBuild.Place(loadText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(700, 60));
            var track = UIBuild.Image("Bar", loading, ArchiveAssets.Pill, ArchiveAssets.WithAlpha(ArchiveAssets.Cream, 0.16f));
            UIBuild.Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(640, 24));
            var fill = UIBuild.Image("Fill", track.transform, ArchiveAssets.Pill, ArchiveAssets.Gold);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            loading.gameObject.SetActive(false);
            fader.gameObject.SetActive(false);

            menuRT.gameObject.SetActive(false);

            // ---------------------------------------------------------------- pause button on the game controls
            var pauseButton = BuildPauseButton(scene, bold, out string buttonNote);

            // ---------------------------------------------------------------- hook up
            pause.mainMenuScene = System.IO.Path.GetFileNameWithoutExtension(TitleScreenBuilder.ScenePath);
            pause.pauseButton = pauseButton;
            pause.menuGroup = menuGroup;
            pause.resumeButton = resume;
            pause.howToPlayButton = howTo;
            pause.settingsButton = settings;
            pause.mainMenuButton = mainMenu;
            pause.howToPlayPanel = howPanel;
            pause.settingsPanel = setPanel;
            pause.backButtons = new[] { howBack, setBack };
            pause.volumeSlider = volume;
            pause.volumeValue = volumeValue;
            pause.lookSlider = look;
            pause.lookValue = lookValue;
            pause.graphicsSlider = graphics;
            pause.graphicsValue = graphicsValue;
            pause.fader = faderGroup;
            pause.loadingGroup = loadingGroup;
            pause.loadingFill = fill.rectTransform;
            pause.versionText = version;
            pause.playerText = player;
            EditorUtility.SetDirty(pause);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = canvasRT.gameObject;
            return "Built the pause menu in " + scene.name + "." + buttonNote;
        }

        /// <summary>A round dark button (like RUN) with a pause sign, at the top-right of the controls' safe area.</summary>
        static Button BuildPauseButton(Scene scene, TMP_FontAsset bold, out string note)
        {
            note = "";
            SprintButtonHandler sprint = null;
            Joystick joystick = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (sprint == null) sprint = root.GetComponentInChildren<SprintButtonHandler>(true);
                if (joystick == null) joystick = root.GetComponentInChildren<Joystick>(true);
            }
            Canvas canvas = null;
            if (sprint != null) canvas = sprint.GetComponentInParent<Canvas>(true);
            if (canvas == null && joystick != null) canvas = joystick.GetComponentInParent<Canvas>(true);
            if (canvas == null)
            {
                note = "\n\nNo game controls Canvas found, so there's no pause button (Esc still works).";
                return null;
            }
            canvas = canvas.rootCanvas;
            Transform parent = canvas.transform.Find("Safe Area");
            if (parent == null) parent = canvas.transform;

            var old = parent.Find(ButtonName);
            if (old != null) UIBuild.Destroy(old.gameObject);

            var img = UIBuild.Image(ButtonName, parent, ArchiveAssets.ButtonDark, Color.white, true);
            UIBuild.Place(img.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-100, -96), new Vector2(132, 132));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.55f, 0.75f, 1f, 1f);
            cb.disabledColor = new Color(1, 1, 1, 0.4f);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };

            // Pause sign: two rounded cream bars
            var icon = UIBuild.Rect("Icon", img.transform);
            UIBuild.Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(46, 48));
            for (int i = 0; i < 2; i++)
            {
                var bar = UIBuild.Image(i == 0 ? "Left Bar" : "Right Bar", icon, ArchiveAssets.Pill, ArchiveAssets.Cream);
                bar.pixelsPerUnitMultiplier = 4.5f;   // small bars, same round ends
                UIBuild.Place(bar.rectTransform, new Vector2(i == 0 ? 0f : 1f, 0.5f), new Vector2(i == 0 ? 0f : 1f, 0.5f),
                    Vector2.zero, new Vector2(15, 48));
            }

            var label = UIBuild.Text("Label", img.transform, "PAUSE", bold, 18, ArchiveAssets.Cream, TextAlignmentOptions.Center);
            label.characterSpacing = 6;
            UIBuild.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 22), new Vector2(120, 26));

            var juice = img.gameObject.AddComponent<UIButtonJuice>();
            juice.hoverScale = 1.04f;
            juice.pressedScale = 0.92f;
            return btn;
        }
    }
}
