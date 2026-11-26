using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Set Up First-Person View (arms only)
    ///
    /// The player's camera sits inside the character's body, so looking down showed the inside of the torso,
    /// the legs and the shirt. This tool makes the camera see only the character's arms and hands:
    ///  - it makes an "arms only" copy of each body part that has arms in it (skin and sleeves), keeping only
    ///    the triangles that move with the arm bones, and adds it as an "FP Arms - ..." renderer that follows
    ///    the same skeleton, so the arms still swing with the walk / run / idle animations;
    ///  - a First-Person Body component hides the full body from the player's camera only (it still casts the
    ///    player's shadow); the Scene tab and previews show the whole character as usual;
    ///  - the camera's near clip plane goes from 0.3 m to 0.05 m, so the arms right next to the camera are drawn
    ///    whole instead of being cut open into thin flaps.
    /// Safe to run again (it keeps the arms it already made; delete the "FP Arms - ..." objects to rebuild them). The arm meshes are saved in Assets/ARCHIV3D/Player/First Person.
    /// To undo it, delete the "FP Arms - ..." objects and the First-Person Body component.
    /// </summary>
    public static class FirstPersonArmsSetup
    {
        const string MeshDir = "Assets/ARCHIV3D/Player/First Person";
        public const string ArmsPrefix = "FP Arms - ";

        // A triangle is kept when its corners move with the arm bones at least this much (0..1).
        // 0.5 cuts the arm at the shoulder joint, where the sleeve seam usually is.
        const float KeepWeight = 0.5f;

        // Camera near clip plane for the first-person view (meters).
        const float NearClip = 0.05f;

        [MenuItem("Tools/ARCHIV3D/Set Up First-Person View (arms only)", false, 40)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then try again.", "OK");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != ArchiveHud.MainMapPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(ArchiveHud.MainMapPath, OpenSceneMode.Single);
                if (!scene.IsValid()) return;
            }

            var player = FindInScene<FirstPersonController>(scene);
            if (player == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find the player (FirstPersonController) in " + scene.name + ".", "OK");
                return;
            }

            var animator = player.GetComponentInChildren<Animator>(true);
            Transform leftArm = null, rightArm = null;
            if (animator != null && animator.avatar != null && animator.avatar.isHuman)
            {
                leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
            if (leftArm == null) leftArm = FindBone(player.transform, "LeftArm", "LeftUpperArm", "upperarm_l", "L_UpperArm");
            if (rightArm == null) rightArm = FindBone(player.transform, "RightArm", "RightUpperArm", "upperarm_r", "R_UpperArm");
            if (leftArm == null || rightArm == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find the character's arm bones, so the arms can't be separated.", "OK");
                return;
            }

            var log = new List<string>();
            var restoreReadable = new List<ModelImporter>();
            try
            {
                // Arms from an earlier run are kept (delete the "FP Arms - ..." objects to rebuild them)
                var existingArms = new Dictionary<string, SkinnedMeshRenderer>();
                var bodyParts = new List<SkinnedMeshRenderer>();
                foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr == null) continue;
                    if (smr.name.StartsWith(ArmsPrefix))
                    {
                        if (smr.sharedMesh != null) existingArms[smr.name.Substring(ArmsPrefix.Length)] = smr;
                        else Undo.DestroyObjectImmediate(smr.gameObject);
                    }
                    else if (smr.sharedMesh != null) bodyParts.Add(smr);
                }
                if (bodyParts.Count == 0)
                {
                    EditorUtility.DisplayDialog("ARCHIV3D", "The player has no skinned body parts to change.", "OK");
                    return;
                }

                var toBuild = new List<SkinnedMeshRenderer>();
                foreach (var smr in bodyParts)
                    if (!existingArms.ContainsKey(smr.name) && HasArmBones(smr, leftArm, rightArm)) toBuild.Add(smr);

                if (toBuild.Count > 0)
                {
                    EditorUtility.DisplayProgressBar("ARCHIV3D", "Reading the character's body...", 0.2f);
                    MakeReadable(toBuild, restoreReadable, log);
                    if (!AssetDatabase.IsValidFolder(MeshDir)) CreateFolders(MeshDir);
                }

                var armsRenderers = new List<Renderer>();
                int part = 0;
                foreach (var smr in bodyParts)
                {
                    part++;
                    EditorUtility.DisplayProgressBar("ARCHIV3D", "Arms: " + smr.name, 0.3f + 0.6f * part / bodyParts.Count);

                    if (existingArms.TryGetValue(smr.name, out var kept))
                    {
                        armsRenderers.Add(kept);
                        log.Add(smr.name + ": arms already made (" + kept.name + ")");
                    }
                    else if (toBuild.Contains(smr))
                    {
                        var armsMesh = BuildArmsMesh(smr, leftArm, rightArm, out int keptTris, out int totalTris, out string problem);
                        if (problem != null) log.Add(smr.name + ": " + problem);
                        if (armsMesh != null)
                        {
                            armsMesh = SaveMesh(armsMesh, MeshDir + "/" + Safe(smr.name) + " Arms.asset");
                            armsRenderers.Add(AddArmsRenderer(smr, armsMesh));
                            log.Add(smr.name + ": arms kept (" + keptTris + " of " + totalTris + " triangles)");
                        }
                        else if (problem == null) log.Add(smr.name + ": hidden from the player's camera (no arms in it)");
                    }
                    else log.Add(smr.name + ": hidden from the player's camera (no arms in it)");

                    // The body itself stays normal (so the Scene tab shows it); First-Person Body hides it from the
                    // player's camera only. An earlier version of this tool set it to Shadows Only: undo that.
                    if (smr.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
                    {
                        Undo.RecordObject(smr, "First-person body");
                        smr.shadowCastingMode = ShadowCastingMode.On;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
                    }
                }

                var cam = player.GetComponentInChildren<Camera>(true);
                if (cam == null) cam = Camera.main;

                var fpBody = player.GetComponent<FirstPersonBody>();
                if (fpBody == null) fpBody = Undo.AddComponent<FirstPersonBody>(player.gameObject);
                else Undo.RecordObject(fpBody, "First-person body");
                fpBody.playerCamera = cam;
                fpBody.bodyRenderers = new List<Renderer>();
                foreach (var smr in bodyParts) fpBody.bodyRenderers.Add(smr);
                fpBody.armsRenderers = armsRenderers;
                EditorUtility.SetDirty(fpBody);
                PrefabUtility.RecordPrefabInstancePropertyModifications(fpBody);
                log.Add("First-Person Body (on " + player.name + "): the player's camera sees only the arms; the Scene tab shows the whole character");

                // Keep the skeleton animating even when only shadows / arms are visible.
                if (animator != null && animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
                {
                    Undo.RecordObject(animator, "First-person body");
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                    log.Add("The character's Animator now always animates (so the arms never freeze)");
                }

                // The arms hang right next to the camera. With the default near clip (0.3 m) the camera cut them
                // open into thin "flaps"; 0.05 m draws them whole (and walls no longer get cut open up close).
                if (cam != null && cam.nearClipPlane > NearClip + 0.001f)
                {
                    Undo.RecordObject(cam, "First-person camera");
                    log.Add("Camera near clip " + cam.nearClipPlane.ToString("0.###") + " → " + NearClip +
                            " m, so the arms are drawn whole up close");
                    cam.nearClipPlane = NearClip;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                RestoreReadable(restoreReadable);
                EditorUtility.ClearProgressBar();
            }

            Debug.Log("[ARCHIV3D] First-person view:\n- " + string.Join("\n- ", log));
            EditorUtility.DisplayDialog("ARCHIV3D: First-person view",
                "- " + string.Join("\n- ", log) +
                "\n\nIn the game, the player's camera sees only the arms and hands (the full body still casts the player's shadow). " +
                "The Scene tab still shows the whole character." +
                "\n\nPress Play and look down to check.", "Nice");
        }

        // ------------------------------------------------------------------ mesh work

        static Mesh BuildArmsMesh(SkinnedMeshRenderer smr, Transform leftArm, Transform rightArm,
                                  out int keptTris, out int totalTris, out string problem)
        {
            keptTris = 0;
            totalTris = 0;
            problem = null;
            var mesh = smr.sharedMesh;
            var bones = smr.bones;

            var isArm = new bool[bones.Length];
            bool anyArmBone = false;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                isArm[i] = b != null && (b == leftArm || b == rightArm || b.IsChildOf(leftArm) || b.IsChildOf(rightArm));
                anyArmBone |= isArm[i];
            }
            if (!anyArmBone) return null;

            BoneWeight[] weights;
            try { weights = mesh.boneWeights; }
            catch (Exception e) { problem = "couldn't read its skin weights (" + e.Message + ")"; return null; }
            if (weights == null || weights.Length != mesh.vertexCount)
            {
                problem = "couldn't read its skin weights (turn on Read/Write in the model's import settings and run this again)";
                return null;
            }

            var armWeight = new float[weights.Length];
            for (int v = 0; v < weights.Length; v++)
            {
                var w = weights[v];
                float a = 0f;
                if (w.weight0 > 0f && w.boneIndex0 < isArm.Length && isArm[w.boneIndex0]) a += w.weight0;
                if (w.weight1 > 0f && w.boneIndex1 < isArm.Length && isArm[w.boneIndex1]) a += w.weight1;
                if (w.weight2 > 0f && w.boneIndex2 < isArm.Length && isArm[w.boneIndex2]) a += w.weight2;
                if (w.weight3 > 0f && w.boneIndex3 < isArm.Length && isArm[w.boneIndex3]) a += w.weight3;
                armWeight[v] = a;
            }

            var kept = new List<int[]>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) { kept.Add(mesh.GetIndices(s)); continue; }
                int[] tris = mesh.GetTriangles(s);
                totalTris += tris.Length / 3;
                var keep = new List<int>(tris.Length / 4);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    float avg = (armWeight[tris[t]] + armWeight[tris[t + 1]] + armWeight[tris[t + 2]]) / 3f;
                    if (avg < KeepWeight) continue;
                    keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]);
                }
                keptTris += keep.Count / 3;
                kept.Add(keep.ToArray());
            }
            if (keptTris == 0) return null;

            var arms = Object.Instantiate(mesh);
            arms.name = mesh.name + " (first-person arms)";
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) == MeshTopology.Triangles) arms.SetTriangles(kept[s], s, false);
                else arms.SetIndices(kept[s], mesh.GetTopology(s), s, false);
            }
            arms.bounds = mesh.bounds;
            return arms;
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Keep the same file (and GUID) so version control sees an update, not a new file.
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            mesh.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static SkinnedMeshRenderer AddArmsRenderer(SkinnedMeshRenderer source, Mesh armsMesh)
        {
            var go = new GameObject(ArmsPrefix + source.name);
            Undo.RegisterCreatedObjectUndo(go, "First-person arms");
            go.layer = source.gameObject.layer;
            go.transform.SetParent(source.transform.parent, false);
            go.transform.localPosition = source.transform.localPosition;
            go.transform.localRotation = source.transform.localRotation;
            go.transform.localScale = source.transform.localScale;
            go.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

            var r = go.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = armsMesh;
            r.bones = source.bones;
            r.rootBone = source.rootBone;
            r.sharedMaterials = source.sharedMaterials;
            r.localBounds = source.localBounds;
            r.quality = source.quality;
            r.updateWhenOffscreen = true;               // the camera is inside the body: never cull the arms
            r.skinnedMotionVectors = source.skinnedMotionVectors;
            r.shadowCastingMode = ShadowCastingMode.Off; // the full body already casts the shadow
            r.receiveShadows = source.receiveShadows;
            r.lightProbeUsage = source.lightProbeUsage;
            r.reflectionProbeUsage = source.reflectionProbeUsage;
            r.probeAnchor = source.probeAnchor;
            r.enabled = source.enabled;
            for (int i = 0; i < armsMesh.blendShapeCount && i < source.sharedMesh.blendShapeCount; i++)
                r.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
            return r;
        }

        static bool HasArmBones(SkinnedMeshRenderer smr, Transform leftArm, Transform rightArm)
        {
            foreach (var b in smr.bones)
                if (b != null && (b == leftArm || b == rightArm || b.IsChildOf(leftArm) || b.IsChildOf(rightArm))) return true;
            return false;
        }

        // ------------------------------------------------------------------ Read/Write (temporary)

        /// <summary>
        /// The character's model is imported without Read/Write (it saves memory on phones), so its skin weights
        /// can't be read from scripts. Turn it on for a moment and back off again when the arms are built.
        /// </summary>
        static void MakeReadable(List<SkinnedMeshRenderer> parts, List<ModelImporter> restore, List<string> log)
        {
            var done = new HashSet<string>();
            foreach (var smr in parts)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null || mesh.isReadable) continue;
                string path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path) || !done.Add(path)) continue;
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || importer.isReadable) continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
                restore.Add(importer);
            }
        }

        static void RestoreReadable(List<ModelImporter> importers)
        {
            foreach (var importer in importers)
            {
                if (importer == null) continue;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ helpers

        static Transform FindBone(Transform root, params string[] names)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                foreach (var n in names)
                    if (t.name.EndsWith(n, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        static void CreateFolders(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(':', '_');
        }

        // ------------------------------------------------------------------ Play-mode check

        static double s_LookUntil;
        static FirstPersonController s_LookPlayer;
        static bool s_LookPlayerWasOn;

        /// <summary>Play mode: points the player's camera at their feet for a few seconds, to check the arms-only view.</summary>
        [MenuItem("Tools/ARCHIV3D/Debug/Look Down (check first-person arms)", false, 910)]
        static void LookDown()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Press Play first, then use this to look down.", "OK");
                return;
            }
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var cam = Camera.main;
            if (player == null || cam == null) return;

            if (s_LookPlayer == null)
            {
                s_LookPlayer = player;
                s_LookPlayerWasOn = player.enabled;
                EditorApplication.update += HoldLookDown;
            }
            player.enabled = false;    // so the mouse can't move the camera while you look
            var e = cam.transform.localEulerAngles;
            cam.transform.localRotation = Quaternion.Euler(70f, e.y, e.z);
            s_LookUntil = EditorApplication.timeSinceStartup + 15.0;
            Debug.Log("[ARCHIV3D] Looking down for 15 seconds: only the arms and hands should be visible.");
        }

        static void HoldLookDown()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > s_LookUntil || s_LookPlayer == null)
            {
                EditorApplication.update -= HoldLookDown;
                if (s_LookPlayer != null && EditorApplication.isPlaying) s_LookPlayer.enabled = s_LookPlayerWasOn;
                s_LookPlayer = null;
            }
        }
    }
}
