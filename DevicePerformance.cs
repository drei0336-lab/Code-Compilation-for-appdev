using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ARCHIV3D
{
    /// <summary>What the player picked in Settings > Graphics.</summary>
    public enum GraphicsChoice { Auto = 0, Low = 1, Medium = 2, High = 3 }

    /// <summary>How hard the game pushes the phone.</summary>
    public enum GraphicsTier { Low = 0, Medium = 1, High = 2 }

    /// <summary>
    /// Picks graphics settings that suit the phone the game runs on, before the first scene loads.
    ///
    /// Low:    "Mobile Low" quality level (lower resolution, no shadows, half-size textures,
    ///         shorter tree distance), no post-processing, 30 fps.
    /// Medium: "Mobile" quality level, 30 fps.
    /// High:   "Mobile" quality level at full resolution, 60 fps.
    ///
    /// Auto (the default) picks the tier from the phone's memory, CPU cores and GPU.
    /// Players can override it on the title screen (Settings > Graphics).
    /// On top of that, AdaptiveResolution lowers the resolution a little if the frame rate still drops.
    /// </summary>
    public static class DevicePerformance
    {
        public const string LowQualityLevel = "Mobile Low";
        public const string NormalQualityLevel = "Mobile";

        /// <summary>The tier Auto picks for this device.</summary>
        public static GraphicsTier DetectedTier { get; private set; } = GraphicsTier.High;
        /// <summary>The tier in use right now.</summary>
        public static GraphicsTier Tier { get; private set; } = GraphicsTier.High;
        /// <summary>One line describing the device, for logs.</summary>
        public static string DeviceSummary { get; private set; } = "";

        public static event Action<GraphicsTier> TierChanged;

        // Render scale of each pipeline asset as it was authored, so tiers can go back to it.
        static readonly Dictionary<UniversalRenderPipelineAsset, float> s_AuthoredScale = new Dictionary<UniversalRenderPipelineAsset, float>();
        // Post-processing state of cameras that Low switched off, so Medium/High can switch it back on.
        static readonly Dictionary<UniversalAdditionalCameraData, bool> s_PostWas = new Dictionary<UniversalAdditionalCameraData, bool>();

        // Quality level in use when the game started (the Editor keeps using it unless Low is picked).
        static int s_StartLevel = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            s_StartLevel = QualitySettings.GetQualityLevel();
            s_AuthoredScale.Clear();
            s_PostWas.Clear();
            DetectedTier = Detect(out string summary);
            DeviceSummary = summary;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(GameSettings.Graphics);
            Debug.Log($"[ARCHIV3D] Device: {DeviceSummary} -> auto tier {DetectedTier}, using {Tier} " +
                      $"(quality level \"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\").");
        }

        // ------------------------------------------------------------------ detection

        /// <summary>Guesses how strong this device is. Desktop and the Editor count as High.</summary>
        public static GraphicsTier Detect(out string summary)
        {
            int ram = SystemInfo.systemMemorySize;          // MB
            int cores = SystemInfo.processorCount;
            int mhz = SystemInfo.processorFrequency;
            string gpu = SystemInfo.graphicsDeviceName ?? "";
            int shaderLevel = SystemInfo.graphicsShaderLevel;
            summary = $"{SystemInfo.deviceModel} | RAM {ram} MB | {cores} cores @ {mhz} MHz | {gpu} ({SystemInfo.graphicsDeviceType}, SM{shaderLevel})";

            if (!Application.isMobilePlatform) return GraphicsTier.High;

            string g = gpu.ToLowerInvariant();
            bool weakGpu = ContainsAny(g, WeakGpus);
            bool strongGpu = ContainsAny(g, StrongGpus);

            // 3 GB phones report about 2.7-2.9 GB, 4 GB phones about 3.6-3.8 GB.
            if ((ram > 0 && ram <= 3300) || cores <= 4 || shaderLevel < 45 || weakGpu) return GraphicsTier.Low;
            if (ram >= 7000 && cores >= 8 && strongGpu) return GraphicsTier.High;
            return GraphicsTier.Medium;
        }

        // Entry-level phone GPUs (GL_RENDERER / Vulkan device names, lower case).
        static readonly string[] WeakGpus =
        {
            "mali-4", "mali-t", "mali-g31", "mali-g51", "mali-g52", "mali-g57 mc1",
            "adreno (tm) 3", "adreno (tm) 4", "adreno (tm) 50", "adreno (tm) 51",
            "adreno (tm) 610", "adreno (tm) 612", "adreno (tm) 613",
            "powervr rogue ge", "powervr rogue g6", "powervr rogue gm", "bxe", "img bxm-4",
            "vivante", "videocore", "tegra 3", "tegra 4",
        };

        // Recent flagship GPUs.
        static readonly string[] StrongGpus =
        {
            "adreno (tm) 64", "adreno (tm) 65", "adreno (tm) 66", "adreno (tm) 69", "adreno (tm) 7", "adreno (tm) 8",
            "mali-g7", "immortalis", "xclipse", "apple",
        };

        static bool ContainsAny(string s, string[] parts)
        {
            foreach (var p in parts) if (s.Contains(p)) return true;
            return false;
        }

        // ------------------------------------------------------------------ applying

        public static GraphicsTier TierFor(GraphicsChoice choice) =>
            choice == GraphicsChoice.Auto ? DetectedTier : (GraphicsTier)((int)choice - 1);

        /// <summary>Switches to the settings for a choice (Auto = the detected tier).</summary>
        public static void Apply(GraphicsChoice choice)
        {
            Tier = TierFor(choice);

            // Low -> "Mobile Low", Medium/High -> "Mobile".
            // In the Editor, Auto keeps the Editor's own level; picking Low/Medium/High previews that phone tier
            // (the Editor's level is put back when Play mode stops).
            // Desktop builds don't include the Mobile levels, so nothing changes there.
            int level;
            if (Application.isEditor && choice == GraphicsChoice.Auto) level = s_StartLevel;
            else level = FindLevel(Tier == GraphicsTier.Low ? LowQualityLevel : NormalQualityLevel);
            if (level >= 0 && level != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(level, true);

            // Phones default to 30 fps; only High asks for 60. 30 fps also keeps cheap phones cooler.
            if (Application.isMobilePlatform)
                Application.targetFrameRate = Tier == GraphicsTier.High ? 60 : 30;

            float scale = AuthoredRenderScale();
            if (Tier == GraphicsTier.High) scale = 1f;
            AdaptiveResolution.Begin(scale);

            ApplyToLoadedScenes();
            TierChanged?.Invoke(Tier);
        }

        static int FindLevel(string name)
        {
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>The render scale the active pipeline asset was set up with (before any runtime change).</summary>
        public static float AuthoredRenderScale()
        {
            var rp = CurrentAsset();
            if (rp == null) return 1f;
            if (!s_AuthoredScale.TryGetValue(rp, out float s))
            {
                s = rp.renderScale;
                s_AuthoredScale[rp] = s;
            }
            return s;
        }

        public static UniversalRenderPipelineAsset CurrentAsset() =>
            (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyToScene(scene);
            AdaptiveResolution.OnSceneLoaded();
        }

        static void ApplyToLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.isLoaded) ApplyToScene(s);
            }
        }

        // ------------------------------------------------------------------ small-object culling

        /// <summary>
        /// Low/Medium: small props stop drawing when they are smaller than this fraction of the screen height
        /// (before the quality level's LOD Bias: "Mobile Low" uses 0.6, so there it's about 2%).
        /// About 1.2% means a pile of books disappears at roughly 29 m on Medium and 17 m on Low.
        /// </summary>
        public static float CullScreenHeight = 0.012f;
        /// <summary>Only objects smaller than this (bounds diagonal, metres) are culled that way.</summary>
        public static float CullMaxSize = 2.5f;
        /// <summary>Medium: objects smaller than this don't cast shadows (Low has no shadows at all).</summary>
        public static float NoShadowMaxSize = 0.8f;

        static void CullSmallObjects(GameObject root, bool crossFade, bool smallShadowsOff)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponentInParent<LODGroup>(true) != null) continue;   // already has LODs
                if (r.GetComponentInParent<Canvas>(true) != null) continue;
                float size = r.bounds.size.magnitude;
                if (size <= 0f) continue;

                if (smallShadowsOff && size < NoShadowMaxSize && r.shadowCastingMode == ShadowCastingMode.On)
                    r.shadowCastingMode = ShadowCastingMode.Off;

                if (size >= CullMaxSize) continue;
                var group = r.gameObject.AddComponent<LODGroup>();
                group.fadeMode = crossFade ? LODFadeMode.CrossFade : LODFadeMode.None;
                group.animateCrossFading = crossFade;
                group.SetLODs(new[] { new LOD(CullScreenHeight, new Renderer[] { r }) });
                group.RecalculateBounds();
            }
        }

        static void LighterBloom(GameObject root)
        {
            foreach (var v in root.GetComponentsInChildren<Volume>(true))
            {
                if (v.sharedProfile == null) continue;
                // .profile makes a runtime copy, so the asset itself isn't changed.
                if (v.profile.TryGet(out Bloom bloom))
                {
                    bloom.highQualityFiltering.Override(false);
                    bloom.downscale.Override(BloomDownscaleMode.Quarter);
                    bloom.maxIterations.Override(Mathf.Min(bloom.maxIterations.value, 4));
                }
            }
        }

        /// <summary>Per-scene changes that a quality level can't make (cameras, props, terrain).</summary>
        static void ApplyToScene(Scene scene)
        {
            bool low = Tier == GraphicsTier.Low;
            bool phoneTier = Tier != GraphicsTier.High;
            var rp = CurrentAsset();
            bool crossFade = rp != null && rp.enableLODCrossFade;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (phoneTier)
                {
                    CullSmallObjects(root, crossFade, smallShadowsOff: !low);
                    if (!low) LighterBloom(root);
                }

                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                {
                    var data = cam.GetUniversalAdditionalCameraData();
                    if (data == null) continue;
                    if (low)
                    {
                        if (!s_PostWas.ContainsKey(data)) s_PostWas[data] = data.renderPostProcessing;
                        data.renderPostProcessing = false;   // one less full-screen pass
                        data.antialiasing = AntialiasingMode.None;
                        cam.allowHDR = false;
                        cam.allowMSAA = false;
                    }
                    else if (s_PostWas.TryGetValue(data, out bool was))
                    {
                        data.renderPostProcessing = was;
                        s_PostWas.Remove(data);
                    }
                }

                if (!low) continue;
                foreach (var t in root.GetComponentsInChildren<Terrain>(true))
                {
                    // Backup in case the "Mobile Low" quality level's terrain limits aren't set up.
                    t.treeDistance = Mathf.Min(t.treeDistance, 150f);
                    t.treeBillboardDistance = Mathf.Min(t.treeBillboardDistance, 30f);
                    t.treeMaximumFullLODCount = Mathf.Min(t.treeMaximumFullLODCount, 15);
                    t.detailObjectDistance = Mathf.Min(t.detailObjectDistance, 35f);
                    t.heightmapPixelError = Mathf.Max(t.heightmapPixelError, 12f);
                    t.basemapDistance = Mathf.Min(t.basemapDistance, 150f);
                }
            }
        }
    }
}
