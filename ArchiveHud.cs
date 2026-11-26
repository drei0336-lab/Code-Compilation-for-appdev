using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Restyles the Main Map touch controls: joystick bottom-left, Run and Interact bottom-right,
    /// a crosshair in the middle, all kept inside the phone's safe area.
    /// Keeps the existing objects and their wiring (PlayerInteractor, SprintButtonHandler, FixedJoystick).
    /// </summary>
    public static class ArchiveHud
    {
        public const string MainMapPath = "Assets/Scenes/Main Map.unity";

        public static string Restyle(Scene scene)
        {
            var log = new List<string>();

            SprintButtonHandler sprint = null;
            Joystick joystick = null;
            PlayerInteractor interactor = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (sprint == null) sprint = root.GetComponentInChildren<SprintButtonHandler>(true);
                if (joystick == null) joystick = root.GetComponentInChildren<Joystick>(true);
                if (interactor == null) interactor = root.GetComponentInChildren<PlayerInteractor>(true);
            }

            Canvas canvas = null;
            if (sprint != null) canvas = sprint.GetComponentInParent<Canvas>(true);
            if (canvas == null && joystick != null) canvas = joystick.GetComponentInParent<Canvas>(true);
            if (canvas == null) return "Couldn't find the controls Canvas in " + scene.name + ".";
            canvas = canvas.rootCanvas;

            GameObject interact = FindInteractButton(canvas.transform);

            // Safe area container so nothing hides under a notch or rounded corner
            var safe = canvas.transform.Find("Safe Area") as RectTransform;
            if (safe == null)
            {
                safe = UIBuild.Rect("Safe Area", canvas.transform);
                UIBuild.Stretch(safe);
                safe.gameObject.AddComponent<SafeAreaFitter>();
            }

            // Crosshair (shows what Interact will use)
            var crossT = safe.Find("Crosshair");
            Image cross = crossT != null ? crossT.GetComponent<Image>() : UIBuild.Image("Crosshair", safe, ArchiveAssets.Crosshair, Color.white);
            UIBuild.Place(cross.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            cross.transform.SetAsFirstSibling();

            if (joystick != null) { StyleJoystick(joystick, safe); log.Add("joystick"); }
            if (interact != null) { StyleInteract(interact, safe, interactor); log.Add("Interact button"); }
            if (sprint != null) { StyleRun(sprint.gameObject, safe); log.Add("Run button"); }

            if (canvas.GetComponent<GameSettingsApplier>() == null)
                Undo.AddComponent<GameSettingsApplier>(canvas.gameObject);

            // Bookshelf sections were using the computer's prompt text
            int prompts = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var it in root.GetComponentsInChildren<Interactable>(true))
                {
                    if (it.prompt != "Use Computer") continue;
                    if (it.GetComponent<BlackCompuQuiz.BlackCompuComputer>() != null) continue;
                    Undo.RecordObject(it, "Change prompt");
                    it.prompt = "Browse Books";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(it);
                    prompts++;
                }
            }
            if (prompts > 0) log.Add(prompts + " bookshelf prompt(s) set to \"Browse Books\"");

            EditorSceneManager.MarkSceneDirty(scene);
            return "Restyled: " + string.Join(", ", log) + ".";
        }

        static GameObject FindInteractButton(Transform canvas)
        {
            foreach (var b in canvas.GetComponentsInChildren<Button>(true))
            {
                if (b.name.ToLowerInvariant().Contains("interact")) return b.gameObject;
                int n = b.onClick.GetPersistentEventCount();
                for (int i = 0; i < n; i++)
                    if (b.onClick.GetPersistentMethodName(i) == "Interact") return b.gameObject;
            }
            return null;
        }

        static void StyleJoystick(Joystick joystick, RectTransform safe)
        {
            var rt = (RectTransform)joystick.transform;
            Undo.SetTransformParent(rt, safe, "Move joystick");
            Undo.RecordObject(rt, "Style joystick");
            UIBuild.Place(rt, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(235, 235), new Vector2(300, 300));
            rt.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rt);

            var img = joystick.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Style joystick");
                UIBuild.SetImage(img, ArchiveAssets.JoystickBase, Color.white, true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(img);
            }

            var so = new SerializedObject(joystick);
            so.FindProperty("handleRange").floatValue = 0.55f;
            so.FindProperty("deadZone").floatValue = 0.08f;
            var handle = so.FindProperty("handle").objectReferenceValue as RectTransform;
            so.ApplyModifiedProperties();

            if (handle != null)
            {
                Undo.RecordObject(handle, "Style joystick handle");
                handle.sizeDelta = new Vector2(132, 132);
                PrefabUtility.RecordPrefabInstancePropertyModifications(handle);
                var himg = handle.GetComponent<Image>();
                if (himg != null)
                {
                    Undo.RecordObject(himg, "Style joystick handle");
                    UIBuild.SetImage(himg, ArchiveAssets.JoystickKnob, Color.white, false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(himg);
                }
            }
        }

        static void StyleButtonBase(GameObject go, RectTransform safe, Sprite sprite, Vector2 pos, float size, Color pressed)
        {
            var rt = (RectTransform)go.transform;
            Undo.SetTransformParent(rt, safe, "Move button");
            Undo.RecordObject(rt, "Style button");
            UIBuild.Place(rt, new Vector2(1, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            rt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            Undo.RecordObject(img, "Style button");
            UIBuild.SetImage(img, sprite, Color.white, true);

            var btn = go.GetComponent<Button>();
            Undo.RecordObject(btn, "Style button");
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = pressed;
            cb.disabledColor = new Color(1, 1, 1, 0.4f);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        static Image Child(Transform parent, string name, Sprite sprite, Color color, Vector2 pos, Vector2 size)
        {
            var t = parent.Find(name);
            var img = t != null ? t.GetComponent<Image>() : null;
            if (img == null) img = UIBuild.Image(name, parent, sprite, color);
            Undo.RecordObject(img, "Style " + name);
            UIBuild.SetImage(img, sprite, color, false);
            UIBuild.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            return img;
        }

        static UIButtonJuice Juice(GameObject go, float pressed, bool pop)
        {
            var j = go.GetComponent<UIButtonJuice>();
            if (j == null) j = Undo.AddComponent<UIButtonJuice>(go);
            j.hoverScale = 1.03f;
            j.pressedScale = pressed;
            j.popOnEnable = pop;
            return j;
        }

        static void StyleRun(GameObject run, RectTransform safe)
        {
            StyleButtonBase(run, safe, ArchiveAssets.ButtonDark, new Vector2(-210, 210), 190, new Color(0.55f, 0.75f, 1f, 1f));
            Child(run.transform, "Icon", ArchiveAssets.IconRun, Color.white, new Vector2(0, 16), new Vector2(86, 86));

            var label = run.GetComponentInChildren<TMP_Text>(true);
            if (label == null) label = UIBuild.Text("Label", run.transform, "RUN", ArchiveAssets.BoldFont, 26, ArchiveAssets.Cream, TextAlignmentOptions.Center);
            Undo.RecordObject(label, "Style label");
            Undo.RecordObject(label.gameObject, "Style label");
            label.name = "Label";
            UIBuild.SetText(label, "RUN", ArchiveAssets.BoldFont, 25, ArchiveAssets.Cream, TextAlignmentOptions.Center);
            label.characterSpacing = 8;
            UIBuild.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(150, 34));

            Juice(run, 0.92f, false);
        }

        static void StyleInteract(GameObject interact, RectTransform safe, PlayerInteractor interactor)
        {
            StyleButtonBase(interact, safe, ArchiveAssets.ButtonGold, new Vector2(-450, 320), 170, new Color(0.82f, 0.82f, 0.82f, 1f));
            Child(interact.transform, "Icon", ArchiveAssets.IconHand, new Color32(46, 28, 8, 255), new Vector2(0, 2), new Vector2(92, 92));

            // Prompt pill above the button ("Use Computer", "Browse Books", ...)
            var pillT = interact.transform.Find("Prompt");
            Image pill = pillT != null ? pillT.GetComponent<Image>() : UIBuild.Image("Prompt", interact.transform, ArchiveAssets.Pill, ArchiveAssets.WithAlpha(ArchiveAssets.Ink, 0.82f));
            UIBuild.SetImage(pill, ArchiveAssets.Pill, ArchiveAssets.WithAlpha(ArchiveAssets.Ink, 0.82f), false);
            UIBuild.Place(pill.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(220, 54));
            var layout = pill.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = Undo.AddComponent<HorizontalLayoutGroup>(pill.gameObject);
            layout.padding = new RectOffset(28, 28, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var fitter = pill.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = Undo.AddComponent<ContentSizeFitter>(pill.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            TMP_Text label = null;
            foreach (var t in interact.GetComponentsInChildren<TMP_Text>(true)) { label = t; break; }
            if (label == null) label = UIBuild.Text("Label", pill.transform, "Interact", ArchiveAssets.BoldFont, 26, ArchiveAssets.GoldLight, TextAlignmentOptions.Center);
            Undo.SetTransformParent(label.transform, pill.transform, "Move label");
            Undo.RecordObject(label, "Style label");
            Undo.RecordObject(label.gameObject, "Style label");
            label.name = "Label";
            UIBuild.SetText(label, "Interact", ArchiveAssets.BoldFont, 26, ArchiveAssets.GoldLight, TextAlignmentOptions.Center);
            label.characterSpacing = 1;

            Juice(interact, 0.9f, true);

            // Show the button only when the player looks at something they can use, with its prompt
            if (interactor != null)
            {
                var so = new SerializedObject(interactor);
                so.FindProperty("interactButton").objectReferenceValue = interact;
                so.FindProperty("promptText").objectReferenceValue = label;
                so.ApplyModifiedProperties();
            }
        }
    }
}
