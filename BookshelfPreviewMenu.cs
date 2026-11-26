using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Preview Bookshelf (Play mode): while the game is playing, opens a year's bookshelf
    /// (semesters > subjects > books) straight away, so you can check new books without walking to the shelf.
    /// </summary>
    static class BookshelfPreviewMenu
    {
        const string Root = "Tools/ARCHIV3D/Preview Bookshelf (Play mode)/";

        [MenuItem(Root + "1st Year", false, 40)] static void Year1() => OpenYear(1);
        [MenuItem(Root + "2nd Year", false, 41)] static void Year2() => OpenYear(2);
        [MenuItem(Root + "3rd Year", false, 42)] static void Year3() => OpenYear(3);
        [MenuItem(Root + "Selected Bookshelf", false, 60)] static void Selected() => OpenReader(Selection.activeGameObject
            ? Selection.activeGameObject.GetComponentInParent<BookshelfReader>() : null, "the selected object");

        [MenuItem(Root + "1st Year", true)] static bool CanYear1() => Application.isPlaying;
        [MenuItem(Root + "2nd Year", true)] static bool CanYear2() => Application.isPlaying;
        [MenuItem(Root + "3rd Year", true)] static bool CanYear3() => Application.isPlaying;
        [MenuItem(Root + "Selected Bookshelf", true)] static bool CanSelected() => Application.isPlaying;

        static void OpenYear(int year)
        {
            BookshelfReader reader = null;
            foreach (var shelf in Object.FindObjectsByType<YearBookshelf>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (shelf.yearLevel != year) continue;
                reader = shelf.GetComponent<BookshelfReader>();
                if (reader != null) break;
            }
            OpenReader(reader, "the " + YearBookshelf.Ordinal(year) + " year bookshelf");
        }

        static void OpenReader(BookshelfReader reader, string what)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Press Play first, then use this menu.", "OK");
                return;
            }
            if (reader == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find a Bookshelf Reader on " + what + ". " +
                    "Run Tools > ARCHIV3D > Set Up Year Subject Shelves first.", "OK");
                return;
            }
            if (reader.IsBookOpen)
            {
                Debug.Log("[ARCHIV3D] A book is already open. Close it first.", reader);
                return;
            }
            reader.Open();
            Debug.Log("[ARCHIV3D] Opened " + what + " (" + reader.name + ").", reader);
        }
    }
}
