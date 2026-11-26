using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>
    /// Safety net for slow phones: if the frame rate stays under the target for a few seconds,
    /// render the 3D view at a slightly lower resolution (the UI stays sharp). If the phone has
    /// room to spare later, the resolution creeps back up.
    ///
    /// Only runs in builds on phones. In the Editor it does nothing, so your
    /// render pipeline assets are never changed while you test.
    /// Started by DevicePerformance; you don't need to add it to a scene.
    /// </summary>
    [AddComponentMenu("")]
    public class AdaptiveResolution : MonoBehaviour
    {
        /// <summary>Never go below this render scale.</summary>
        public static float MinScale = 0.5f;
        /// <summary>Seconds of frames averaged before each decision.</summary>
        public static float Window = 2f;

        static AdaptiveResolution s_Instance;
        static float s_MaxScale = 1f;
        static float s_Scale = 1f;

        float _time;
        int _frames;
        float _skip = 3f;      // ignore the first seconds after a scene load (loading hitches)
        float _calm;           // seconds in a row with frame time well inside budget
        readonly FrameTiming[] _timing = new FrameTiming[1];

        public static float CurrentScale => s_Scale;

        /// <summary>Starts (or restarts) at the given render scale, which is also the upper limit.</summary>
        public static void Begin(float maxScale)
        {
            s_MaxScale = Mathf.Clamp(maxScale, MinScale, 2f);
            s_Scale = s_MaxScale;
            SetScale(s_Scale);
            if (!Application.isMobilePlatform || Application.isEditor) return;
            if (s_Instance == null)
            {
                var go = new GameObject("ARCHIV3D Adaptive Resolution");
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(go);
                s_Instance = go.AddComponent<AdaptiveResolution>();
            }
            s_Instance.ResetWindow(3f);
        }

        public static void OnSceneLoaded()
        {
            if (s_Instance != null) s_Instance.ResetWindow(3f);
        }

        void ResetWindow(float skip)
        {
            _time = 0f;
            _frames = 0;
            _skip = skip;
            _calm = 0f;
        }

        static void SetScale(float scale)
        {
            // Phones only. In the Editor this would change the project's asset on disk.
            if (Application.isEditor || !Application.isMobilePlatform) return;
            var rp = DevicePerformance.CurrentAsset();
            if (rp != null) rp.renderScale = scale;
        }

        void Update()
        {
            if (FrameTimingManager.IsFeatureEnabled()) FrameTimingManager.CaptureFrameTimings();

            float dt = Time.unscaledDeltaTime;
            if (_skip > 0f) { _skip -= dt; return; }
            if (dt > 0.5f) return;  // app paused / scene loading; not a real frame

            _time += dt;
            _frames++;
            if (_time < Window) return;

            float avgMs = _time / _frames * 1000f;
            _time = 0f;
            _frames = 0;

            int fps = Application.targetFrameRate > 0 ? Application.targetFrameRate : 30;
            float budgetMs = 1000f / fps;

            if (avgMs > budgetMs * 1.2f && s_Scale > MinScale + 0.001f)
            {
                // Too slow: step the 3D resolution down and look again after a pause.
                s_Scale = Mathf.Max(MinScale, s_Scale - 0.1f);
                SetScale(s_Scale);
                _skip = 1f;
                _calm = 0f;
                return;
            }

            if (avgMs <= budgetMs * 1.05f && s_Scale < s_MaxScale - 0.001f && HasHeadroom(budgetMs))
            {
                _calm += Window;
                if (_calm >= 20f)
                {
                    s_Scale = Mathf.Min(s_MaxScale, s_Scale + 0.05f);
                    SetScale(s_Scale);
                    _calm = 0f;
                }
            }
            else _calm = 0f;
        }

        /// <summary>
        /// True if the GPU and CPU finish well before the frame is due. Needs Frame Timing Stats
        /// (Player Settings); without them the resolution only ever goes down, never back up.
        /// </summary>
        bool HasHeadroom(float budgetMs)
        {
            if (!FrameTimingManager.IsFeatureEnabled()) return false;
            if (FrameTimingManager.GetLatestTimings(1, _timing) < 1) return false;
            double gpu = _timing[0].gpuFrameTime, cpu = _timing[0].cpuMainThreadFrameTime;
            if (gpu <= 0.0) return false;
            return gpu < budgetMs * 0.65 && cpu < budgetMs * 0.75;
        }
    }
}
