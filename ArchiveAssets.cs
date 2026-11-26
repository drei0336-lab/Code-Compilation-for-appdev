using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ARCHIV3D.EditorTools
{
    /// <summary>Loads the ARCHIV3D sprites (setting their import options) and makes the TextMeshPro fonts.</summary>
    public static class ArchiveAssets
    {
        public const string Root = "Assets/ARCHIV3D";
        public const string SpriteDir = Root + "/UI/Sprites";
        public const string FontDir = Root + "/Fonts";

        // Colors shared by the HUD and the title screen
        public static readonly Color Gold = new Color32(240, 190, 84, 255);
        public static readonly Color GoldLight = new Color32(255, 226, 150, 255);
        public static readonly Color Ink = new Color32(16, 13, 11, 255);
        public static readonly Color Walnut = new Color32(34, 24, 16, 255);
        public static readonly Color Cream = new Color32(255, 246, 224, 255);

        public static Sprite JoystickBase => Sprite("joystick_base", Vector4.zero);
        public static Sprite JoystickKnob => Sprite("joystick_knob", Vector4.zero);
        public static Sprite ButtonDark => Sprite("button_dark", Vector4.zero);
        public static Sprite ButtonGold => Sprite("button_gold", Vector4.zero);
        public static Sprite IconRun => Sprite("icon_run", Vector4.zero);
        public static Sprite IconHand => Sprite("icon_hand", Vector4.zero);
        public static Sprite Crosshair => Sprite("crosshair", Vector4.zero);
        public static Sprite RoundedFill => Sprite("rounded_fill", new Vector4(32, 32, 32, 32));
        public static Sprite RoundedBorder => Sprite("rounded_border", new Vector4(32, 32, 32, 32));
        public static Sprite Pill => Sprite("pill", new Vector4(32, 32, 32, 32));
        public static Sprite Vignette => Sprite("vignette", Vector4.zero);
        public static Sprite GradientBottom => Sprite("gradient_bottom", Vector4.zero);
        public static Sprite Logo => Sprite("archiv3d_logo", Vector4.zero);

        public static TMP_FontAsset PixelFont => Font("PixelifySans-Medium", "Pixelify SDF");
        public static TMP_FontAsset BoldFont => Font("Outfit-Bold", "Outfit Bold SDF");
        public static TMP_FontAsset RegularFont => Font("Outfit-Regular", "Outfit Regular SDF");

        public static Sprite Sprite(string file, Vector4 border)
        {
            string path = SpriteDir + "/" + file + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[ARCHIV3D] Missing sprite " + path);
                return null;
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }
            if (importer.spriteBorder != border) { importer.spriteBorder = border; changed = true; }
            if (changed) importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static TMP_FontAsset Font(string ttfName, string assetName)
        {
            string assetPath = FontDir + "/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/" + ttfName + ".ttf");
            if (font == null)
            {
                Debug.LogWarning("[ARCHIV3D] Missing font file " + ttfName + ".ttf; using the default TextMeshPro font.");
                return Fallback();
            }

            try
            {
                var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
                if (fa == null) return Fallback();
                fa.name = assetName;
                AssetDatabase.CreateAsset(fa, assetPath);

                if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
                {
                    fa.atlasTextures[0].name = assetName + " Atlas";
                    AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                }
                if (fa.material != null)
                {
                    fa.material.name = assetName + " Material";
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
                }
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath);
                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ARCHIV3D] Couldn't make font asset " + assetName + ": " + e.Message + ". Using the default TextMeshPro font.");
                if (File.Exists(assetPath)) AssetDatabase.DeleteAsset(assetPath);
                return Fallback();
            }
        }

        public static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        static TMP_FontAsset Fallback()
        {
            if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null) return Fallback();
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }
    }
}
