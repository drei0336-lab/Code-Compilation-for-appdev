using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>Slowly turns the title screen camera around the captured library panorama.</summary>
    public class PanoramaCamera : MonoBehaviour
    {
        [Tooltip("Turning speed in degrees per second.")]
        public float degreesPerSecond = 2.5f;
        [Tooltip("Which way the camera faces when the title screen opens.")]
        public float startYaw = 0f;
        [Tooltip("Negative looks up, positive looks down.")]
        public float pitch = -4f;
        public float pitchWobble = 2f;
        public float wobbleSeconds = 14f;

        float _yaw;

        void OnEnable() { _yaw = startYaw; Apply(); }

        void Update()
        {
            _yaw = Mathf.Repeat(_yaw + degreesPerSecond * Time.unscaledDeltaTime, 360f);
            Apply();
        }

        void Apply()
        {
            float wobble = wobbleSeconds > 0f ? Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / wobbleSeconds) * pitchWobble : 0f;
            transform.rotation = Quaternion.Euler(pitch + wobble, _yaw, 0f);
        }
    }
}
