using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>Player settings saved on the device (set on the title screen, used in the game).</summary>
    public static class GameSettings
    {
        const string VolumeKey = "ARCHIV3D.Volume";
        const string LookKey = "ARCHIV3D.LookSensitivity";
        public const string GraphicsKey = "ARCHIV3D.Graphics";

        public const float MinLook = 0.25f;
        public const float MaxLook = 3f;

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set
            {
                float v = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, v);
                AudioListener.volume = v;
            }
        }

        /// <summary>1 = normal speed.</summary>
        public static float LookSensitivity
        {
            get => PlayerPrefs.GetFloat(LookKey, 1f);
            set => PlayerPrefs.SetFloat(LookKey, Mathf.Clamp(value, MinLook, MaxLook));
        }

        /// <summary>Graphics quality. Auto picks Low / Medium / High for the device (see DevicePerformance).</summary>
        public static GraphicsChoice Graphics
        {
            get => (GraphicsChoice)Mathf.Clamp(PlayerPrefs.GetInt(GraphicsKey, 0), 0, 3);
            set
            {
                PlayerPrefs.SetInt(GraphicsKey, (int)value);
                DevicePerformance.Apply(value);
            }
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
