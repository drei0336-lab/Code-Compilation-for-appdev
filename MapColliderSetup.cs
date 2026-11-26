using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Add Missing Colliders (Main Map).
    /// Finds every visible mesh in Main Map that the player could walk through (walls, floors, shelves,
    /// computers, furniture...) and gives it a solid Mesh Collider shaped exactly like the mesh, so the
    /// player's CharacterController stops at it. Meshes that already have a solid collider, or sit inside
    /// a solid box collider on a parent (like the BlackCompu computers), are left alone.
    /// Bookshelf "click area" triggers are kept, so tapping a shelf still opens its book.
    /// Safe to run again: it only adds what is still missing. Undo with Ctrl+Z.
    /// </summary>
    public static class MapColliderSetup
    {
        const string MainMapPath = "Assets/Scenes/Main Map.unity";

        // Props smaller than this (in meters, on every side) are skipped: the player steps over them anyway,
        // and skipping them keeps physics cheap on low-end phones. Interactable objects are never skipped.
        const float TinySize = 0.2f;

        // A small prop made of many meshes (a pile of books, a computer) gets ONE box around the whole prop
        // instead of a collider per mesh, the same way the other book piles and the BlackCompu computers are set up.
        const int ClusterMinMeshes = 4;
        const float ClusterMaxSize = 2.5f;

        // Components that mark something the player can tap / interact with.
        static readonly HashSet<string> InteractableTypes = new HashSet<string>
        {
            "Interactable", "ShelfBook", "BlackCompuComputer", "ComputerQuiz", "BookshelfReader"
        };

        class Target
        {
            public MeshRenderer renderer;   // mesh-collider target
            public MeshFilter filter;
            public GameObject boxRoot;      // box-collider target (a small multi-mesh prop)
            public Bounds boxBounds;        // world bounds the box must wrap
            public int meshCount = 1;
            public string group;
            public bool interactable;
            public bool convex;

            public GameObject Owner => boxRoot != null ? boxRoot : renderer.gameObject;
        }

        [MenuItem("Tools/ARCHIV3D/Add Missing Colliders (Main Map)", false, 3)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Add Missing Colliders", "Stop Play mode first, then run this again.", "OK");
                return;
            }

            Scene scene = EnsureMainMapOpen();
            if (!scene.IsValid()) return;

            int skippedTiny, skippedOff;
            List<Target> targets = FindTargets(scene, out skippedTiny, out skippedOff);

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("Add Missing Colliders",
                    "Every wall and object in Main Map already has a solid collider. Nothing to add.", "OK");
                return;
            }

            string summary = Summarize(targets);
            int meshes = targets.Sum(t => t.meshCount);
            int boxes = targets.Count(t => t.boxRoot != null);
            int interactables = targets.Count(t => t.interactable);
            string message =
                "Found " + meshes + " mesh(es) in Main Map that the player can walk through.\n" +
                "This adds " + targets.Count + " solid collider(s)" +
                (interactables > 0 ? " (" + interactables + " on interactable objects)" : "") + ":\n\n" +
                summary +
                "\nWalls and furniture get a Mesh Collider shaped like the mesh, so doorways and gaps stay open." +
                (boxes > 0 ? "\n" + boxes + " small multi-part prop(s), like book piles, get one box each." : "") +
                (skippedTiny > 0 ? "\n\nSkipped " + skippedTiny + " tiny prop(s) the player steps over anyway." : "") +
                (skippedOff > 0 ? "\nSkipped " + skippedOff + " object(s) whose collider was turned off on purpose." : "") +
                "\n\nThe scene is saved afterwards. Ctrl+Z undoes it.";

            if (!EditorUtility.DisplayDialog("Add Missing Colliders", message, "Add colliders", "Cancel"))
                return;

            Apply(scene, targets);
        }

        static Scene EnsureMainMapOpen()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == MainMapPath) return active;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (s.path == MainMapPath && s.isLoaded) return s;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;
            return EditorSceneManager.OpenScene(MainMapPath, OpenSceneMode.Single);
        }

        static List<Target> FindTargets(Scene scene, out int skippedTiny, out int skippedOff)
        {
            skippedTiny = 0;
            skippedOff = 0;
            var result = new List<Target>();
            var lowerLods = LowerLodRenderers(scene);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    GameObject go = r.gameObject;
                    if (!go.activeInHierarchy || !r.enabled) continue;              // hidden: can't be walked into
                    if (lowerLods.Contains(r)) continue;                            // LOD1+ copies of a LOD0 mesh
                    if (IsPlayerCameraOrUi(r.transform)) continue;
                    if (IsText(go) || go.GetComponent<ParticleSystem>() != null) continue;

                    MeshFilter mf = go.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;

                    if (HasOwnColliderTurnedOff(go)) { skippedOff++; continue; }
                    if (IsCoveredBySolidCollider(r)) continue;

                    bool interactable = IsInteractable(r.transform);
                    Vector3 size = r.bounds.size;
                    if (!interactable && size.x < TinySize && size.y < TinySize && size.z < TinySize)
                    {
                        skippedTiny++;
                        continue;
                    }

                    // A non-convex Mesh Collider is ignored on a moving (dynamic) Rigidbody, so use convex there.
                    Rigidbody body = go.GetComponentInParent<Rigidbody>(true);
                    result.Add(new Target
                    {
                        renderer = r,
                        filter = mf,
                        group = GroupName(go),
                        interactable = interactable,
                        convex = body != null && !body.isKinematic
                    });
                }
            }
            return MergeSmallProps(result);
        }

        /// <summary>Replaces the per-mesh targets of each small multi-mesh prop with one box around the prop.</summary>
        static List<Target> MergeSmallProps(List<Target> targets)
        {
            var byRoot = new Dictionary<GameObject, List<Target>>();
            foreach (Target t in targets)
            {
                GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(t.renderer.gameObject);
                if (root == null || root == t.renderer.gameObject) continue;
                if (!byRoot.TryGetValue(root, out var list)) byRoot[root] = list = new List<Target>();
                list.Add(t);
            }

            var merged = new HashSet<Target>();
            var result = new List<Target>();
            foreach (var pair in byRoot)
            {
                List<Target> list = pair.Value;
                if (list.Count < ClusterMinMeshes) continue;
                if (pair.Key.GetComponentInParent<Rigidbody>(true) != null) continue;

                // Wrap every visible mesh of the prop, not just the ones that were missing a collider.
                Bounds b = list[0].renderer.bounds;
                foreach (MeshRenderer mr in pair.Key.GetComponentsInChildren<MeshRenderer>(false))
                    if (mr.enabled) b.Encapsulate(mr.bounds);
                if (Mathf.Max(b.size.x, b.size.y, b.size.z) > ClusterMaxSize) continue;

                result.Add(new Target
                {
                    boxRoot = pair.Key,
                    boxBounds = b,
                    meshCount = list.Count,
                    group = list[0].group,
                    interactable = list.Any(t => t.interactable)
                });
                foreach (Target t in list) merged.Add(t);
            }

            foreach (Target t in targets)
                if (!merged.Contains(t)) result.Add(t);
            return result;
        }

        static void Apply(Scene scene, List<Target> targets)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add Missing Colliders");
            int undoGroup = Undo.GetCurrentGroup();

            var log = new StringBuilder();
            log.AppendLine("[ARCHIV3D] Added " + targets.Count + " Mesh Collider(s) in Main Map:");
            var selection = new List<Object>();

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    Target t = targets[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Add Missing Colliders",
                            t.Owner.name, (float)i / targets.Count))
                        break;

                    string kind;
                    if (t.boxRoot != null)
                    {
                        FitBox(Undo.AddComponent<BoxCollider>(t.boxRoot), t.boxBounds);
                        kind = "box around " + t.meshCount + " meshes";
                    }
                    else
                    {
                        var mc = Undo.AddComponent<MeshCollider>(t.renderer.gameObject);
                        mc.sharedMesh = t.filter.sharedMesh;
                        mc.convex = t.convex;
                        kind = "mesh";
                    }
                    selection.Add(t.Owner);
                    if (i < 600) log.AppendLine("  " + PathOf(t.Owner.transform) + "  [" + kind + "]" + (t.interactable ? " (interactable)" : ""));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.objects = selection.ToArray();
            Debug.Log(log.ToString());

            EditorUtility.DisplayDialog("Add Missing Colliders",
                "Added " + selection.Count + " collider(s) and saved Main Map.\n\n" +
                "They're selected in the Hierarchy, and the full list is in the Console.\n" +
                "Press Play and walk into the walls and shelves to check.", "OK");
        }

        // ---------- checks ----------

        static bool IsPlayerCameraOrUi(Transform t)
        {
            return t.GetComponentInParent<CharacterController>(true) != null
                || t.GetComponentInParent<Camera>(true) != null
                || t.GetComponentInParent<Canvas>(true) != null;
        }

        static bool IsText(GameObject go)
        {
            if (go.GetComponent<TextMesh>() != null) return true;
            foreach (Component c in go.GetComponents<Component>())
                if (c != null && c.GetType().Name.StartsWith("TextMeshPro")) return true;
            return false;
        }

        static bool IsInteractable(Transform t)
        {
            foreach (MonoBehaviour mb in t.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                for (System.Type type = mb.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                    if (InteractableTypes.Contains(type.Name)) return true;
            }
            return false;
        }

        static bool HasOwnColliderTurnedOff(GameObject go)
        {
            Collider[] own = go.GetComponents<Collider>();
            return own.Length > 0 && own.All(c => !c.enabled);
        }

        /// <summary>True if this mesh already has a solid collider, or sits inside a solid collider on a parent.
        /// Trigger colliders (like the bookshelf click areas) don't count: the player walks through them.</summary>
        static bool IsCoveredBySolidCollider(Renderer r)
        {
            Bounds mesh = r.bounds;
            for (Transform t = r.transform; t != null; t = t.parent)
            {
                foreach (Collider c in t.GetComponents<Collider>())
                {
                    if (c == null || !c.enabled || c.isTrigger) continue;
                    if (t == r.transform) return true;

                    Bounds b = WorldBounds(c);
                    b.Expand(b.size * 0.1f + Vector3.one * 0.02f);
                    if (b.Contains(mesh.min) && b.Contains(mesh.max)) return true;
                }
            }
            return false;
        }

        static Bounds WorldBounds(Collider c)
        {
            if (c is BoxCollider box)
            {
                Transform t = box.transform;
                Vector3 e = box.size * 0.5f;
                var b = new Bounds(t.TransformPoint(box.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var corner = box.center + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x,
                        (i & 2) == 0 ? -e.y : e.y,
                        (i & 4) == 0 ? -e.z : e.z);
                    b.Encapsulate(t.TransformPoint(corner));
                }
                return b;
            }
            return c.bounds;
        }

        /// <summary>Sizes a box collider (in its object's local space) to wrap the given world bounds.</summary>
        static void FitBox(BoxCollider box, Bounds world)
        {
            Transform t = box.transform;
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? world.min.x : world.max.x,
                    (i & 2) == 0 ? world.min.y : world.max.y,
                    (i & 4) == 0 ? world.min.z : world.max.z);
                Vector3 local = t.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
            box.center = (min + max) * 0.5f;
            box.size = Vector3.Max(max - min, Vector3.one * 0.01f);
        }

        static HashSet<Renderer> LowerLodRenderers(Scene scene)
        {
            var set = new HashSet<Renderer>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (LODGroup g in root.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = g.GetLODs();
                for (int i = 1; i < lods.Length; i++)
                    foreach (Renderer r in lods[i].renderers)
                        if (r != null) set.Add(r);
            }
            return set;
        }

        // ---------- reporting ----------

        static string GroupName(GameObject go)
        {
            GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            return outer != null ? outer.name : go.transform.root.name;
        }

        static string Summarize(List<Target> targets)
        {
            var sb = new StringBuilder();
            var groups = targets.GroupBy(t => t.group).OrderByDescending(g => g.Count()).ToList();
            int shown = 0;
            foreach (var g in groups)
            {
                if (shown == 12) { sb.AppendLine("  ...and " + (groups.Count - shown) + " more group(s)"); break; }
                sb.AppendLine("  " + g.Key + ": " + g.Count());
                shown++;
            }
            return sb.ToString();
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
