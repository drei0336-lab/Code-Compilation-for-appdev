using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>Keeps this RectTransform inside the phone's safe area (away from notches and rounded corners).</summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rt;
        Rect _last;
        Vector2Int _lastScreen;

        void OnEnable() { _rt = (RectTransform)transform; Apply(); }

        void Update()
        {
            if (Screen.safeArea != _last || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y) Apply();
        }

        void Apply()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            _last = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            if (!Application.isPlaying)
            {
                // In the editor just fill the canvas so layouts are easy to see.
                _rt.anchorMin = Vector2.zero;
                _rt.anchorMax = Vector2.one;
            }
            else
            {
                Vector2 min = _last.position;
                Vector2 max = _last.position + _last.size;
                _rt.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
                _rt.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            }
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
