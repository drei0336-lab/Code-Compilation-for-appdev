using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Add NPC Guide (Filipe)
    /// Sets up the NPCGuide.fbx character (textures, materials, humanoid rig, idle animation),
    /// makes the Filipe prefab, places him in Main Map in front of the player's start, and adds the
    /// Dialogue Screen that shows what he says.
    /// </summary>
    public static class NPCGuideSetup
    {
        public const string Dir = "Assets/ARCHIV3D/NPC/Filipe";
        public const string ModelPath = Dir + "/NPCGuide.fbx";
        public const string TextureDir = Dir + "/Textures";
        public const string MaterialDir = Dir + "/Materials";
        public const string ControllerPath = Dir + "/Filipe Idle.controller";
        public const string PrefabPath = Dir + "/Filipe.prefab";
        public const string IdleClipModel = "Assets/Model/Idle.fbx";
        public const string ObjectName = "Filipe";
        public const float TargetHeight = 1.75f;

        class MatSpec
        {
            public string source;     // material name inside the FBX
            public string baseMap;    // texture file name (no extension) in TextureDir
            public string normalMap;
            public Color color = Color.white;
            public float smoothness;
            public float metallic;
            public bool twoSided;
        }

        // Values come from the avatar's original glTF materials (roughness -> smoothness).
        static readonly MatSpec[] Materials =
        {
            new MatSpec { source = "Wolf3D_Skin", baseMap = "Filipe_Face", smoothness = 0.30f },
            new MatSpec { source = "Wolf3D_Body", baseMap = "Filipe_Body", normalMap = "Filipe_Body_Normal", smoothness = 0.40f },
            new MatSpec { source = "Wolf3D_Eye", baseMap = "Filipe_Eye", smoothness = 0.65f },
            new MatSpec { source = "Wolf3D_Teeth", baseMap = "Filipe_Teeth", smoothness = 0.35f },
            new MatSpec { source = "Wolf3D_Hair", baseMap = "Filipe_Hair", normalMap = "Filipe_Hair_Normal", smoothness = 0.15f, twoSided = true },
            new MatSpec { source = "Wolf3D_Outfit_Top", baseMap = "Filipe_Top", normalMap = "Filipe_Top_Normal", smoothness = 0.14f, metallic = 0.13f, twoSided = true },
            new MatSpec { source = "Wolf3D_Outfit_Bottom", baseMap = "Filipe_Bottom", normalMap = "Filipe_Bottom_Normal", smoothness = 0.25f, twoSided = true },
            new MatSpec { source = "Wolf3D_Outfit_Footwear", baseMap = "Filipe_Shoes", normalMap = "Filipe_Shoes_Normal", smoothness = 0.60f },
            new MatSpec { source = "Wolf3D_Glasses", color = new Color(0.035f, 0.03f, 0.032f, 1f), smoothness = 0.60f, twoSided = true },
        };

        [MenuItem("Tools/ARCHIV3D/Add NPC Guide (Filipe)", false, 60)]
        public static void Add() => Run(false);

        [MenuItem("Tools/ARCHIV3D/Rebuild Filipe (materials, rig, prefab)", false, 61)]
        public static void Rebuild() => Run(true);

        static void Run(bool rebuildPrefab)
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then try again.", "OK");
                return;
            }
            var log = new List<string>();
            MigrateOldAssets(log);   // Pilayro -> Filipe: renames his files inside Unity so every reference keeps working

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find " + ModelPath + ".", "OK");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ArchiveHud.MainMapPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ArchiveHud.MainMapPath, OpenSceneMode.Single);
                if (!scene.IsValid()) return;
            }

            RenameSceneObject(scene, log);
            try
            {
                EditorUtility.DisplayProgressBar("ARCHIV3D", "Textures and materials...", 0.2f);
                ConfigureTextures(log);
                var mats = MakeMaterials(log);

                EditorUtility.DisplayProgressBar("ARCHIV3D", "Character rig...", 0.4f);
                bool human = ConfigureModel(mats, log);

                EditorUtility.DisplayProgressBar("ARCHIV3D", "Idle animation...", 0.6f);
                var controller = MakeController(log);

                EditorUtility.DisplayProgressBar("ARCHIV3D", "Prefab...", 0.75f);
                // Remember where Filipe stands in the scene, so rebuilding the prefab can't move him
                var placed = FindInScene<NPCGuide>(scene);
                bool hadPlaced = placed != null;
                Vector3 placedPos = hadPlaced ? placed.transform.position : Vector3.zero;
                Quaternion placedRot = hadPlaced ? placed.transform.rotation : Quaternion.identity;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null || rebuildPrefab) prefab = BuildPrefab(controller, human, log);
                else FixPrefabText(log);

                placed = FindInScene<NPCGuide>(scene);
                if (hadPlaced && placed != null &&
                    (placed.transform.position != placedPos || placed.transform.rotation != placedRot))
                {
                    Undo.RecordObject(placed.transform, "Keep Filipe's spot");
                    placed.transform.SetPositionAndRotation(placedPos, placedRot);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(placed.transform);
                }

                EditorUtility.DisplayProgressBar("ARCHIV3D", "Placing Filipe in " + scene.name + "...", 0.9f);
                var ds = EnsureDialogueScreen(scene, log);
                var npc = PlaceInScene(scene, prefab, ds, log);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                if (npc != null)
                {
                    Selection.activeGameObject = npc;
                    if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("[ARCHIV3D] Filipe setup:\n- " + string.Join("\n- ", log));
            EditorUtility.DisplayDialog("ARCHIV3D: Filipe", "- " + string.Join("\n- ", log) +
                "\n\nPress Play, walk up to Filipe and tap the hand button (or click him) to talk.", "Nice");
        }

        // ------------------------------------------------------------------ textures + materials

        static Texture2D FindTexture(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == baseName)
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        static void ConfigureTextures(List<string> log)
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool normal = Path.GetFileNameWithoutExtension(path).EndsWith("_Normal");
                bool changed = false;
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                if (!normal && ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; changed = true; }
                if (!normal && !ti.sRGBTexture) { ti.sRGBTexture = true; changed = true; }
                if (ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; changed = true; }   // plenty for a phone
                if (changed) { ti.SaveAndReimport(); n++; }
            }
            log.Add("Textures: " + AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir }).Length + " found" +
                    (n > 0 ? " (" + n + " set to the right type / max 1024 px)" : ""));
        }

        static Dictionary<string, Material> MakeMaterials(List<string> log)
        {
            if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder(Dir, "Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var result = new Dictionary<string, Material>();
            var missing = new List<string>();
            foreach (var s in Materials)
            {
                string path = MaterialDir + "/" + s.source.Replace("Wolf3D_", "Filipe_") + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != shader) mat.shader = shader;

                var baseTex = FindTexture(s.baseMap);
                if (s.baseMap != null && baseTex == null) missing.Add(s.baseMap);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseTex);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", s.color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", s.color);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", s.smoothness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", s.smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", s.metallic);

                var normal = FindTexture(s.normalMap);
                if (s.normalMap != null && normal == null) missing.Add(s.normalMap);
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", normal);
                if (normal != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");

                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", s.twoSided ? 0f : 2f);
                mat.doubleSidedGI = s.twoSided;
                EditorUtility.SetDirty(mat);
                result[s.source] = mat;
            }
            AssetDatabase.SaveAssets();
            log.Add("Materials: " + result.Count + " URP Lit materials in " + MaterialDir +
                    (missing.Count > 0 ? " (missing textures: " + string.Join(", ", missing) + ")" : " with the original textures"));
            return result;
        }

        // ------------------------------------------------------------------ model + animation

        static bool ConfigureModel(Dictionary<string, Material> mats, List<string> log)
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.importBlendShapes = true;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats)
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();

            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            bool ok = avatar != null && avatar.isValid && avatar.isHuman;
            log.Add(ok ? "Rig: Humanoid (the idle animation is retargeted onto him)"
                       : "Rig: Humanoid setup FAILED. Select NPCGuide.fbx > Rig > Configure to check the bone mapping.");
            return ok;
        }

        static AnimationClip FindIdleClip()
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(IdleClipModel))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            foreach (var guid in AssetDatabase.FindAssets("Idle t:AnimationClip"))
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview__") && c.isHumanMotion) return c;
            }
            return null;
        }

        static AnimatorController MakeController(List<string> log)
        {
            var clip = FindIdleClip();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var layers = ctrl.layers;
            var sm = layers[0].stateMachine;
            AnimatorState idle = null;
            foreach (var cs in sm.states)
                if (cs.state.name == "Idle") idle = cs.state;
            if (idle == null) idle = sm.AddState("Idle");
            idle.motion = clip;
            idle.writeDefaultValues = true;
            sm.defaultState = idle;
            layers[0].iKPass = true;          // lets him turn his head to look at the player
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            log.Add(clip != null ? "Idle animation: \"" + Path.GetFileName(IdleClipModel) + "\" (looping) in " + Path.GetFileName(ControllerPath)
                                 : "Idle animation: no idle clip found (looked for " + IdleClipModel + ")");
            return ctrl;
        }

        // ------------------------------------------------------------------ prefab

        static GameObject BuildPrefab(AnimatorController controller, bool human, List<string> log)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = new GameObject(ObjectName);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);

            // Leftovers from Blender: a 1 m sphere at his feet, a camera and a light
            int hidden = 0;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t == inst.transform) continue;
                if (t.name.StartsWith("Icosphere") || t.GetComponent<Camera>() != null || t.GetComponent<Light>() != null)
                {
                    t.gameObject.SetActive(false);
                    hidden++;
                }
            }

            var anim = inst.GetComponent<Animator>();
            if (anim == null) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // Make him face the prefab's forward (blue arrow)
            var l = FindDeep(inst.transform, "LeftUpLeg");
            var r = FindDeep(inst.transform, "RightUpLeg");
            if (l != null && r != null)
            {
                Vector3 right = r.position - l.position;
                right.y = 0f;
                if (right.sqrMagnitude > 1e-6f)
                {
                    Vector3 fwd = Vector3.Cross(right.normalized, Vector3.up);
                    float yaw = Vector3.SignedAngle(fwd, Vector3.forward, Vector3.up);
                    inst.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * inst.transform.rotation;
                }
            }

            // Real-world size (the library's player stands about 1.6 m at eye height)
            float height = MeasureHeight(inst);
            if (height > 0.05f)
                inst.transform.localScale *= TargetHeight / height;

            var life = inst.AddComponent<NPCIdleLife>();
            life.lookAtPlayer = human;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, TargetHeight * 0.5f, 0f);
            col.height = TargetHeight;
            col.radius = 0.32f;

            var it = root.AddComponent<Interactable>();
            it.prompt = "Talk to " + ObjectName;
            it.onInteract = new UnityEngine.Events.UnityEvent();

            var guide = root.AddComponent<NPCGuide>();
            guide.npcName = ObjectName;
            guide.npcTitle = "Library Guide";
            guide.lines = new List<string>(NPCGuide.DefaultLines);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            log.Add("Prefab: " + PrefabPath + " (" + TargetHeight.ToString("0.00") + " m tall, was " + height.ToString("0.00") +
                    (hidden > 0 ? "; hid " + hidden + " Blender leftover(s): Icosphere/camera/light" : "") + ")");
            return prefab;
        }

        static float MeasureHeight(GameObject go)
        {
            bool any = false;
            Bounds b = default;
            foreach (var rend in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!any) { b = rend.bounds; any = true; }
                else b.Encapsulate(rend.bounds);
            }
            float h = any ? b.size.y : 0f;
            if (h < 0.2f || h > 50f)
            {
                var head = FindDeep(go.transform, "HeadTop_End");
                var toe = FindDeep(go.transform, "LeftToe_End");
                if (head != null && toe != null) h = Mathf.Abs(head.position.y - toe.position.y) * 1.04f;
            }
            return h;
        }

        static Transform FindDeep(Transform t, string name)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
                if (c.name == name) return c;
            return null;
        }

        /// <summary>
        /// The first version used TextMeshPro's &lt;alpha&gt; tag with a closing tag it doesn't have, so
        /// "&lt;/alpha&gt;" showed up in the text. Fixes those lines in the prefab without rebuilding it.
        /// </summary>
        static void FixPrefabText(List<string> log)
        {
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var guide = contents.GetComponent<NPCGuide>();
                if (guide == null || guide.lines == null) return;
                int fixedLines = 0;
                for (int i = 0; i < guide.lines.Count; i++)
                {
                    string s = guide.lines[i];
                    if (s == null || !s.Contains("</alpha>")) continue;
                    guide.lines[i] = s.Replace("<alpha=#99>", "<size=85%><color=#FFF6E0AA>").Replace("</alpha>", "</color></size>");
                    fixedLines++;
                }
                if (fixedLines > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                    log.Add("Prefab: fixed " + fixedLines + " line(s) that showed \"</alpha>\"");
                }
                else log.Add("Prefab: " + PrefabPath + " already exists (kept it)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ------------------------------------------------------------------ rename (Pilayro -> Filipe)

        const string OldName = "Pilayro";
        const string OldDir = "Assets/ARCHIV3D/NPC/" + OldName;

        /// <summary>
        /// The guide used to be called Pilayro. Renames his folder and files (Unity keeps every reference,
        /// since assets are linked by ID, not by name) and the name/prompt/lines saved in his prefab.
        /// Does nothing once everything is called Filipe.
        /// </summary>
        static void MigrateOldAssets(List<string> log)
        {
            int files = 0;
            if (AssetDatabase.IsValidFolder(OldDir) && !AssetDatabase.IsValidFolder(Dir))
            {
                string err = AssetDatabase.MoveAsset(OldDir, Dir);
                if (!string.IsNullOrEmpty(err)) { log.Add("Rename: couldn't rename " + OldDir + " (" + err + ")"); return; }
                files++;
            }
            if (!AssetDatabase.IsValidFolder(Dir)) return;

            foreach (var guid in AssetDatabase.FindAssets("", new[] { Dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.Contains(OldName)) continue;
                string err = AssetDatabase.RenameAsset(path, file.Replace(OldName, ObjectName));
                if (string.IsNullOrEmpty(err)) files++;
                else log.Add("Rename: couldn't rename " + path + " (" + err + ")");
            }
            if (files > 0) AssetDatabase.SaveAssets();

            bool prefabText = false;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    prefabText = ReplaceOldName(contents);
                    if (contents.name != ObjectName) { contents.name = ObjectName; prefabText = true; }
                    if (prefabText) PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            if (files > 0 || prefabText)
                log.Add("Renamed " + OldName + " to " + ObjectName + ": " + files + " file(s)/folder" +
                        (prefabText ? " + his name, \"Talk to\" prompt and greeting" : ""));
        }

        /// <summary>Renames the guide in the open scene, including any lines changed only in the scene.</summary>
        static void RenameSceneObject(Scene scene, List<string> log)
        {
            var guide = FindInScene<NPCGuide>(scene);
            if (guide == null) return;
            bool changed = false;
            if (guide.gameObject.name.Contains(OldName))
            {
                Undo.RecordObject(guide.gameObject, "Rename guide");
                guide.gameObject.name = guide.gameObject.name.Replace(OldName, ObjectName);
                PrefabUtility.RecordPrefabInstancePropertyModifications(guide.gameObject);
                changed = true;
            }
            var it = guide.GetComponent<Interactable>();
            if (it != null) Undo.RecordObject(it, "Rename guide");
            Undo.RecordObject(guide, "Rename guide");
            if (ReplaceOldName(guide.gameObject))
            {
                if (it != null) PrefabUtility.RecordPrefabInstancePropertyModifications(it);
                PrefabUtility.RecordPrefabInstancePropertyModifications(guide);
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                log.Add("Scene: renamed the guide in " + scene.name + " to \"" + guide.gameObject.name + "\"");
            }
        }

        static bool ReplaceOldName(GameObject root)
        {
            bool changed = false;
            var g = root.GetComponent<NPCGuide>();
            if (g != null)
            {
                if (g.npcName != null && g.npcName.Contains(OldName)) { g.npcName = g.npcName.Replace(OldName, ObjectName); changed = true; }
                if (g.lines != null)
                    for (int i = 0; i < g.lines.Count; i++)
                        if (g.lines[i] != null && g.lines[i].Contains(OldName)) { g.lines[i] = g.lines[i].Replace(OldName, ObjectName); changed = true; }
            }
            var it = root.GetComponent<Interactable>();
            if (it != null && it.prompt != null && it.prompt.Contains(OldName)) { it.prompt = it.prompt.Replace(OldName, ObjectName); changed = true; }
            return changed;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var c = go.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        // ------------------------------------------------------------------ scene

        static DialogueScreen EnsureDialogueScreen(Scene scene, List<string> log)
        {
            DialogueScreen ds = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                ds = go.GetComponentInChildren<DialogueScreen>(true);
                if (ds != null) break;
            }
            bool created = false;
            if (ds == null)
            {
                var go = new GameObject("ARCHIV3D Dialogue Screen");
                Undo.RegisterCreatedObjectUndo(go, "Create Dialogue Screen");
                SceneManager.MoveGameObjectToScene(go, scene);
                ds = go.AddComponent<DialogueScreen>();
                created = true;
            }

            Undo.RecordObject(ds, "Style Dialogue Screen");
            if (ds.nameFont == null) ds.nameFont = ArchiveAssets.PixelFont;
            if (ds.nameFontMaterial == null) ds.nameFontMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArchiveAssets.FontDir + "/Pixelify SDF - Shadow.mat");
            if (ds.bodyFont == null) ds.bodyFont = ArchiveAssets.RegularFont;
            if (ds.labelFont == null) ds.labelFont = ArchiveAssets.BoldFont;
            if (ds.panelSprite == null) ds.panelSprite = ArchiveAssets.RoundedFill;
            if (ds.borderSprite == null) ds.borderSprite = ArchiveAssets.RoundedBorder;
            if (ds.pillSprite == null) ds.pillSprite = ArchiveAssets.Pill;
            if (ds.shadeSprite == null) ds.shadeSprite = ArchiveAssets.GradientBottom;
            if (ds.nextButtonSprite == null) ds.nextButtonSprite = ArchiveAssets.ButtonGold;
            if (ds.closeButtonSprite == null) ds.closeButtonSprite = ArchiveAssets.ButtonDark;
            if (ds.nextIcon == null) ds.nextIcon = ArchiveAssets.Sprite("icon_arrow_right", Vector4.zero);
            if (ds.closeIcon == null) ds.closeIcon = ArchiveAssets.Sprite("icon_close", Vector4.zero);
            EditorUtility.SetDirty(ds);

            log.Add(created ? "Dialogue: added \"ARCHIV3D Dialogue Screen\" (bottom-center box in the game's walnut + gold style)"
                            : "Dialogue: \"" + ds.name + "\" already in the scene (kept its settings)");
            return ds;
        }

        static GameObject PlaceInScene(Scene scene, GameObject prefab, DialogueScreen ds, List<string> log)
        {
            NPCGuide existing = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                existing = go.GetComponentInChildren<NPCGuide>(true);
                if (existing != null) break;
            }
            if (existing != null)
            {
                Undo.RecordObject(existing, "Link dialogue");
                existing.dialogueScreen = ds;
                PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
                log.Add("Scene: \"" + existing.name + "\" is already in " + scene.name + " (left where it is)");
                return existing.gameObject;
            }

            var npc = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(npc, "Add Filipe");
            npc.name = ObjectName;

            string how = FindSpot(scene, npc, out Vector3 pos, out Quaternion rot);
            npc.transform.SetPositionAndRotation(pos, rot);

            var guide = npc.GetComponent<NPCGuide>();
            guide.dialogueScreen = ds;
            PrefabUtility.RecordPrefabInstancePropertyModifications(guide);

            log.Add("Scene: Filipe placed " + how + " at (" + pos.x.ToString("0.0") + ", " + pos.y.ToString("0.00") + ", " + pos.z.ToString("0.0") +
                    "). Move him with the Move tool if you'd like him somewhere else.");
            return npc;
        }

        /// <summary>A clear spot on the floor a few steps in front of where the player starts, facing the player.</summary>
        static string FindSpot(Scene scene, GameObject npc, out Vector3 pos, out Quaternion rot)
        {
            Physics.SyncTransforms();
            FirstPersonController player = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                player = go.GetComponentInChildren<FirstPersonController>(true);
                if (player != null) break;
            }
            if (player == null)
            {
                pos = Vector3.zero;
                rot = Quaternion.identity;
                return "at the world origin (no FirstPersonController found)";
            }

            Transform p = player.transform;
            Vector3 fwd = p.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            var cam = player.GetComponentInChildren<Camera>(true);
            Vector3 eye = cam != null ? cam.transform.position : p.position + Vector3.up * 1.6f;

            float floorY = p.position.y;
            if (Physics.Raycast(p.position + Vector3.up * 1.0f, Vector3.down, out var pf, 5f, ~0, QueryTriggerInteraction.Ignore))
                floorY = pf.point.y;

            var props = new List<Bounds>();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == npc) continue;
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (!mr.enabled || mr.transform.IsChildOf(p)) continue;
                    var b = mr.bounds;
                    if (b.size.x > 6f || b.size.z > 6f || b.size.y > 6f) continue;   // floors, walls, big merged meshes
                    props.Add(b);
                }
            }

            float[] dists = { 3.4f, 4.2f, 2.8f, 5.0f, 5.8f, 2.3f };
            float[] sides = { 0.8f, -0.8f, 0f, 1.5f, -1.5f };
            foreach (float d in dists)
            {
                foreach (float s in sides)
                {
                    Vector3 c = p.position + fwd * d + right * s;
                    c.y = floorY;
                    if (!Physics.Raycast(c + Vector3.up * 1.2f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(hit.point.y - floorY) > 0.4f) continue;   // stairs, tables, a hole
                    Vector3 feet = hit.point;

                    bool blocked = false;
                    foreach (var h in Physics.OverlapCapsule(feet + Vector3.up * 0.45f, feet + Vector3.up * 1.55f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h == hit.collider || h.transform.IsChildOf(p) || h.transform.IsChildOf(npc.transform)) continue;
                        blocked = true;
                        break;
                    }
                    if (blocked) continue;

                    var box = new Bounds(feet + Vector3.up * 0.95f, new Vector3(0.9f, 1.5f, 0.9f));
                    foreach (var b in props)
                        if (b.Intersects(box)) { blocked = true; break; }
                    if (blocked) continue;

                    if (Physics.Linecast(eye, feet + Vector3.up * 1.4f, out var los, ~0, QueryTriggerInteraction.Ignore) &&
                        !los.transform.IsChildOf(npc.transform) && !los.transform.IsChildOf(p)) continue;

                    pos = feet;
                    Vector3 face = p.position - feet; face.y = 0f;
                    rot = face.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(face.normalized, Vector3.up) : Quaternion.identity;
                    return d.ToString("0.0") + " m in front of the player's start";
                }
            }

            pos = p.position + fwd * 3f;
            pos.y = floorY;
            rot = Quaternion.LookRotation(-fwd, Vector3.up);
            return "3 m in front of the player's start (no fully clear spot found; check he isn't inside furniture)";
        }
    }
}
