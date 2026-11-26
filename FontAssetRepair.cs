using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Repair Broken Font Assets
    ///
    /// The game's TextMeshPro fonts are "dynamic": Unity writes new letters into them whenever text is shown,
    /// so every group member's Unity changes these files. When Unity Version Control merges two of those
    /// versions it can stitch them together into one file with the same data two or three times. Unity then
    /// shows "The image data of Texture '... SDF Atlas' has an unexpected size" and text in that font can
    /// disappear.
    ///
    /// This finds font assets with that damage and resets them with TextMeshPro's own "Clear Dynamic Data"
    /// (the same clean state Unity leaves them in after every build). The letters come back by themselves
    /// the next time text in that font is shown. Check the repaired fonts in so the rest of the group gets them.
    /// </summary>
    public static class FontAssetRepair
    {
        [MenuItem("Tools/ARCHIV3D/Repair Broken Font Assets", false, 70)]
        public static void RepairMenu()
        {
            var repaired = Repair(out var checkedCount);
            string msg = repaired.Count == 0
                ? "Checked " + checkedCount + " font assets: none are damaged."
                : "Repaired " + repaired.Count + " of " + checkedCount + " font assets:\n- " + string.Join("\n- ", repaired) +
                  "\n\nThey're reset to a clean state; their letters come back by themselves when text is shown. " +
                  "Check them in (Unity Version Control) so the rest of your group gets the fixed files.";
            Debug.Log("[ARCHIV3D] " + msg);
            EditorUtility.DisplayDialog("ARCHIV3D: Font Assets", msg, "OK");
        }

        /// <summary>Repairs every damaged TextMeshPro font asset in the project. Returns their paths.</summary>
        public static List<string> Repair(out int checkedCount)
        {
            var repaired = new List<string>();
            checkedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") || !path.EndsWith(".asset")) continue;
                checkedCount++;
                if (!IsDamaged(path)) continue;

                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null) continue;

                font.ClearFontAssetData(true);
                EditorUtility.SetDirty(font);
                if (font.atlasTextures != null)
                    foreach (var tex in font.atlasTextures)
                        if (tex != null) EditorUtility.SetDirty(tex);
                if (font.material != null) EditorUtility.SetDirty(font.material);
                AssetDatabase.SaveAssetIfDirty(font);
                repaired.Add(path);
            }
            if (repaired.Count > 0) AssetDatabase.SaveAssets();
            return repaired;
        }

        /// <summary>
        /// True when the file has the same setting written more than once inside one object, which Unity
        /// itself never does (it happens when a merge keeps both sides).
        /// </summary>
        public static bool IsDamaged(string assetPath)
        {
            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "", assetPath);
            if (!File.Exists(full)) return false;
            string text = File.ReadAllText(full);
            if (text.Contains("\n<<<<<<<") || text.Contains("\n>>>>>>>")) return true;   // left-over conflict markers

            foreach (var doc in Regex.Split(text, @"\n(?=--- !u!)"))
            {
                var seen = new HashSet<string>();
                foreach (Match m in Regex.Matches(doc, @"^  ([A-Za-z_][\w ]*):", RegexOptions.Multiline))
                    if (!seen.Add(m.Groups[1].Value)) return true;
            }
            return false;
        }
    }
}
