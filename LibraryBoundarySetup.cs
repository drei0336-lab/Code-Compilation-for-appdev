using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Add Invisible Wall at Library Door
    ///
    /// Puts an invisible wall (a solid Box Collider) across the library's front door, so the player can't walk
    /// out onto the terrain. When the player comes close, a red barrier grid with "NOT ACCESSIBLE" fades in on
    /// the doorway and a short message shows at the top of the screen (see LibraryBoundary.cs).
    /// The wall is on the "Ignore Raycast" layer, so it never blocks tapping or looking at things.
    ///
    /// Afterwards the tool walks the whole library floor (like the player would, with the player's own size)
    /// to check that there's no other way out, and tells you what it found.
    /// Safe to run again: it replaces the old barrier.
    /// </summary>
    public static class LibraryBoundarySetup
    {
        public const string BarrierName = "Library Exit Barrier";
        public const string WallsName = "Library Wall Colliders";
        const string MaterialDir = "Assets/ARCHIV3D/Materials";
        const string MaterialPath = MaterialDir + "/Barrier Grid.mat";
        const string ShaderName = "ARCHIV3D/Barrier Grid";
        const int IgnoreRaycastLayer = 2;

        // Main Map's front door, used only if the door frame can't be found by name.
        static readonly Bounds FallbackDoor = new Bounds(new Vector3(16.447f, 1.534f, -0.872f), new Vector3(0.16f, 3f, 2.7f));
        static readonly Bounds FallbackFloor = new Bounds(new Vector3(-0.033f, 0.034f, -0.872f), new Vector3(32.8f, 0f, 16.4f));

        [MenuItem("Tools/ARCHIV3D/Add Invisible Wall at Library Door", false, 41)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then try again.", "OK");
                return;
            }
            var scene = OpenMainMap();
            if (!scene.IsValid()) return;

            var log = new List<string>();
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "The \"" + ShaderName + "\" shader is missing (Assets/ARCHIV3D/Shaders/BarrierGrid.shader).", "OK");
                return;
            }

            Bounds door = FindDoor(scene, out string doorName);
            Bounds floor = FindFloor(scene);
            log.Add(doorName != null ? "Door: " + doorName : "Door frame not found by name, used Main Map's front door position");

            // Old barrier from an earlier run
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == BarrierName) Undo.DestroyObjectImmediate(root);

            var material = EnsureMaterial(shader);
            var barrier = BuildBarrier(scene, door, floor, material);
            log.Add("Added \"" + BarrierName + "\": an invisible wall across the doorway with the red \"NOT ACCESSIBLE\" grid");

            EditorSceneManager.MarkSceneDirty(scene);
            Physics.SyncTransforms();

            // Can the player still get out anywhere?
            var report = CheckLeaks(scene, floor, "after");
            string result;
            if (report.error != null) result = "Couldn't check the rest of the library: " + report.error;
            else if (report.exits.Count == 0) result = "Checked the whole library floor with the player's size: there's no other way out.";
            else
            {
                result = "The player can still get out at " + report.exits.Count + " other spot(s) where the walls have no collider:\n" + report.ExitList() +
                         "\n(map: " + report.mapPath + ")";
                if (EditorUtility.DisplayDialog("ARCHIV3D: Library walls", result +
                        "\n\nMake the library's outer walls solid too? This adds invisible colliders along the edges of the library floor " +
                        "(nothing visible changes).", "Make walls solid", "Skip"))
                {
                    SealWalls(scene, floor);
                    Physics.SyncTransforms();
                    var again = CheckLeaks(scene, floor, "after-walls");
                    result = again.exits.Count == 0
                        ? "Made the library's outer walls solid. Checked again: there's no way out now."
                        : "Made the outer walls solid, but the player can still get out at:\n" + again.ExitList() + "\n(map: " + again.mapPath + ")";
                }
            }
            log.Add(result);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = barrier;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

            Debug.Log("[ARCHIV3D] Library door:\n- " + string.Join("\n- ", log));
            EditorUtility.DisplayDialog("ARCHIV3D: Library door", "- " + string.Join("\n- ", log) +
                "\n\nPress Play and walk to the front door: you'll stop at it, the red grid appears and \"NOT ACCESSIBLE\" shows.", "Nice");
        }

        [MenuItem("Tools/ARCHIV3D/Debug/Check Library for Openings (writes a map)", false, 912)]
        static void CheckMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then try again.", "OK");
                return;
            }
            var scene = OpenMainMap();
            if (!scene.IsValid()) return;
            Physics.SyncTransforms();
            var report = CheckLeaks(scene, FindFloor(scene), "check");
            string msg = report.error != null ? report.error
                : report.exits.Count == 0 ? "No way out: the player can't leave the library floor."
                : "The player can get out at " + report.exits.Count + " spot(s):\n" + report.ExitList();
            msg += "\n\nReachable floor: " + report.reachableArea.ToString("0") + " m². Map: " + report.mapPath;
            Debug.Log("[ARCHIV3D] Library openings check: " + msg);
            EditorUtility.DisplayDialog("ARCHIV3D: Library openings", msg, "OK");
        }

        // ------------------------------------------------------------------ building

        static Scene OpenMainMap()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path == ArchiveHud.MainMapPath) return scene;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;
            return EditorSceneManager.OpenScene(ArchiveHud.MainMapPath, OpenSceneMode.Single);
        }

        static Bounds FindDoor(Scene scene, out string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.name.ToLowerInvariant();
                    if (n.Contains("puertadoble") || n.StartsWith("marco_puerta"))
                    {
                        name = r.name;
                        return r.bounds;
                    }
                }
            name = null;
            return FallbackDoor;
        }

        static Bounds FindFloor(Scene scene)
        {
            Renderer best = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "Floor" && (best == null || root.name.StartsWith("LibraryInv"))) best = r;
            return best != null ? best.bounds : FallbackFloor;
        }

        static Material EnsureMaterial(Shader shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                if (!AssetDatabase.IsValidFolder(MaterialDir)) AssetDatabase.CreateFolder("Assets/ARCHIV3D", "Materials");
                mat = new Material(shader) { name = "Barrier Grid" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.shader = shader;
            mat.SetColor("_Color", new Color(1f, 0.27f, 0.22f, 1f));
            mat.SetFloat("_Alpha", 0.6f);     // how it looks in the editor; in the game the script fades it in
            mat.SetFloat("_CellSize", 0.4f);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        static GameObject BuildBarrier(Scene scene, Bounds door, Bounds floor, Material material)
        {
            // The doorway is the frame's thin side; "outward" points from the library floor to the outside.
            bool thinX = door.size.x <= door.size.z;
            Vector3 outward = thinX ? Vector3.right : Vector3.forward;
            Vector3 toInside = floor.center - door.center;
            toInside.y = 0f;
            if (Vector3.Dot(toInside, outward) > 0f) outward = -outward;
            float width = thinX ? door.size.z : door.size.x;
            float height = door.size.y;
            float floorY = door.min.y;

            var root = new GameObject(BarrierName);
            Undo.RegisterCreatedObjectUndo(root, "Library door barrier");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = IgnoreRaycastLayer;
            root.transform.SetPositionAndRotation(new Vector3(door.center.x, floorY, door.center.z),
                                                  Quaternion.LookRotation(outward, Vector3.up));

            // The invisible wall. Local +Z points outside. It starts 0.45 m inside the doorway, so the camera
            // stays far enough from the grid that it's never cut off by the camera's near plane.
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = new Vector3(0f, (height + 0.6f) * 0.5f, -0.1f);
            box.size = new Vector3(width + 0.3f, height + 0.6f, 0.7f);

            // The red grid across the doorway
            var grid = new GameObject("Barrier Grid");
            grid.layer = IgnoreRaycastLayer;
            grid.transform.SetParent(root.transform, false);
            grid.transform.localPosition = new Vector3(0f, height * 0.5f, -0.02f);
            grid.transform.localScale = new Vector3(width, height, 1f);
            grid.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var gridRenderer = grid.AddComponent<MeshRenderer>();
            gridRenderer.sharedMaterial = material;
            gridRenderer.shadowCastingMode = ShadowCastingMode.Off;
            gridRenderer.receiveShadows = false;
            gridRenderer.lightProbeUsage = LightProbeUsage.Off;
            gridRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            gridRenderer.sortingOrder = 0;

            // "NOT ACCESSIBLE" on the barrier, at eye level
            var labelGo = new GameObject("NOT ACCESSIBLE Label", typeof(RectTransform));
            labelGo.layer = IgnoreRaycastLayer;
            labelGo.transform.SetParent(root.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.62f, -0.05f);
            var label = labelGo.AddComponent<TextMeshPro>();
            var bold = ArchiveAssets.BoldFont;
            if (bold != null) label.font = bold;
            label.text = "NOT ACCESSIBLE";
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.2f;
            label.fontSizeMax = 2f;
            label.characterSpacing = 8f;
            label.color = new Color32(255, 244, 236, 255);
            label.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(width - 0.6f, 0.6f, 1.0f), 0.3f);
            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 2;
                labelRenderer.shadowCastingMode = ShadowCastingMode.Off;
                labelRenderer.receiveShadows = false;
            }

            var boundary = root.AddComponent<LibraryBoundary>();
            boundary.title = "NOT ACCESSIBLE";
            boundary.message = "The library exit is closed. Keep exploring inside!";
            boundary.gridRenderer = gridRenderer;
            boundary.wallLabel = label;
            boundary.titleFont = bold;
            boundary.messageFont = ArchiveAssets.RegularFont;
            boundary.pillSprite = ArchiveAssets.Pill;
            return root;
        }

        /// <summary>Invisible colliders along the four edges of the library floor (nothing visible).</summary>
        static void SealWalls(Scene scene, Bounds floor)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == WallsName) Undo.DestroyObjectImmediate(r);

            var root = new GameObject(WallsName);
            Undo.RegisterCreatedObjectUndo(root, "Library walls");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.layer = IgnoreRaycastLayer;

            const float inset = 0.2f, thick = 1f;
            float y0 = floor.min.y - 1f, y1 = floor.min.y + 14f;
            float h = y1 - y0, cy = (y0 + y1) * 0.5f;
            float sx = floor.size.x + 2f * thick, sz = floor.size.z + 2f * thick;
            AddWall(root, "Wall Collider (East)", new Vector3(floor.max.x - inset + thick * 0.5f, cy, floor.center.z), new Vector3(thick, h, sz));
            AddWall(root, "Wall Collider (West)", new Vector3(floor.min.x + inset - thick * 0.5f, cy, floor.center.z), new Vector3(thick, h, sz));
            AddWall(root, "Wall Collider (North)", new Vector3(floor.center.x, cy, floor.max.z - inset + thick * 0.5f), new Vector3(sx, h, thick));
            AddWall(root, "Wall Collider (South)", new Vector3(floor.center.x, cy, floor.min.z + inset - thick * 0.5f), new Vector3(sx, h, thick));
        }

        static void AddWall(GameObject root, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.layer = IgnoreRaycastLayer;
            go.transform.SetParent(root.transform, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ------------------------------------------------------------------ "can the player get out?" check

        class LeakReport
        {
            public readonly List<Vector3> exits = new List<Vector3>();
            public float reachableArea;
            public string mapPath = "-";
            public string error;

            public string ExitList()
            {
                var sb = new StringBuilder();
                foreach (var e in exits) sb.Append("  • near x ").Append(e.x.ToString("0.0")).Append(", z ").Append(e.z.ToString("0.0")).Append('\n');
                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>
        /// Flood-fills the floor from the player's spot on a 20 cm grid, testing each spot with a capsule the size of
        /// the player's CharacterController (ignoring triggers and the big box around the whole building). Any spot
        /// it reaches more than 1 m outside the library floor means there's a way out. Writes a top-down map to Logs/.
        /// </summary>
        static LeakReport CheckLeaks(Scene scene, Bounds floor, string label)
        {
            var report = new LeakReport();
            FirstPersonController player = null;
            foreach (var root in scene.GetRootGameObjects())
                if ((player = root.GetComponentInChildren<FirstPersonController>(true)) != null) break;
            if (player == null) { report.error = "no player (FirstPersonController) in the scene"; return report; }

            float radius = 0.125f, height = 0.9f;
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                var s = player.transform.lossyScale;
                radius = cc.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
                height = Mathf.Max(cc.height * Mathf.Abs(s.y), radius * 2f + 0.1f);
            }
            float floorY = floor.min.y;
            Vector3 start = player.transform.position;

            // Colliders that wrap the whole building (like the library model's big box) don't stop the player from inside.
            var ignore = new HashSet<Collider>();
            foreach (var c in Physics.OverlapSphere(new Vector3(start.x, floorY + 0.5f, start.z), 0.05f, ~0, QueryTriggerInteraction.Ignore))
                if (c.bounds.size.x > 10f && c.bounds.size.z > 10f) ignore.Add(c);
            foreach (var c in player.GetComponentsInChildren<Collider>(true)) ignore.Add(c);

            const float cell = 0.2f;
            Bounds area = floor;
            area.Expand(new Vector3(8f, 0f, 8f));
            int nx = Mathf.CeilToInt(area.size.x / cell), nz = Mathf.CeilToInt(area.size.z / cell);
            Vector3 o = area.min;
            var state = new byte[nx * nz];      // 0 = not tested, 1 = free, 2 = blocked
            var ground = new float[nx * nz];
            var parent = new int[nx * nz];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;   // -2 = not reached

            bool Free(int i)
            {
                if (state[i] != 0) return state[i] == 1;
                float x = o.x + (i % nx + 0.5f) * cell, z = o.z + (i / nx + 0.5f) * cell;
                float g = floorY;
                bool found = false;
                foreach (var hit in Physics.RaycastAll(new Vector3(x, floorY + 1.2f, z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (ignore.Contains(hit.collider)) continue;
                    if (!found || hit.point.y > g) { g = hit.point.y; found = true; }
                }
                ground[i] = g;
                var a = new Vector3(x, g + 0.2f + radius, z);
                var b = new Vector3(x, Mathf.Max(g + height - radius, a.y), z);
                bool blocked = false;
                foreach (var c in Physics.OverlapCapsule(a, b, radius * 0.95f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (ignore.Contains(c) || c is TerrainCollider) continue;
                    blocked = true;
                    break;
                }
                state[i] = blocked ? (byte)2 : (byte)1;
                return !blocked;
            }

            int sx = Mathf.Clamp((int)((start.x - o.x) / cell), 0, nx - 1), sz = Mathf.Clamp((int)((start.z - o.z) / cell), 0, nz - 1);
            int startIndex = -1;
            for (int r = 0; r < 8 && startIndex < 0; r++)
                for (int dz = -r; dz <= r && startIndex < 0; dz++)
                    for (int dx = -r; dx <= r && startIndex < 0; dx++)
                    {
                        int x = sx + dx, z = sz + dz;
                        if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                        if (Free(z * nx + x)) startIndex = z * nx + x;
                    }
            if (startIndex < 0) { report.error = "the player's spot is blocked"; return report; }

            var queue = new Queue<int>();
            parent[startIndex] = -1;
            queue.Enqueue(startIndex);
            int reached = 0;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                reached++;
                int x = i % nx, z = i / nx;
                for (int k = 0; k < 4; k++)
                {
                    int ax = x + (k == 0 ? 1 : k == 1 ? -1 : 0), az = z + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (ax < 0 || az < 0 || ax >= nx || az >= nz) continue;
                    int j = az * nx + ax;
                    if (parent[j] != -2 || !Free(j)) continue;
                    if (Mathf.Abs(ground[j] - ground[i]) > 0.35f) continue;   // too high a step
                    parent[j] = i;
                    queue.Enqueue(j);
                }
            }
            report.reachableArea = reached * cell * cell;

            bool InsideFloor(int i, float margin)
            {
                float x = o.x + (i % nx + 0.5f) * cell, z = o.z + (i / nx + 0.5f) * cell;
                return x >= floor.min.x - margin && x <= floor.max.x + margin && z >= floor.min.z - margin && z <= floor.max.z + margin;
            }

            // Where does each way out leave the floor? Follow the path back to the last spot on the floor.
            var exitCells = new HashSet<int>();
            var traced = new HashSet<int>();
            for (int i = 0; i < parent.Length; i++)
            {
                if (parent[i] == -2 || InsideFloor(i, 1f)) continue;
                int c = i;
                while (c >= 0 && !InsideFloor(c, 0f) && traced.Add(c)) c = parent[c];
                if (c >= 0 && InsideFloor(c, 0f)) exitCells.Add(c);
            }
            foreach (int c in exitCells)
            {
                var p = new Vector3(o.x + (c % nx + 0.5f) * cell, floorY, o.z + (c / nx + 0.5f) * cell);
                bool merged = false;
                for (int e = 0; e < report.exits.Count && !merged; e++)
                    if (Vector3.Distance(report.exits[e], p) < 2f) merged = true;
                if (!merged) report.exits.Add(p);
            }

            report.mapPath = WriteMap(area, floor, nx, nz, cell, state, parent, report.exits, start, label);
            return report;
        }

        static string WriteMap(Bounds area, Bounds floor, int nx, int nz, float cell, byte[] state, int[] parent,
                               List<Vector3> exits, Vector3 start, string label)
        {
            int w = 1600;
            int h = Mathf.Max(64, Mathf.RoundToInt(w * area.size.z / area.size.x));
            Texture2D tex = null;
            RenderTexture rt = null;
            GameObject camGo = null;
            try
            {
                camGo = new GameObject("ARCHIV3D Map Camera") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.transform.SetPositionAndRotation(new Vector3(area.center.x, floor.min.y + 6f, area.center.z), Quaternion.Euler(90f, 0f, 0f));
                cam.orthographicSize = area.size.z * 0.5f;
                cam.aspect = (float)w / h;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 9f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
                cam.cullingMask = ~(1 << 5);   // no UI
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                try { cam.Render(); } catch (System.Exception e) { Debug.LogWarning("[ARCHIV3D] Map render: " + e.Message); }

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                RenderTexture.active = prev;

                var px = tex.GetPixels32();
                var green = new Color32(60, 220, 90, 255);
                var red = new Color32(255, 40, 40, 255);
                var gray = new Color32(0, 0, 0, 255);
                for (int y = 0; y < h; y++)
                {
                    float wz = area.min.z + (y + 0.5f) / h * area.size.z;
                    int cz = Mathf.Clamp((int)((wz - area.min.z) / cell), 0, nz - 1);
                    for (int x = 0; x < w; x++)
                    {
                        float wx = area.min.x + (x + 0.5f) / w * area.size.x;
                        int cx = Mathf.Clamp((int)((wx - area.min.x) / cell), 0, nx - 1);
                        int i = cz * nx + cx;
                        int p = y * w + x;
                        bool onFloor = wx >= floor.min.x && wx <= floor.max.x && wz >= floor.min.z && wz <= floor.max.z;
                        if (parent[i] != -2) px[p] = Color32.Lerp(px[p], onFloor ? green : red, onFloor ? 0.35f : 0.6f);
                        else if (state[i] == 2) px[p] = Color32.Lerp(px[p], gray, 0.25f);
                    }
                }

                void Dot(Vector3 world, float radiusM, Color32 c)
                {
                    int cx = Mathf.RoundToInt((world.x - area.min.x) / area.size.x * w);
                    int cy = Mathf.RoundToInt((world.z - area.min.z) / area.size.z * h);
                    int rp = Mathf.Max(2, Mathf.RoundToInt(radiusM / area.size.x * w));
                    for (int y = cy - rp; y <= cy + rp; y++)
                        for (int x = cx - rp; x <= cx + rp; x++)
                        {
                            if (x < 0 || y < 0 || x >= w || y >= h) continue;
                            int d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                            if (d2 <= rp * rp && d2 >= (rp - 3) * (rp - 3)) px[y * w + x] = c;
                        }
                }
                // Floor outline
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        float wx = area.min.x + (x + 0.5f) / w * area.size.x, wz = area.min.z + (y + 0.5f) / h * area.size.z;
                        float pxm = area.size.x / w * 1.5f;
                        bool inX = wx >= floor.min.x - pxm && wx <= floor.max.x + pxm, inZ = wz >= floor.min.z - pxm && wz <= floor.max.z + pxm;
                        bool edge = (inZ && (Mathf.Abs(wx - floor.min.x) < pxm || Mathf.Abs(wx - floor.max.x) < pxm)) ||
                                    (inX && (Mathf.Abs(wz - floor.min.z) < pxm || Mathf.Abs(wz - floor.max.z) < pxm));
                        if (edge) px[y * w + x] = new Color32(255, 255, 255, 255);
                    }
                foreach (var e in exits) Dot(e, 0.9f, new Color32(255, 0, 255, 255));
                Dot(start, 0.4f, new Color32(40, 120, 255, 255));

                tex.SetPixels32(px);
                tex.Apply();
                string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", "Logs");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "ARCHIV3D_LibraryMap_" + label + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                return path;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ARCHIV3D] Couldn't write the library map: " + e.Message);
                return "-";
            }
            finally
            {
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (camGo != null) Object.DestroyImmediate(camGo);
            }
        }

        // ------------------------------------------------------------------ Play-mode test

        static FirstPersonController s_WalkPlayer;
        static CharacterController s_WalkCc;
        static Transform s_WalkBarrier;
        static Vector3 s_WalkDir;
        static double s_WalkStart, s_WalkLast;
        static bool s_WalkWasOn, s_WalkReported;

        /// <summary>
        /// Play mode: puts the player 3 m inside the front door, facing it, and walks them straight into it for
        /// 4 seconds. The Console says where they stopped. The view stays still for 15 more seconds so you can look.
        /// </summary>
        [MenuItem("Tools/ARCHIV3D/Debug/Walk Into the Library Door (test)", false, 911)]
        static void WalkTest()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Press Play first, then use this test.", "OK");
                return;
            }
            var barrier = Object.FindAnyObjectByType<LibraryBoundary>();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            if (barrier == null || player == null)
            {
                Debug.LogWarning("[ARCHIV3D] Walk test: no Library Exit Barrier or no player in the scene.");
                return;
            }
            var cc = player.GetComponent<CharacterController>();
            if (cc == null) return;

            Vector3 dir = barrier.transform.forward;
            dir.y = 0f;
            dir.Normalize();
            Vector3 pos = barrier.transform.position - dir * 3.2f;
            pos.y = player.transform.position.y;

            if (s_WalkPlayer == null)
            {
                s_WalkWasOn = player.enabled;
                EditorApplication.update += WalkStep;
            }
            s_WalkPlayer = player;
            s_WalkCc = cc;
            s_WalkBarrier = barrier.transform;
            s_WalkDir = dir;
            s_WalkStart = s_WalkLast = EditorApplication.timeSinceStartup;
            s_WalkReported = false;

            player.enabled = false;      // the test walks instead of the controls (and the mouse can't turn the view)
            cc.enabled = false;
            player.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Vector3.up));
            cc.enabled = true;
            var cam = Camera.main;
            if (cam != null) cam.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            SetSpeed(player, 1f);
            Debug.Log("[ARCHIV3D] Walk test: walking into the front door...");
        }

        static void WalkStep()
        {
            if (!EditorApplication.isPlaying || s_WalkPlayer == null || s_WalkCc == null)
            {
                EditorApplication.update -= WalkStep;
                s_WalkPlayer = null;
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            double t = now - s_WalkStart;
            if (t < 4.0)
            {
                float dt = Mathf.Min((float)(now - s_WalkLast), 0.05f);
                s_WalkLast = now;
                if (s_WalkCc.enabled) s_WalkCc.Move(s_WalkDir * 2.5f * dt + Vector3.down * 0.02f);
            }
            else if (!s_WalkReported)
            {
                s_WalkReported = true;
                SetSpeed(s_WalkPlayer, 0f);
                float past = Vector3.Dot(s_WalkPlayer.transform.position - s_WalkBarrier.position, s_WalkDir);
                Debug.Log(past < 0f
                    ? "[ARCHIV3D] Walk test PASSED: the player stopped " + (-past).ToString("0.00") + " m before the doorway and can't go out."
                    : "[ARCHIV3D] Walk test FAILED: the player got " + past.ToString("0.00") + " m past the doorway.");
            }
            else if (t > 19.0)
            {
                s_WalkPlayer.enabled = s_WalkWasOn;
                EditorApplication.update -= WalkStep;
                s_WalkPlayer = null;
            }
        }

        static void SetSpeed(Component player, float speed)
        {
            var animator = player.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters)
                if (p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) { animator.SetFloat("Speed", speed); return; }
        }
    }
}
