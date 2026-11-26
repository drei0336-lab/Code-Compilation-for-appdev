using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Writes a plain-text report of what the open scene costs to draw on a phone
    /// (triangles, lights, shadows, terrain trees, textures, settings) to Logs/ARCHIV3D-PerformanceReport.txt.
    /// </summary>
    public static class PerformanceReport
    {
        public const string ReportPath = "Logs/ARCHIV3D-PerformanceReport.txt";

        [MenuItem("Tools/ARCHIV3D/Performance Report (open scene)", false, 40)]
        public static void Run()
        {
            string text = Build();
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, text);
            Debug.Log("[ARCHIV3D] Performance report written to " + System.IO.Path.GetFullPath(ReportPath) + "\n" + FirstLines(text, 40));
        }

        static string FirstLines(string s, int n) => string.Join("\n", s.Split('\n').Take(n));

        public static string Build()
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.GetActiveScene();
            sb.AppendLine("ARCHIV3D performance report");
            sb.AppendLine("Scene: " + scene.path + "   Unity " + Application.unityVersion + "   Target: " + EditorUserBuildSettings.activeBuildTarget);
            sb.AppendLine("Written: " + DateTime.Now);
            sb.AppendLine();

            Renderers(sb);
            Lights(sb);
            Terrains(sb);
            Cameras(sb);
            Textures(sb, scene);
            Audio(sb, scene);
            Settings(sb);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ renderers

        static void Renderers(StringBuilder sb)
        {
            var rends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            long tris = 0, shadowTris = 0;
            int staticBatch = 0, casters = 0, subMeshes = 0;
            var perMesh = new Dictionary<Mesh, (int count, long tris)>();
            var shaders = new Dictionary<string, int>();
            var mats = new HashSet<Material>();
            int transparent = 0;

            foreach (var r in rends)
            {
                Mesh m = null;
                if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); m = mf ? mf.sharedMesh : null; }
                else if (r is SkinnedMeshRenderer smr) m = smr.sharedMesh;
                long t = 0;
                if (m != null)
                {
                    for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3;
                    subMeshes += m.subMeshCount;
                    perMesh.TryGetValue(m, out var e);
                    perMesh[m] = (e.count + 1, e.tris + t);
                }
                tris += t;
                if (r.shadowCastingMode != ShadowCastingMode.Off) { casters++; shadowTris += t; }
                if (GameObjectUtility.GetStaticEditorFlags(r.gameObject).HasFlag(StaticEditorFlags.BatchingStatic)) staticBatch++;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    mats.Add(mat);
                    string sn = mat.shader ? mat.shader.name : "(none)";
                    shaders.TryGetValue(sn, out int c); shaders[sn] = c + 1;
                    if (mat.renderQueue >= 2500 && mat.renderQueue < 4000) transparent++;
                }
            }

            int lodGroups = Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
            sb.AppendLine("== MESHES ==");
            sb.AppendLine($"Renderers (active): {rends.Length}   sub-meshes: {subMeshes}   unique materials: {mats.Count}   transparent material slots: {transparent}");
            sb.AppendLine($"Triangles (all, no culling): {tris:N0}   in shadow casters: {shadowTris:N0} ({casters} casters)");
            sb.AppendLine($"Static-batching renderers: {staticBatch}   LOD Groups: {lodGroups}");
            sb.AppendLine("Heaviest meshes (mesh, copies, triangles in scene, read/write):");
            foreach (var kv in perMesh.OrderByDescending(k => k.Value.tris).Take(25))
                sb.AppendLine($"  {kv.Key.name,-48} x{kv.Value.count,-4} {kv.Value.tris,12:N0}   rw={kv.Key.isReadable}   {AssetDatabase.GetAssetPath(kv.Key)}");
            sb.AppendLine("Heaviest object groups (name without numbers, copies, triangles, typical size):");
            var groups = new Dictionary<string, (int count, long tris, float size)>();
            foreach (var r in rends)
            {
                Mesh m = MeshOf(r);
                if (m == null) continue;
                long t = 0;
                for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3;
                string key = System.Text.RegularExpressions.Regex.Replace(r.name, @"[\s_.()\-]*\d+[\s_.()\-]*", " ").Trim();
                groups.TryGetValue(key, out var e);
                groups[key] = (e.count + 1, e.tris + t, Mathf.Max(e.size, r.bounds.size.magnitude));
            }
            foreach (var kv in groups.OrderByDescending(k => k.Value.tris).Take(30))
                sb.AppendLine($"  {kv.Key,-40} x{kv.Value.count,-5} {kv.Value.tris,12:N0}   size {kv.Value.size:0.0} m");
            var sizes = new[] { 0.5f, 1f, 2f, 4f };
            sb.Append("Triangles by object size (bounds diagonal):");
            float prev = 0f;
            foreach (var lim in sizes.Concat(new[] { float.MaxValue }))
            {
                long t = 0; int c = 0;
                foreach (var r in rends)
                {
                    float d = r.bounds.size.magnitude;
                    if (d < prev || d >= lim) continue;
                    Mesh m = MeshOf(r);
                    if (m == null) continue;
                    for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3;
                    c++;
                }
                sb.Append($"  [{prev:0.#}-{(lim == float.MaxValue ? "+" : lim.ToString("0.#"))} m: {c} objects, {t:N0} tris]");
                prev = lim;
            }
            sb.AppendLine();
            sb.AppendLine("Shaders (material slots):");
            foreach (var kv in shaders.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kv.Value,5}  {kv.Key}");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------ lights

        static void Lights(StringBuilder sb)
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(l => l.enabled && l.gameObject.activeInHierarchy).ToArray();
            sb.AppendLine("== LIGHTS ==");
            sb.AppendLine($"Active lights: {lights.Length}");
            foreach (var g in lights.GroupBy(l => (l.type, l.lightmapBakeType, l.shadows)))
                sb.AppendLine($"  {g.Count(),4} x {g.Key.type} / {g.Key.lightmapBakeType} / shadows {g.Key.shadows}");
            foreach (var l in lights.Take(40))
                sb.AppendLine($"    {PathOf(l.transform)}  int={l.intensity:0.##} range={l.range:0.#} render={l.renderMode}");
            var probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            sb.AppendLine($"Reflection probes: {probes.Length}" + string.Concat(probes.Select(p => $"  [{p.name}: {p.mode}, {p.resolution}px]")));
            var ps = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            sb.AppendLine($"Particle systems: {ps.Length}");
            sb.AppendLine($"Lightmaps baked: {LightmapSettings.lightmaps.Length}   Fog: {RenderSettings.fog}   Skybox: {(RenderSettings.skybox ? RenderSettings.skybox.shader.name : "none")}");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------ terrain

        static void Terrains(StringBuilder sb)
        {
            var ts = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            sb.AppendLine("== TERRAIN ==");
            sb.AppendLine($"Terrains: {ts.Length}");
            foreach (var t in ts)
            {
                var d = t.terrainData;
                if (d == null) continue;
                sb.AppendLine($"  {t.name}: size {d.size}  heightmap {d.heightmapResolution}  splat {d.alphamapResolution} x{d.terrainLayers.Length} layers  " +
                              $"pixelError {t.heightmapPixelError}  instanced {t.drawInstanced}  shadows {t.shadowCastingMode}");
                sb.AppendLine($"     trees: {d.treeInstanceCount}  treeDistance {t.treeDistance}  billboardStart {t.treeBillboardDistance}  maxMeshTrees {t.treeMaximumFullLODCount}  " +
                              $"details: {d.detailPrototypes.Length} prototypes  detailDistance {t.detailObjectDistance}");
                var counts = new int[d.treePrototypes.Length];
                foreach (var ti in d.treeInstances) if (ti.prototypeIndex < counts.Length) counts[ti.prototypeIndex]++;
                for (int i = 0; i < d.treePrototypes.Length; i++)
                {
                    var p = d.treePrototypes[i].prefab;
                    if (p == null) continue;
                    long tri = 0;
                    var lod = p.GetComponent<LODGroup>();
                    var rs = lod != null && lod.GetLODs().Length > 0 ? lod.GetLODs()[0].renderers : p.GetComponentsInChildren<Renderer>();
                    foreach (var r in rs)
                    {
                        var mf = r ? r.GetComponent<MeshFilter>() : null;
                        if (mf && mf.sharedMesh) for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) tri += mf.sharedMesh.GetIndexCount(s) / 3;
                    }
                    var mat = rs.FirstOrDefault(r => r != null)?.sharedMaterial;
                    sb.AppendLine($"     tree {i}: {p.name}  x{counts[i]}  LOD0 tris {tri:N0}  LODGroup {(lod ? lod.lodCount.ToString() : "none")}  " +
                                  $"shader {(mat && mat.shader ? mat.shader.name : "?")}  instancing {(mat ? mat.enableInstancing : false)}");
                }
            }
            sb.AppendLine();
        }

        // ------------------------------------------------------------------ cameras / volumes

        static void Cameras(StringBuilder sb)
        {
            sb.AppendLine("== CAMERAS / POST ==");
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var d = c.GetUniversalAdditionalCameraData();
                sb.AppendLine($"  {PathOf(c.transform)}: far {c.farClipPlane}  HDR {c.allowHDR}  MSAA {c.allowMSAA}  occlusion {c.useOcclusionCulling}  " +
                              $"post {(d ? d.renderPostProcessing.ToString() : "?")}  AA {(d ? d.antialiasing.ToString() : "?")}  depthTex {(d ? d.requiresDepthOption.ToString() : "?")}");
            }
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var p = v.sharedProfile;
                if (p == null) { sb.AppendLine($"  Volume {v.name}: no profile"); continue; }
                var on = p.components.Where(c => c.active && c.parameters.Any(x => x.overrideState)).Select(c => c.GetType().Name);
                sb.AppendLine($"  Volume {v.name} ({AssetDatabase.GetAssetPath(p)}): components with overrides: {string.Join(", ", on)}");
            }
            sb.AppendLine($"Canvases: {Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
            sb.AppendLine($"Occlusion culling data baked: {(StaticOcclusionCulling.umbraDataSize > 0)} ({StaticOcclusionCulling.umbraDataSize} bytes)");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------ textures

        static void Textures(StringBuilder sb, UnityEngine.SceneManagement.Scene scene)
        {
            var roots = new List<Object>(scene.GetRootGameObjects());
            if (RenderSettings.skybox) roots.Add(RenderSettings.skybox);
            var deps = EditorUtility.CollectDependencies(roots.ToArray());
            var texs = deps.OfType<Texture2D>().Distinct().ToArray();
            long total = 0;
            var rows = new List<(long bytes, string line)>();
            int big = 0, noOverride = 0;
            foreach (var t in texs)
            {
                string path = AssetDatabase.GetAssetPath(t);
                if (string.IsNullOrEmpty(path) || path.StartsWith("Packages/") || path.StartsWith("Resources/unity_builtin")) continue;
                long bytes = EstimateBytes(t);
                total += bytes;
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                string android = "-";
                if (imp != null)
                {
                    var s = imp.GetPlatformTextureSettings("Android");
                    android = s.overridden ? $"Android override {s.maxTextureSize} {s.format}" : $"default max {imp.maxTextureSize}";
                    if (!s.overridden) noOverride++;
                }
                if (Mathf.Max(t.width, t.height) > 1024) big++;
                rows.Add((bytes, $"  {t.width,5}x{t.height,-5} {t.format,-14} {bytes / 1048576f,6:0.0} MB  {android,-34} {path}"));
            }
            sb.AppendLine("== TEXTURES (used by this scene) ==");
            sb.AppendLine($"Textures: {rows.Count}   bigger than 1024: {big}   without Android override: {noOverride}   est. GPU memory: {total / 1048576f:0} MB");
            foreach (var r in rows.OrderByDescending(r => r.bytes).Take(30)) sb.AppendLine(r.line);
            sb.AppendLine();
        }

        static long EstimateBytes(Texture2D t)
        {
            float bpp;
            switch (t.format)
            {
                case TextureFormat.ASTC_4x4: bpp = 8f; break;
                case TextureFormat.ASTC_5x5: bpp = 5.12f; break;
                case TextureFormat.ASTC_6x6: bpp = 3.56f; break;
                case TextureFormat.ASTC_8x8: bpp = 2f; break;
                case TextureFormat.ASTC_10x10: bpp = 1.28f; break;
                case TextureFormat.ASTC_12x12: bpp = 0.89f; break;
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC2_RGB:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.DXT1: bpp = 4f; break;
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.DXT5:
                case TextureFormat.BC7: bpp = 8f; break;
                case TextureFormat.RGB24: bpp = 24f; break;
                case TextureFormat.Alpha8:
                case TextureFormat.R8: bpp = 8f; break;
                default: bpp = 32f; break;
            }
            float mips = t.mipmapCount > 1 ? 1.333f : 1f;
            return (long)(t.width * (long)t.height * bpp / 8f * mips);
        }

        // ------------------------------------------------------------------ audio

        static void Audio(StringBuilder sb, UnityEngine.SceneManagement.Scene scene)
        {
            var deps = EditorUtility.CollectDependencies(scene.GetRootGameObjects().Cast<Object>().ToArray());
            var clips = deps.OfType<AudioClip>().Distinct().ToArray();
            sb.AppendLine("== AUDIO ==");
            foreach (var c in clips)
            {
                var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(c)) as AudioImporter;
                string s = imp != null ? $"{imp.defaultSampleSettings.loadType} {imp.defaultSampleSettings.compressionFormat}" : "?";
                sb.AppendLine($"  {c.name}  {c.length:0.0}s  {s}");
            }
            if (clips.Length == 0) sb.AppendLine("  (none)");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------ settings

        static void Settings(StringBuilder sb)
        {
            sb.AppendLine("== QUALITY / URP ==");
            string[] names = QualitySettings.names;
            sb.AppendLine($"Quality levels: {string.Join(", ", names)}   current: {names[QualitySettings.GetQualityLevel()]}");
            for (int i = 0; i < names.Length; i++)
            {
                var rp = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
                if (rp == null) { sb.AppendLine($"  {names[i]}: no URP asset"); continue; }
                sb.AppendLine($"  {names[i]}: {rp.name}  scale {rp.renderScale}  HDR {rp.supportsHDR}  MSAA {rp.msaaSampleCount}  " +
                              $"shadows {rp.supportsMainLightShadows} {rp.mainLightShadowmapResolution}px dist {rp.shadowDistance} cascades {rp.shadowCascadeCount}  " +
                              $"addLights {rp.additionalLightsRenderingMode} max {rp.maxAdditionalLightsCount}  depthTex {rp.supportsCameraDepthTexture} opaqueTex {rp.supportsCameraOpaqueTexture}");
            }
            sb.AppendLine("== PLAYER (Android) ==");
            sb.AppendLine($"Backend {PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android)}  " +
                          $"archs {PlayerSettings.Android.targetArchitectures}  minSdk {PlayerSettings.Android.minSdkVersion}  " +
                          $"APIs {string.Join("/", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))} (auto {PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android)})  " +
                          $"framePacing {PlayerSettings.Android.optimizedFramePacing}  colorSpace {PlayerSettings.colorSpace}");
        }

        static Mesh MeshOf(Renderer r)
        {
            if (r is SkinnedMeshRenderer sk) return sk.sharedMesh;
            return r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (int i = 0; t != null && i < 6; i++, t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
