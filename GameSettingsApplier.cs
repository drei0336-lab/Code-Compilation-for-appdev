using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>Applies the title screen's settings (volume, look sensitivity) when the game scene starts.</summary>
    public class GameSettingsApplier : MonoBehaviour
    {
        void Start()
        {
            AudioListener.volume = GameSettings.Volume;

            float look = GameSettings.LookSensitivity;
            if (Mathf.Approximately(look, 1f)) return;

            var player = Object.FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            player.rotationSpeed *= look;
            player.touchSensitivity *= look;
        }
    }
}
