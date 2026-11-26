using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARCHIV3D.EditorTools
{
    /// <summary>Tools > ARCHIV3D menu: restyle the mobile controls and build the title screen.</summary>
    [InitializeOnLoad]
    public static class ArchiveSetup
    {
        const int PanoramaSize = 2048;
        static string OfferedKey => "ARCHIV3D.SetupOffered.v1." + Application.dataPath;

        static ArchiveSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += Offer;
        }

        static void Offer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorPrefs.GetBool(OfferedKey, false)) return;
            EditorPrefs.SetBool(OfferedKey, true);
            if (EditorUtility.DisplayDialog("ARCHIV3D: new controls and title screen",
                    "Restyle the joystick, Interact and Run buttons in Main Map, and build the title screen in TITLESCREEN now?\n\n" +
                    "This also takes a 360° picture of the library for the title screen background, " +
                    "and makes TITLESCREEN the first scene of the game.\n\n" +
                    "You can do this later from Tools > ARCHIV3D.", "Do it now", "Later"))
                DoEverything();
        }

        [MenuItem("Tools/ARCHIV3D/Restyle Controls + Build Title Screen", false, 0)]
        public static void DoEverything()
        {
            if (!Ready()) return;
            var report = new List<string>();

            var map = OpenScene(ArchiveHud.MainMapPath);
            if (map.IsValid())
            {
                report.Add(ArchiveHud.Restyle(map));
                EditorSceneManager.SaveScene(map);
                report.Add(PanoramaCapture.CaptureFromPlayer(map, PanoramaSize, out string msg) ? msg : "Panorama: " + msg);
            }
            else report.Add("Couldn't open " + ArchiveHud.MainMapPath + ".");

            var titleScene = OpenScene(TitleScreenBuilder.ScenePath);
            if (titleScene.IsValid())
            {
                report.Add(TitleScreenBuilder.Build(titleScene));
                EditorSceneManager.SaveScene(titleScene);
                report.Add(UpdateBuildScenes());
            }
            else report.Add("Couldn't open " + TitleScreenBuilder.ScenePath + ".");

            EditorUtility.DisplayDialog("ARCHIV3D", string.Join("\n\n", report) +
                "\n\nPress Play in TITLESCREEN to try it.", "Nice");
        }

        [MenuItem("Tools/ARCHIV3D/Restyle Mobile Controls (Main Map)", false, 20)]
        public static void RestyleOnly()
        {
            if (!Ready()) return;
            var map = OpenScene(ArchiveHud.MainMapPath);
            if (!map.IsValid()) return;
            string result = ArchiveHud.Restyle(map);
            EditorSceneManager.SaveScene(map);
            EditorUtility.DisplayDialog("ARCHIV3D", result, "OK");
        }

        [MenuItem("Tools/ARCHIV3D/Rebuild Title Screen (TITLESCREEN)", false, 21)]
        public static void RebuildTitleOnly()
        {
            if (!Ready()) return;
            if (!EditorUtility.DisplayDialog("ARCHIV3D", "This replaces the \"" + TitleScreenBuilder.RootName +
                    "\" object in TITLESCREEN, so changes you made to it by hand will be lost.", "Rebuild", "Cancel")) return;
            var scene = OpenScene(TitleScreenBuilder.ScenePath);
            if (!scene.IsValid()) return;
            string result = TitleScreenBuilder.Build(scene);
            EditorSceneManager.SaveScene(scene);
            EditorUtility.DisplayDialog("ARCHIV3D", result + "\n\n" + UpdateBuildScenes(), "OK");
        }

        [MenuItem("Tools/ARCHIV3D/Retake Title Background From Player Spot", false, 40)]
        public static void RecaptureFromPlayer()
        {
            if (!Ready()) return;
            var map = OpenScene(ArchiveHud.MainMapPath);
            if (!map.IsValid()) return;
            PanoramaCapture.CaptureFromPlayer(map, PanoramaSize, out string msg);
            EditorUtility.DisplayDialog("ARCHIV3D", msg, "OK");
        }

        [MenuItem("Tools/ARCHIV3D/Retake Title Background From Scene View", false, 41)]
        public static void RecaptureFromSceneView()
        {
            if (!Ready()) return;
            bool ok = PanoramaCapture.CaptureFromSceneView(PanoramaSize, out string msg);
            if (ok)
                msg += "\n\nTip: to make the title screen start facing this way, set TITLESCREEN > Main Camera > " +
                       "Panorama Camera > Start Yaw to " + PanoramaCapture.LoadYaw().ToString("0") + ".";
            EditorUtility.DisplayDialog("ARCHIV3D", msg, "OK");
        }

        static bool Ready()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then try again.", "OK");
                return false;
            }
            return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        static Scene OpenScene(string path)
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == path) return active;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) return default;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        /// <summary>TITLESCREEN first, then Main Map, keeping any other scenes already in the list.</summary>
        static string UpdateBuildScenes()
        {
            var list = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(TitleScreenBuilder.ScenePath, true),
                new EditorBuildSettingsScene(ArchiveHud.MainMapPath, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != TitleScreenBuilder.ScenePath && s.path != ArchiveHud.MainMapPath) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
            return "Scene list: TITLESCREEN is now the first scene, then Main Map.";
        }
    }
}
