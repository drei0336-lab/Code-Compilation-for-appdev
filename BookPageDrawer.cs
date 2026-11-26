using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>Shows each book page in the Inspector as "Page 3: heading" with its fields underneath.</summary>
    [CustomPropertyDrawer(typeof(BookPage))]
    public class BookPageDrawer : PropertyDrawer
    {
        static readonly string[] Fields = { "heading", "text", "picture" };
        static float Line => EditorGUIUtility.singleLineHeight;
        static float Gap => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = Line;
            if (!property.isExpanded) return h;
            foreach (var f in Fields)
                h += Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(f), true);
            return h + Gap;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, Title(property, label), true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = row.yMax + Gap;
                foreach (var f in Fields)
                {
                    var p = property.FindPropertyRelative(f);
                    float h = EditorGUI.GetPropertyHeight(p, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, h), p, true);
                    y += h + Gap;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        static GUIContent Title(SerializedProperty property, GUIContent label)
        {
            var m = Regex.Match(property.propertyPath, @"\[(\d+)\]$");
            string prefix = m.Success ? "Page " + (int.Parse(m.Groups[1].Value) + 1) : label.text;
            string heading = property.FindPropertyRelative("heading").stringValue;
            if (string.IsNullOrWhiteSpace(heading))
            {
                heading = property.FindPropertyRelative("text").stringValue;
                heading = string.IsNullOrWhiteSpace(heading) ? "(empty page)" : heading;
            }
            heading = heading.Replace('\n', ' ').Trim();
            if (heading.Length > 50) heading = heading.Substring(0, 47) + "...";
            return new GUIContent(prefix + ":  " + heading, label.tooltip);
        }
    }
}
