using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Books > Add Math Symbols Font
    ///
    /// Lora and Outfit don't have logic, set and arrow symbols (for example the ones for and, or, not, implies,
    /// for all, there exists, element of, union, intersection, subset, empty set, equivalent). Without help,
    /// TextMeshPro shows them as empty boxes. This makes "Math Symbols SDF" from DejaVuSans-MathSymbols.ttf (a small
    /// part of the free DejaVu Sans font, see DejaVuSans-LICENSE.txt) and adds it as a fallback to every font asset
    /// in Assets/ARCHIV3D/Fonts and to TextMeshPro's default font (used by the BlackCompu quiz screen).
    /// TextMeshPro then draws only the missing symbols with it; everything else keeps
    /// its own font. The Discrete Mathematics book tool runs this for you. Safe to run again.
    /// </summary>
    public static class SymbolFontSetup
    {
        const string TtfName = "DejaVuSans-MathSymbols";
        const string AssetName = "Math Symbols SDF";

        static string TtfPath => ArchiveAssets.FontDir + "/" + TtfName + ".ttf";
        static string AssetPath => ArchiveAssets.FontDir + "/" + AssetName + ".asset";

        [MenuItem("Tools/ARCHIV3D/Books/Add Math Symbols Font", false, 70)]
        static void Menu()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then run this again.", "OK");
                return;
            }
            int added = Ensure(out string error);
            EditorUtility.DisplayDialog("ARCHIV3D",
                error ?? (added == 0
                    ? "The math symbols font is already a fallback of every ARCHIV3D font."
                    : "Added the math symbols font as a fallback to " + added + " ARCHIV3D font(s)."),
                "OK");
        }

        /// <summary>
        /// Makes the symbols font asset if needed and adds it as a fallback to every font asset in Assets/ARCHIV3D/Fonts.
        /// Returns how many font assets got it; <paramref name="error"/> is set if it couldn't be done.
        /// </summary>
        public static int Ensure(out string error)
        {
            error = null;
            var symbols = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            if (symbols == null)
            {
                symbols = Create(out error);
                if (symbols == null) return 0;
            }

            int added = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { ArchiveAssets.FontDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == AssetPath) continue;
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fontAsset == null) continue;
                if (fontAsset.fallbackFontAssetTable == null) fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (fontAsset.fallbackFontAssetTable.Contains(symbols)) continue;

                Undo.RecordObject(fontAsset, "Add math symbols fallback");
                fontAsset.fallbackFontAssetTable.Add(symbols);
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssetIfDirty(fontAsset);
                added++;
            }
            // TextMeshPro's default font (LiberationSans SDF): used by text with no font set, such as the
            // BlackCompu quiz screen, so quiz questions with logic and set symbols show them too.
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null && defaultFont != symbols)
            {
                if (defaultFont.fallbackFontAssetTable == null) defaultFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!defaultFont.fallbackFontAssetTable.Contains(symbols))
                {
                    Undo.RecordObject(defaultFont, "Add math symbols fallback");
                    defaultFont.fallbackFontAssetTable.Add(symbols);
                    EditorUtility.SetDirty(defaultFont);
                    AssetDatabase.SaveAssetIfDirty(defaultFont);
                    added++;
                }
            }

            if (added > 0) Debug.Log("[ARCHIV3D] Added " + AssetName + " as a fallback to " + added + " font asset(s).");
            return added;
        }

        static TMP_FontAsset Create(out string error)
        {
            error = null;
            var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
            if (font == null)
            {
                error = "Missing " + TtfPath + ", so math symbols may show as boxes.";
                return null;
            }
            try
            {
                var fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                if (fontAsset == null)
                {
                    error = "Couldn't make " + AssetName + " from " + TtfPath + ".";
                    return null;
                }
                fontAsset.name = AssetName;
                AssetDatabase.CreateAsset(fontAsset, AssetPath);
                if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
                {
                    fontAsset.atlasTextures[0].name = AssetName + " Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
                }
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = AssetName + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath);
                var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
                if (saved == null) error = "Couldn't save " + AssetPath + ".";
                return saved;
            }
            catch (System.Exception e)
            {
                error = "Couldn't make " + AssetName + ": " + e.Message;
                if (AssetDatabase.LoadMainAssetAtPath(AssetPath) != null) AssetDatabase.DeleteAsset(AssetPath);
                return null;
            }
        }
    }
}
