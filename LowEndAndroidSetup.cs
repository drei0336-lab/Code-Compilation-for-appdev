using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Optimize for Low-End Android.
    /// Sets the project up so the game runs on cheap Android phones:
    ///  - a "Mobile Low" quality level + render pipeline asset that weak phones switch to automatically,
    ///  - lighter settings on the normal "Mobile" level (no HDR, shorter tree distance),
    ///  - Android player settings (32-bit phones, frame pacing, frame timing stats, mesh data stripping),
    ///  - big scene textures capped at 1024 px on Android,
    ///  - terrain drawn with GPU instancing,
    ///  - a Graphics option (Auto / Low / Medium / High) on the title screen's Settings panel.
    /// Safe to run again; it only changes what still needs changing.
    /// </summary>
    public static class LowEndAndroidSetup
    {
        const string SettingsFolder = "Assets/Settings";
        const string LowAssetPath = SettingsFolder + "/Mobile_Low_RPAsset.asset";
        const int AndroidTextureCap = 1024;

        static readonly List<string> s_Log = new List<string>();

        [MenuItem("Tools/ARCHIV3D/Optimize for Low-End Android", false, 41)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            s_Log.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("ARCHIV3D", "Render pipeline assets...", 0.05f);
                var normal = NormalPipelineAsset();
                var low = LowPipelineAsset(normal);
                EditorUtility.DisplayProgressBar("ARCHIV3D", "Quality levels...", 0.15f);
                QualityLevels(low);
                EditorUtility.DisplayProgressBar("ARCHIV3D", "Player settings...", 0.2f);
                Player();
                Textures();
                EditorUtility.DisplayProgressBar("ARCHIV3D", "Scenes...", 0.9f);
                Scenes();
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                s_Log.Add("STOPPED by an error: " + e.Message);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string summary = "Optimize for Low-End Android\n\n" + string.Join("\n", s_Log.Select(l => "• " + l));
            Debug.Log("[ARCHIV3D] " + summary);
            File.WriteAllText("Logs/ARCHIV3D-LowEndSetup.txt", summary);
            EditorUtility.DisplayDialog("ARCHIV3D", summary.Length > 1500 ? summary.Substring(0, 1500) + "\n..." : summary, "OK");
        }

        static void Log(string s) => s_Log.Add(s);

        // ------------------------------------------------------------------ pipeline assets

        /// <summary>The URP asset the "Mobile" quality level uses (Medium / High phones).</summary>
        static UniversalRenderPipelineAsset NormalPipelineAsset()
        {
            int i = Array.FindIndex(QualitySettings.names, n => n == DevicePerformance.NormalQualityLevel);
            var rp = (i >= 0 ? QualitySettings.GetRenderPipelineAssetAt(i) : null) as UniversalRenderPipelineAsset;
            if (rp == null) rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SettingsFolder + "/Mobile_RPAsset.asset");
            if (rp == null) throw new Exception("Couldn't find the Mobile URP asset (Assets/Settings/Mobile_RPAsset.asset).");

            var so = new SerializedObject(rp);
            int n = 0;
            // HDR stays on here: Main Map's bloom and tonemapping need it. Shadows: only what's near the player.
            n += Set(so, "m_MSAA", 1);
            n += Set(so, "m_RequireOpaqueTexture", false);
            n += Set(so, "m_SoftShadowsSupported", false);
            n += Set(so, "m_ShadowDistance", 25f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
            Log(n > 0 ? $"{rp.name} (Medium/High phones): shadows drawn to 25 m instead of 50 m, MSAA off, no opaque texture."
                      : $"{rp.name}: already set.");
            return rp;
        }

        /// <summary>A copy of the Mobile asset tuned for weak phones.</summary>
        static UniversalRenderPipelineAsset LowPipelineAsset(UniversalRenderPipelineAsset normal)
        {
            var low = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LowAssetPath);
            bool created = false;
            if (low == null)
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(normal), LowAssetPath))
                    throw new Exception("Couldn't create " + LowAssetPath);
                low = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(LowAssetPath);
                created = true;
            }

            var so = new SerializedObject(low);
            Set(so, "m_RenderScale", 0.7f);
            Set(so, "m_SupportsHDR", false);
            Set(so, "m_MSAA", 1);
            Set(so, "m_RequireDepthTexture", false);
            Set(so, "m_RequireOpaqueTexture", false);
            Set(so, "m_MainLightShadowsSupported", false);
            Set(so, "m_MainLightShadowmapResolution", 512);
            Set(so, "m_ShadowDistance", 20f);
            Set(so, "m_AdditionalLightsRenderingMode", 2);     // per vertex
            Set(so, "m_AdditionalLightsPerObjectLimit", 2);
            Set(so, "m_AdditionalLightShadowsSupported", false);
            Set(so, "m_SoftShadowsSupported", false);
            Set(so, "m_EnableLODCrossFade", false);
            Set(so, "m_SupportsLightCookies", false);
            Set(so, "m_ColorGradingLutSize", 16);
            Set(so, "m_SupportDataDrivenLensFlare", false);
            Set(so, "m_SupportScreenSpaceLensFlare", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(low);
            Log((created ? "Created " : "Updated ") + LowAssetPath + " (weak phones): 70% resolution, no shadows, " +
                "HDR/MSAA off, extra lights per-vertex.");
            return low;
        }

        // ------------------------------------------------------------------ quality levels

        static void QualityLevels(UniversalRenderPipelineAsset lowAsset)
        {
            var qsObj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset").FirstOrDefault();
            if (qsObj == null) { Log("Couldn't open Quality settings; skipped."); return; }
            var so = new SerializedObject(qsObj);
            var levels = so.FindProperty("m_QualitySettings");

            int mobile = FindLevel(levels, DevicePerformance.NormalQualityLevel);
            if (mobile < 0) { Log("No \"Mobile\" quality level found; skipped quality levels."); return; }

            int low = FindLevel(levels, DevicePerformance.LowQualityLevel);
            bool created = false;
            if (low < 0)
            {
                // Duplicate Mobile, then move the copy to the end so the existing level numbers
                // (and the per-platform defaults that point at them) don't shift.
                levels.InsertArrayElementAtIndex(mobile);
                levels.MoveArrayElement(mobile, levels.arraySize - 1);
                low = levels.arraySize - 1;
                created = true;
            }

            var l = levels.GetArrayElementAtIndex(low);
            Set(l, "name", DevicePerformance.LowQualityLevel);
            Set(l, "pixelLightCount", 1);
            Set(l, "shadows", 0);                  // disable
            Set(l, "shadowDistance", 20f);
            Set(l, "globalTextureMipmapLimit", 1); // textures at half size: about a quarter of the memory
            Set(l, "anisotropicTextures", 0);
            Set(l, "antiAliasing", 0);
            Set(l, "softParticles", false);
            Set(l, "realtimeReflectionProbes", false);
            Set(l, "lodBias", 0.6f);
            Set(l, "enableLODCrossFade", false);
            Set(l, "particleRaycastBudget", 64);
            Set(l, "skinWeights", 2);
            Set(l, "vSyncCount", 0);
            SetRef(l, "customRenderPipeline", lowAsset);
            TerrainLimits(l, treeDistance: 150, billboardStart: 30, maxMeshTrees: 15, detailDistance: 35,
                          detailDensity: 0.4f, pixelError: 12, basemapDistance: 150);

            // Normal Mobile level: keep the look, but stop drawing trees kilometres away.
            var m = levels.GetArrayElementAtIndex(mobile);
            TerrainLimits(m, treeDistance: 400, billboardStart: 60, maxMeshTrees: 40, detailDistance: 60,
                          detailDensity: 1f, pixelError: 6, basemapDistance: 400);
            Set(m, "realtimeReflectionProbes", false);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(qsObj);
            AssetDatabase.SaveAssets();
            Log((created ? "Added" : "Updated") + " quality level \"Mobile Low\": half-size textures, no shadows, " +
                "trees to 150 m, lower LOD distance. Weak phones switch to it automatically.");
            Log("Quality level \"Mobile\": terrain trees drawn to 400 m instead of 5 km.");
        }

        static int FindLevel(SerializedProperty levels, string name)
        {
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name")?.stringValue == name) return i;
            return -1;
        }

        static void TerrainLimits(SerializedProperty level, float treeDistance, float billboardStart, int maxMeshTrees,
                                  float detailDistance, float detailDensity, float pixelError, float basemapDistance)
        {
            // TerrainQualityOverrides flags: PixelError 1, BasemapDistance 2, DetailDensity 4, DetailDistance 8,
            // TreeDistance 16, BillboardStart 32, FadeLength 64, MaxTrees 128.
            Set(level, "terrainQualityOverrides", 1 | 2 | 4 | 8 | 16 | 32 | 128);
            Set(level, "terrainPixelError", pixelError);
            Set(level, "terrainBasemapDistance", basemapDistance);
            Set(level, "terrainDetailDensityScale", detailDensity);
            Set(level, "terrainDetailDistance", detailDistance);
            Set(level, "terrainTreeDistance", treeDistance);
            Set(level, "terrainBillboardStart", billboardStart);
            Set(level, "terrainMaxTrees", maxMeshTrees);
        }

        // ------------------------------------------------------------------ player settings

        static void Player()
        {
            var changes = new List<string>();

            if (!PlayerSettings.Android.optimizedFramePacing)
            {
                PlayerSettings.Android.optimizedFramePacing = true;
                changes.Add("optimized frame pacing on");
            }
            if (!PlayerSettings.enableFrameTimingStats)
            {
                PlayerSettings.enableFrameTimingStats = true;
                changes.Add("frame timing stats on (lets the game measure GPU time)");
            }
            if (!PlayerSettings.stripUnusedMeshComponents)
            {
                PlayerSettings.stripUnusedMeshComponents = true;
                changes.Add("optimize mesh data on");
            }

            // Many cheap phones still run 32-bit Android; a 64-bit-only app can't even be installed on them.
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP &&
                Enum.TryParse("ARMv7", out AndroidArchitecture v7) &&
                (PlayerSettings.Android.targetArchitectures & v7) == 0)
            {
                try
                {
                    PlayerSettings.Android.targetArchitectures |= v7;
                    if ((PlayerSettings.Android.targetArchitectures & v7) != 0)
                        changes.Add("ARMv7 added next to ARM64, so 32-bit phones can install the game");
                }
                catch (Exception e) { changes.Add("couldn't add ARMv7 (" + e.Message + ")"); }
            }

            Log(changes.Count > 0 ? "Android Player Settings: " + string.Join(", ", changes) + "." : "Android Player Settings: already set.");
        }

        // ------------------------------------------------------------------ textures

        static void Textures()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var paths = AssetDatabase.GetDependencies(scenes, true)
                .Where(p => p.StartsWith("Assets/") && !p.StartsWith("Assets/ARCHIV3D/"))
                .Distinct().ToList();

            var todo = new List<TextureImporter>();
            foreach (var p in paths)
            {
                if (!(AssetImporter.GetAtPath(p) is TextureImporter imp)) continue;
                if (imp.textureType == TextureImporterType.Sprite || imp.textureType == TextureImporterType.GUI) continue;
                if (imp.textureType == TextureImporterType.Lightmap || imp.textureType == TextureImporterType.Cookie) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(p);
                if (tex == null || Mathf.Max(tex.width, tex.height) <= AndroidTextureCap) continue;
                var s = imp.GetPlatformTextureSettings("Android");
                if (s.overridden && s.maxTextureSize <= AndroidTextureCap) continue;
                todo.Add(imp);
            }

            if (todo.Count == 0) { Log($"Textures: none above {AndroidTextureCap} px on Android."); return; }

            int done = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var imp in todo)
                {
                    var s = imp.GetPlatformTextureSettings("Android");
                    if (!s.overridden)
                    {
                        var d = imp.GetDefaultPlatformTextureSettings();
                        s.format = TextureImporterFormat.Automatic;
                        s.textureCompression = d.textureCompression;
                        s.compressionQuality = d.compressionQuality;
                        s.crunchedCompression = d.crunchedCompression;
                        s.resizeAlgorithm = d.resizeAlgorithm;
                    }
                    s.overridden = true;
                    s.maxTextureSize = AndroidTextureCap;
                    imp.SetPlatformTextureSettings(s);
                    EditorUtility.SetDirty(imp);
                    imp.SaveAndReimport();
                    done++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            Log($"Textures: {done} scene textures capped at {AndroidTextureCap} px on Android (PC keeps full size). " +
                "This roughly quarters their memory on the phone.");
        }

        // ------------------------------------------------------------------ scenes

        static void Scenes()
        {
            string active = SceneManager.GetActiveScene().path;
            foreach (var bs in EditorBuildSettings.scenes.Where(s => s.enabled))
            {
                var scene = SceneManager.GetSceneByPath(bs.path);
                bool opened = false;
                if (!scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(bs.path, OpenSceneMode.Additive);
                    opened = true;
                }

                bool changed = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Terrain>(true))
                    {
                        if (t.drawInstanced) continue;
                        Undo.RecordObject(t, "Terrain instancing");
                        t.drawInstanced = true;
                        EditorUtility.SetDirty(t);
                        changed = true;
                        Log($"{scene.name}: terrain \"{t.name}\" now drawn with GPU instancing (fewer draw calls).");
                    }

                    foreach (var title in root.GetComponentsInChildren<TitleScreen>(true))
                        if (AddGraphicsRow(title)) { changed = true; Log($"{scene.name}: added Graphics (Auto / Low / Medium / High) to the Settings panel."); }
                }

                if (changed) EditorSceneManager.SaveScene(scene);
                if (opened && scene.path != active) EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Adds a Graphics slider to an already-built title screen by copying its Look sensitivity row.</summary>
        static bool AddGraphicsRow(TitleScreen title)
        {
            if (title.graphicsSlider != null || title.lookSlider == null) return false;
            var lookRow = title.lookSlider.transform.parent;
            if (lookRow == null || lookRow.parent == null) return false;

            var row = Object.Instantiate(lookRow.gameObject, lookRow.parent);
            row.name = "Graphics Row";
            row.transform.SetSiblingIndex(lookRow.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(row, "Graphics row");

            var slider = row.GetComponentInChildren<Slider>(true);
            slider.onValueChanged.RemoveAllListeners();
            slider.wholeNumbers = true;
            slider.minValue = 0;
            slider.maxValue = 3;
            slider.SetValueWithoutNotify(0);

            TMP_Text label = null, value = null;
            foreach (var t in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.name == "Label") label = t;
                else if (t.name == "Value") value = t;
            }
            if (label != null) label.text = "Graphics";
            if (value != null)
            {
                value.text = "Auto";
                TitleScreenBuilder.WidenValue(value);
            }

            // Make room: the hint line under the rows explains Auto.
            var card = lookRow.parent.parent;
            var hint = card != null ? card.Find("Hint") : null;
            if (hint != null && hint.TryGetComponent<TMP_Text>(out var hintText))
            {
                hintText.text = TitleScreenBuilder.GraphicsHint;
                var rt = (RectTransform)hint;
                rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 940), rt.sizeDelta.y);
            }

            Undo.RecordObject(title, "Graphics row");
            title.graphicsSlider = slider;
            title.graphicsValue = value;
            EditorUtility.SetDirty(title);
            return true;
        }

        // ------------------------------------------------------------------ SerializedProperty helpers

        static int Set(SerializedObject so, string name, bool v) => SetNum(so.FindProperty(name), name, v ? 1 : 0);
        static int Set(SerializedObject so, string name, double v) => SetNum(so.FindProperty(name), name, v);
        static int Set(SerializedProperty parent, string name, bool v) => SetNum(parent.FindPropertyRelative(name), name, v ? 1 : 0);
        static int Set(SerializedProperty parent, string name, double v) => SetNum(parent.FindPropertyRelative(name), name, v);

        static int Set(SerializedProperty parent, string name, string v)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null || p.propertyType != SerializedPropertyType.String) { Missing(name); return 0; }
            if (p.stringValue == v) return 0;
            p.stringValue = v;
            return 1;
        }

        static int SetRef(SerializedProperty parent, string name, Object v)
        {
            var p = parent.FindPropertyRelative(name);
            if (p == null || p.propertyType != SerializedPropertyType.ObjectReference) { Missing(name); return 0; }
            if (p.objectReferenceValue == v) return 0;
            p.objectReferenceValue = v;
            return 1;
        }

        /// <summary>Sets an int / enum / float / bool setting, whichever type Unity stores it as.</summary>
        static int SetNum(SerializedProperty p, string name, double v)
        {
            if (p == null) { Missing(name); return 0; }
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                    if (p.intValue == (int)v) return 0;
                    p.intValue = (int)v;
                    return 1;
                case SerializedPropertyType.Float:
                    if (Mathf.Approximately(p.floatValue, (float)v)) return 0;
                    p.floatValue = (float)v;
                    return 1;
                case SerializedPropertyType.Boolean:
                    if (p.boolValue == (v != 0)) return 0;
                    p.boolValue = v != 0;
                    return 1;
                default:
                    Debug.LogWarning($"[ARCHIV3D] Unexpected type {p.propertyType} for {name}; left as is.");
                    return 0;
            }
        }

        static void Missing(string name) =>
            Debug.LogWarning("[ARCHIV3D] Setting not found (Unity version difference?), left as is: " + name);
    }
}
