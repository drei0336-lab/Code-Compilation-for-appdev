using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ARCHIV3D
{
    /// <summary>
    /// First-person body: the player's own camera sees only the character's arms and hands, while every other
    /// view (the Scene tab, prefab and inspector previews) shows the whole character as usual.
    ///
    /// Just before the player's camera draws, the full body is switched to "Shadows Only" (not drawn, but it still
    /// casts the player's shadow) and the arms-only copies ("FP Arms - ...") are drawn instead. Right after, everything
    /// goes back, so the scene file never changes. Set up by Tools > ARCHIV3D > Set Up First-Person View (arms only).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("ARCHIV3D/First-Person Body")]
    public class FirstPersonBody : MonoBehaviour
    {
        [Tooltip("The player's camera. Only this camera shows the arms-only view. Empty = the camera inside this character.")]
        public Camera playerCamera;

        [Tooltip("The whole body (Body, Bottoms, Shoes, Hair...). The player's camera doesn't draw them, but they still " +
                 "cast the player's shadow. The Scene tab shows them normally.")]
        public List<Renderer> bodyRenderers = new List<Renderer>();

        [Tooltip("The arms-only copies (FP Arms - ...). Only the player's camera draws them.")]
        public List<Renderer> armsRenderers = new List<Renderer>();

        readonly List<ShadowCastingMode> _savedModes = new List<ShadowCastingMode>();
        bool _applied;
        bool _firstPerson;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
            Camera.onPreCull += OnPreCullBuiltIn;
            Camera.onPostRender += OnPostRenderBuiltIn;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            Camera.onPreCull -= OnPreCullBuiltIn;
            Camera.onPostRender -= OnPostRenderBuiltIn;
            Restore();
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam) => Apply(cam);
        void OnEndCamera(ScriptableRenderContext context, Camera cam) => Restore();

        // Only used if the project ever runs without URP (the built-in render pipeline)
        void OnPreCullBuiltIn(Camera cam) { if (GraphicsSettings.currentRenderPipeline == null) Apply(cam); }
        void OnPostRenderBuiltIn(Camera cam) { if (GraphicsSettings.currentRenderPipeline == null) Restore(); }

        bool IsPlayerCamera(Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game) return false;
            if (playerCamera != null) return cam == playerCamera;
            return cam.transform.IsChildOf(transform);
        }

        void Apply(Camera cam)
        {
            Restore();
            _firstPerson = IsPlayerCamera(cam);
            if (_firstPerson)
            {
                _savedModes.Clear();
                foreach (var r in bodyRenderers)
                {
                    _savedModes.Add(r != null ? r.shadowCastingMode : ShadowCastingMode.On);
                    if (r != null && r.shadowCastingMode != ShadowCastingMode.Off) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    else if (r != null) r.forceRenderingOff = true;
                }
            }
            else
            {
                foreach (var r in armsRenderers)
                    if (r != null) r.forceRenderingOff = true;
            }
            _applied = true;
        }

        void Restore()
        {
            if (!_applied) return;
            _applied = false;
            if (_firstPerson)
            {
                for (int i = 0; i < bodyRenderers.Count; i++)
                {
                    var r = bodyRenderers[i];
                    if (r == null) continue;
                    if (i < _savedModes.Count) r.shadowCastingMode = _savedModes[i];
                    r.forceRenderingOff = false;
                }
            }
            else
            {
                foreach (var r in armsRenderers)
                    if (r != null) r.forceRenderingOff = false;
            }
        }
    }
}
