using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Set Up Bookshelf Books.
    /// Gives every year-section bookshelf in Main Map its own book component
    /// ("1st year Section" = Year1Book, "2nd year Section" = Year2Book, ...), all sharing the same logic,
    /// and makes the shelf open the book when tapped / clicked (like the computers).
    /// </summary>
    [InitializeOnLoad]
    public static class BookshelfSetup
    {
        const string MainMapPath = "Assets/Scenes/Main Map.unity";
        const string BookScriptFolder = ArchiveAssets.Root + "/Scripts/Books/Shelves";
        const string PendingKey = "ARCHIV3D.BookshelfPending";
        const int MaxPerRendererColliders = 60;

        // "1st year Section", "2nd Year Section", "3rd yr section", "4th Year Section (2)" ...
        static readonly Regex SectionName = new Regex(@"^\s*(\d+)\s*(st|nd|rd|th)?\s*y(ea)?r\b.*section", RegexOptions.IgnoreCase);

        static string OfferedKey => "ARCHIV3D.BookshelfOffered.v1." + Application.dataPath;

        static BookshelfSetup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += OnEditorReady;
        }

        static void OnEditorReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                SetUp();
                return;
            }
            if (EditorPrefs.GetBool(OfferedKey, false)) return;
            EditorPrefs.SetBool(OfferedKey, true);
            if (EditorUtility.DisplayDialog("ARCHIV3D: bookshelf books",
                    "Make the 1st, 2nd and 3rd year bookshelves in Main Map open a book when tapped or clicked?\n\n" +
                    "Each shelf gets its own book component (like the computers' quizzes) where you write the pages.\n\n" +
                    "You can do this later from Tools > ARCHIV3D.", "Set up now", "Later"))
                SetUp();
        }

        [MenuItem("Tools/ARCHIV3D/Set Up Bookshelf Books", false, 2)]
        public static void SetUp()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then try again.", "OK");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != MainMapPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMapPath) == null)
                {
                    EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find " + MainMapPath + ".", "OK");
                    return;
                }
                scene = EditorSceneManager.OpenScene(MainMapPath, OpenSceneMode.Single);
            }

            var shelves = FindShelves(scene);
            if (shelves.Count == 0)
            {
                EditorUtility.DisplayDialog("ARCHIV3D",
                    "Couldn't find any \"1st year Section\" / \"2nd year Section\" / \"3rd year Section\" objects in Main Map.", "OK");
                return;
            }

            // Make the numbered book scripts first if a shelf needs one that doesn't exist yet.
            var missing = new List<int>();
            foreach (var pair in shelves)
                if (pair.Key.GetComponent<ShelfBook>() == null && pair.Key.GetComponent<YearBookshelf>() == null &&
                    FindBookType(pair.Value) == null && !missing.Contains(pair.Value))
                    missing.Add(pair.Value);
            if (missing.Count > 0)
            {
                foreach (int n in missing) WriteBookScript(n);
                SessionState.SetBool(PendingKey, true);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("ARCHIV3D", "Making book scripts for year(s) " + string.Join(", ", missing) +
                    ".\n\nSetup will finish by itself once Unity has compiled them.", "OK");
                return;
            }

            var lines = new List<string>();
            int fixedCalls = 0, colliders = 0;
            foreach (var pair in shelves)
            {
                var go = pair.Key;
                int n = pair.Value;
                string shelfName = PrettyName(go.name);

                var reader = go.GetComponent<BookshelfReader>();
                if (reader == null) reader = Undo.AddComponent<BookshelfReader>(go); // also adds an Interactable if missing
                Undo.RecordObject(reader, "Set up bookshelf");
                if (string.IsNullOrWhiteSpace(reader.shelfName) || reader.shelfName == "Bookshelf") reader.shelfName = shelfName;
                PrefabUtility.RecordPrefabInstancePropertyModifications(reader);

                var interactable = go.GetComponent<Interactable>();
                Undo.RecordObject(interactable, "Set up bookshelf");
                if (string.IsNullOrWhiteSpace(interactable.prompt) || interactable.prompt == "Use Computer" || interactable.prompt == "Browse Books")
                    interactable.prompt = "Read Book";
                PrefabUtility.RecordPrefabInstancePropertyModifications(interactable);
                fixedCalls += RemoveBrokenLookAtCalls(interactable);

                var book = go.GetComponent<ShelfBook>();
                // Shelves that use subjects (Year Bookshelf) keep their books in subject assets instead.
                if (book == null && go.GetComponent<YearBookshelf>() == null)
                {
                    book = (ShelfBook)Undo.AddComponent(go, FindBookType(n));
                    Undo.RecordObject(book, "Set up book");
                    Seed(book, n, go.name);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(book);
                }

                colliders += EnsureColliders(go);
                lines.Add(go.name + "  ->  " + (book != null ? book.GetType().Name : "subjects (Year Bookshelf)"));
            }

            EnsureBookScreen(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var objs = new List<Object>();
            foreach (var pair in shelves) objs.Add(pair.Key);
            Selection.objects = objs.ToArray();

            string extra = "";
            if (colliders > 0) extra += "\n\nAdded " + colliders + " invisible click area(s) so the shelves can be tapped (they don't block walking).";
            if (fixedCalls > 0) extra += "\n\nRemoved " + fixedCalls + " broken \"LookAt\" event from the 1st year shelf's Interactable (it had no target and would stop the book from opening).";
            EditorUtility.DisplayDialog("ARCHIV3D: bookshelves ready",
                "Each shelf now opens its own book:\n\n" + string.Join("\n", lines) + extra +
                "\n\nSelect a shelf and write its pages in its book component (Inspector).\nThe scene was saved.", "Nice");
        }

        // ------------------------------------------------------------------ helpers

        static List<KeyValuePair<GameObject, int>> FindShelves(Scene scene)
        {
            var found = new List<KeyValuePair<GameObject, int>>();
            foreach (var root in scene.GetRootGameObjects()) Collect(root.transform, found);
            return found;
        }

        static void Collect(Transform t, List<KeyValuePair<GameObject, int>> found)
        {
            var m = SectionName.Match(t.name);
            if (m.Success)
            {
                found.Add(new KeyValuePair<GameObject, int>(t.gameObject, int.Parse(m.Groups[1].Value)));
                return;
            }
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), found);
        }

        /// <summary>"1st year Section" -> "1st Year Section" (only words that start with a letter are capitalized).</summary>
        static string PrettyName(string raw)
        {
            var words = raw.Trim().Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (w.Length > 0 && char.IsLetter(w[0]))
                    words[i] = char.ToUpper(w[0], CultureInfo.InvariantCulture) + w.Substring(1);
            }
            return string.Join(" ", words);
        }

        static string Ordinal(int n)
        {
            int t = n % 100;
            if (t >= 11 && t <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }

        static Type FindBookType(int n)
        {
            string wanted = "Year" + n + "Book";
            foreach (var t in TypeCache.GetTypesDerivedFrom<ShelfBook>())
                if (!t.IsAbstract && t.Name == wanted) return t;
            return null;
        }

        static void WriteBookScript(int n)
        {
            Directory.CreateDirectory(BookScriptFolder);
            string path = BookScriptFolder + "/Year" + n + "Book.cs";
            if (File.Exists(path)) return;
            string o = Ordinal(n);
            File.WriteAllText(path,
                "using UnityEngine;\n\n" +
                "namespace ARCHIV3D\n{\n" +
                "    /// <summary>The book on the " + o + " year bookshelf. Same logic as every other shelf book (see ShelfBook).</summary>\n" +
                "    [AddComponentMenu(\"ARCHIV3D/Books/" + o + " Year Book\")]\n" +
                "    public class Year" + n + "Book : ShelfBook\n    {\n    }\n}\n");
        }

        static void Seed(ShelfBook book, int n, string shelfObjectName)
        {
            string o = Ordinal(n);
            book.title = o + " Year Collection";
            book.subtitle = "ARCHIV3D Library";
            book.description = "Write what the " + o + " year shelf is about here.\nIt shows on the first page.";
            book.pages = new List<BookPage>
            {
                new BookPage
                {
                    heading = "Welcome",
                    text = "This is the " + o + " year shelf's book. Replace these pages with your own ideas: select \"" +
                           shelfObjectName + "\" in the Hierarchy and edit its " + o + " Year Book component."
                },
                new BookPage
                {
                    heading = "Adding pages",
                    text = "Press + under Pages to add a page. Each page has a heading, its text, and an optional picture.\n\n" +
                           "Write as much as you like: text that doesn't fit continues on the next page by itself."
                },
                new BookPage
                {
                    heading = "Styling text",
                    text = "You can make words <b>bold</b> or <i>italic</i> by typing <noparse><b>word</b></noparse> or " +
                           "<noparse><i>word</i></noparse>.\n\nTo add a picture, import an image, set its Texture Type to " +
                           "Sprite (2D and UI), and drag it into the page's Picture field."
                },
            };
        }

        /// <summary>Removes "Transform.LookAt" events that have no target (they throw and stop the other events).</summary>
        static int RemoveBrokenLookAtCalls(Interactable interactable)
        {
            var so = new SerializedObject(interactable);
            var calls = so.FindProperty("onInteract.m_PersistentCalls.m_Calls");
            if (calls == null) return 0;
            int removed = 0;
            for (int i = calls.arraySize - 1; i >= 0; i--)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_MethodName").stringValue != "LookAt") continue;
                var arg = call.FindPropertyRelative("m_Arguments.m_ObjectArgument");
                if (arg != null && arg.objectReferenceValue == null)
                {
                    calls.DeleteArrayElementAtIndex(i);
                    removed++;
                }
            }
            if (removed > 0) so.ApplyModifiedProperties();
            return removed;
        }

        /// <summary>
        /// PlayerInteractor finds things with a raycast, so the shelf needs colliders.
        /// If it has none, adds invisible trigger boxes around each shelf mesh (they don't block walking).
        /// </summary>
        static int EnsureColliders(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>(true) != null) return 0;

            var filters = go.GetComponentsInChildren<MeshFilter>(true);
            int added = 0;
            if (filters.Length > 0 && filters.Length <= MaxPerRendererColliders)
            {
                foreach (var mf in filters)
                {
                    if (mf.sharedMesh == null) continue;
                    var box = Undo.AddComponent<BoxCollider>(mf.gameObject);
                    box.isTrigger = true;
                    box.center = mf.sharedMesh.bounds.center;
                    box.size = mf.sharedMesh.bounds.size;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                    added++;
                }
                if (added > 0) return added;
            }

            // Fallback: one box around the whole shelf
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var root = Undo.AddComponent<BoxCollider>(go);
            root.isTrigger = true;
            if (renderers.Length > 0)
            {
                Bounds world = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);
                Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? world.min.x : world.max.x,
                                             (i & 2) == 0 ? world.min.y : world.max.y,
                                             (i & 4) == 0 ? world.min.z : world.max.z);
                    var local = go.transform.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
                root.center = (min + max) * 0.5f;
                root.size = Vector3.Max(max - min, Vector3.one * 0.01f);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
            return 1;
        }

        static void EnsureBookScreen(Scene scene)
        {
            BookScreen screen = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                screen = r.GetComponentInChildren<BookScreen>(true);
                if (screen != null) break;
            }
            if (screen == null)
            {
                var go = new GameObject("ARCHIV3D Book Screen");
                Undo.RegisterCreatedObjectUndo(go, "Create Book Screen");
                SceneManager.MoveGameObjectToScene(go, scene);
                screen = go.AddComponent<BookScreen>();
            }

            Undo.RecordObject(screen, "Set up Book Screen");
            if (screen.headingFont == null) screen.headingFont = ArchiveAssets.Font("Lora-Bold", "Lora Bold SDF");
            if (screen.bodyFont == null) screen.bodyFont = ArchiveAssets.Font("Lora-Regular", "Lora Regular SDF");
            if (screen.italicFont == null) screen.italicFont = ArchiveAssets.Font("Lora-Italic", "Lora Italic SDF");
            if (screen.labelFont == null) screen.labelFont = ArchiveAssets.PixelFont;
            if (screen.uiFont == null) screen.uiFont = ArchiveAssets.RegularFont;

            var pageBorderLeft = new Vector4(24, 24, 72, 24);   // left, bottom, right, top
            var pageBorderRight = new Vector4(72, 24, 24, 24);
            if (screen.coverSprite == null) screen.coverSprite = ArchiveAssets.Sprite("book_cover", new Vector4(64, 64, 64, 64));
            if (screen.leftPageSprite == null) screen.leftPageSprite = ArchiveAssets.Sprite("book_page_left", pageBorderLeft);
            if (screen.rightPageSprite == null) screen.rightPageSprite = ArchiveAssets.Sprite("book_page_right", pageBorderRight);
            if (screen.spineSprite == null) screen.spineSprite = ArchiveAssets.Sprite("book_spine", Vector4.zero);
            if (screen.ornamentSprite == null) screen.ornamentSprite = ArchiveAssets.Sprite("book_ornament", Vector4.zero);
            if (screen.roundButtonSprite == null) screen.roundButtonSprite = ArchiveAssets.ButtonDark;
            if (screen.arrowLeftSprite == null) screen.arrowLeftSprite = ArchiveAssets.Sprite("icon_arrow_left", Vector4.zero);
            if (screen.arrowRightSprite == null) screen.arrowRightSprite = ArchiveAssets.Sprite("icon_arrow_right", Vector4.zero);
            if (screen.closeSprite == null) screen.closeSprite = ArchiveAssets.Sprite("icon_close", Vector4.zero);
            if (screen.roundedSprite == null) screen.roundedSprite = ArchiveAssets.RoundedFill;
            EditorUtility.SetDirty(screen);
        }
    }
}
