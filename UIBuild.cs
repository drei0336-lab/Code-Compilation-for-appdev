using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ARCHIV3D.EditorTools
{
    /// <summary>Small helpers for building uGUI objects from editor code.</summary>
    public static class UIBuild
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 0;
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            SetImage(img, sprite, color, raycast);
            return img;
        }

        public static void SetImage(Image img, Sprite sprite, Color color, bool raycast)
        {
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
            }
            else
            {
                img.type = UnityEngine.UI.Image.Type.Simple;
                img.preserveAspect = true;
            }
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, TMP_FontAsset font, float size,
            Color color, TextAlignmentOptions align)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            SetText(t, text, font, size, color, align);
            return t;
        }

        public static void SetText(TMP_Text t, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
        }

        public static void Destroy(GameObject go)
        {
            if (go != null) Undo.DestroyObjectImmediate(go);
        }
    }
}
