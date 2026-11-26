using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// If you preview Graphics: Low in Play mode, the game switches the quality level.
    /// This puts the Editor's quality level back when Play mode ends.
    /// </summary>
    [InitializeOnLoad]
    static class PlayModeQualityRestore
    {
        const string Key = "ARCHIV3D.QualityBeforePlay";

        static PlayModeQualityRestore()
        {
            EditorApplication.playModeStateChanged += OnChange;
        }

        static void OnChange(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                SessionState.SetInt(Key, QualitySettings.GetQualityLevel());
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                int level = SessionState.GetInt(Key, -1);
                if (level >= 0 && level < QualitySettings.names.Length && level != QualitySettings.GetQualityLevel())
                    QualitySettings.SetQualityLevel(level, true);
            }
        }
    }

    /// <summary>
    /// Tools > ARCHIV3D > Preview Graphics: pick which phone tier Play mode simulates
    /// (same as Settings > Graphics on the title screen). Auto = this PC, so the Editor's own quality.
    /// </summary>
    static class GraphicsPreviewMenu
    {
        const string Root = "Tools/ARCHIV3D/Preview Graphics In Play Mode/";

        [MenuItem(Root + "Auto (this PC)", false, 60)] static void Auto() => Pick(GraphicsChoice.Auto);
        [MenuItem(Root + "Low (cheap phone)", false, 61)] static void Low() => Pick(GraphicsChoice.Low);
        [MenuItem(Root + "Medium (mid-range phone)", false, 62)] static void Medium() => Pick(GraphicsChoice.Medium);
        [MenuItem(Root + "High (flagship phone)", false, 63)] static void High() => Pick(GraphicsChoice.High);

        [MenuItem(Root + "Auto (this PC)", true)] static bool AutoV() => Check("Auto (this PC)", GraphicsChoice.Auto);
        [MenuItem(Root + "Low (cheap phone)", true)] static bool LowV() => Check("Low (cheap phone)", GraphicsChoice.Low);
        [MenuItem(Root + "Medium (mid-range phone)", true)] static bool MediumV() => Check("Medium (mid-range phone)", GraphicsChoice.Medium);
        [MenuItem(Root + "High (flagship phone)", true)] static bool HighV() => Check("High (flagship phone)", GraphicsChoice.High);

        static GraphicsChoice Current => (GraphicsChoice)Mathf.Clamp(PlayerPrefs.GetInt(GameSettings.GraphicsKey, 0), 0, 3);

        static bool Check(string item, GraphicsChoice c)
        {
            Menu.SetChecked(Root + item, Current == c);
            return true;
        }

        static void Pick(GraphicsChoice c)
        {
            if (EditorApplication.isPlaying) GameSettings.Graphics = c;   // applies right away
            else PlayerPrefs.SetInt(GameSettings.GraphicsKey, (int)c);
            PlayerPrefs.Save();
            Debug.Log("[ARCHIV3D] Play mode will use Graphics: " + c + ".");
        }
    }
}
