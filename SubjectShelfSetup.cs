using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Set Up Year Subject Shelves.
    /// Follows the ARCHIV3D ERD: each year area's bookshelf (Bookshelf) gets one shelf per semester (Shelf),
    /// each shelf holds that semester's Subjects, and each subject holds its Learning Materials.
    /// Subjects are assets in Assets/ARCHIV3D/Subjects/&lt;Nth Year&gt;.
    /// An empty semester shelf is filled with the starting subject list below (once); subjects already
    /// on the bookshelf are kept and moved onto their semester's shelf.
    /// Safe to run again: it only fills in what is missing.
    /// </summary>
    public static class SubjectShelfSetup
    {
        const string MainMapPath = "Assets/Scenes/Main Map.unity";
        const string LibraryRoot = "Assets/ARCHIV3D/Subjects";
        static readonly Regex SectionName = new Regex(@"^\s*(\d+)\s*(st|nd|rd|th)?\s*y(ea)?r\b.*section", RegexOptions.IgnoreCase);

        // Starting subject list per (year, semester): the class's own list (code as the students write it,
        // full name shown on the shelf). 3rd year has no semester split in that list, so all of it is on the
        // 1st Semester shelf. Rename, move, add or remove subjects freely afterwards.
        static readonly Dictionary<(int year, int sem), (string code, string name, string desc)[]> Curriculum =
            new Dictionary<(int, int), (string, string, string)[]>
        {
            { (1, 1), new[] { ("COMPRO 1", "Computer Programming 1", ""), ("ITC", "Introduction to Computing", "") } },
            { (1, 2), new[] { ("COMPRO 2", "Computer Programming 2", ""), ("DISCRETE MATH", "Discrete Mathematics", ""),
                              ("SAM", "SAM", "") } },
            { (2, 1), new[] { ("DSA", "Data Structures and Algorithms", ""), ("OOP", "Object-Oriented Programming", ""),
                              ("HCI", "Human Computer Interaction", ""), ("OS", "Operating Systems", ""),
                              ("PRIN COMM", "Principles of Communication", "") } },
            { (2, 2), new[] { ("IM", "Information Management", ""), ("NT1", "Networking 1", ""),
                              ("QM", "Quantitative Methods", ""), ("SIA", "Systems Integration and Architecture", ""),
                              ("INTEG PROG", "Integrative Programming and Technologies", "") } },
            { (3, 1), new[] { ("APP DEV", "Applications Development and Emerging Technologies", ""),
                              ("AD BASE", "Advanced Database Systems", ""), ("EDP", "Event-Driven Programming", ""),
                              ("DATA COMMS", "Data Communications", ""), ("PROF ISSUES", "Social and Professional Issues", ""),
                              ("EA", "Enterprise Architecture", ""), ("AD SIA", "Advanced Systems Integration and Architecture", "") } },
        };

        static readonly Color32[] Colors =
        {
            new Color32(104, 46, 30, 255), new Color32(36, 72, 86, 255), new Color32(58, 52, 106, 255),
            new Color32(90, 66, 28, 255), new Color32(44, 86, 56, 255), new Color32(112, 40, 64, 255),
        };

        [MenuItem("Tools/ARCHIV3D/Set Up Year Subject Shelves", false, 3)]
        public static void SetUp()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then try again.", "OK");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.path != MainMapPath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(MainMapPath, OpenSceneMode.Single);
            }

            var shelves = FindShelves(scene);
            if (shelves.Count == 0)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find any \"1st year Section\" / \"2nd year Section\" / " +
                                                        "\"3rd year Section\" objects in Main Map.", "OK");
                return;
            }
            if (shelves.Any(p => p.Key.GetComponent<BookshelfReader>() == null))
                BookshelfSetup.SetUp();   // interact prompt, click areas, book screen

            var report = new List<string>();
            foreach (var pair in shelves.OrderBy(p => p.Value))
            {
                var go = pair.Key;
                int year = Mathf.Clamp(pair.Value, 1, 4);
                string area = YearBookshelf.Ordinal(year) + " Year Area";

                var yb = go.GetComponent<YearBookshelf>();
                if (yb == null) yb = Undo.AddComponent<YearBookshelf>(go);
                Undo.RecordObject(yb, "Year bookshelf");
                yb.yearLevel = year;
                if (string.IsNullOrWhiteSpace(yb.areaName) || yb.areaName.EndsWith(" Year Area")) yb.areaName = area;
                yb.shelves.RemoveAll(s => s == null);

                string folder = EnsureFolder(LibraryRoot + "/" + YearBookshelf.Ordinal(year) + " Year");
                if (yb.semesterShelves == null) yb.semesterShelves = new List<SemesterShelf>();
                yb.semesterShelves.RemoveAll(sh => sh == null);

                // Subjects already on this bookshelf (the old one-shelf list and the semester shelves).
                var existing = new List<SubjectData>();
                existing.AddRange(yb.shelves.Where(x => x != null));
                foreach (var sh in yb.semesterShelves)
                    if (sh.subjects != null) existing.AddRange(sh.subjects.Where(x => x != null));
                existing = existing.Distinct().ToList();
                var inFolder = AssetDatabase.FindAssets("t:SubjectData", new[] { folder })
                    .Select(g => AssetDatabase.LoadAssetAtPath<SubjectData>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(x => x != null).ToList();

                // Untouched placeholders ("Subject 2" with only the sample book) make way for real subjects.
                var removed = new List<string>();
                foreach (var ph in existing.Concat(inFolder).Distinct().Where(IsPlaceholder).ToList())
                {
                    removed.Add(ph.DisplayName);
                    existing.Remove(ph);
                    inFolder.Remove(ph);
                    yb.shelves.Remove(ph);
                    foreach (var sh in yb.semesterShelves) sh.subjects.Remove(ph);
                    AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(ph));
                }

                // One shelf per semester.
                Shelf(yb, 1);
                Shelf(yb, 2);

                // Fill empty semester shelves with the starting subject list (reusing subjects of the same name).
                var created = new List<string>();
                foreach (var sh in yb.semesterShelves.OrderBy(x => x.semester))
                {
                    if (sh.subjects.Any(x => x != null)) continue;
                    if (!Curriculum.TryGetValue((year, sh.semester), out var list)) continue;
                    for (int i = 0; i < list.Length; i++)
                    {
                        var (code, subjectName, desc) = list[i];
                        var subject = existing.Concat(inFolder).FirstOrDefault(x =>
                            string.Equals(x.subjectName?.Trim(), subjectName, System.StringComparison.OrdinalIgnoreCase));
                        if (subject == null)
                        {
                            subject = CreateSubject(folder, code, subjectName, desc, year, sh.semester, Colors[i % Colors.Length]);
                            created.Add(subject.DisplayName);
                        }
                        else
                        {
                            Undo.RecordObject(subject, "Subject semester");
                            subject.yearLevel = year;
                            subject.semester = sh.semester;
                            if (string.IsNullOrWhiteSpace(subject.description)) subject.description = desc;
                            if (string.IsNullOrWhiteSpace(subject.subjectCode)) subject.subjectCode = code;
                            EditorUtility.SetDirty(subject);
                        }
                        sh.subjects.Add(subject);
                    }
                }

                // Any other subject already on the bookshelf goes onto its own semester's shelf.
                foreach (var subject in existing)
                {
                    if (yb.semesterShelves.Any(sh => sh.subjects.Contains(subject))) continue;
                    Shelf(yb, Mathf.Clamp(subject.semester, 1, 3)).subjects.Add(subject);
                }
                yb.shelves.Clear();
                yb.semesterShelves.Sort((a, b) => a.semester.CompareTo(b.semester));

                // Move books from the old shelf components into the first subject.
                var moved = new List<string>();
                int removedSamples = 0;
                foreach (var book in go.GetComponents<ShelfBook>())
                {
                    if (IsUntouchedSample(book)) { removedSamples++; Undo.DestroyObjectImmediate(book); continue; }
                    var first = yb.Subjects.FirstOrDefault();
                    if (first == null) continue;
                    string key = scene.name + "/" + go.name + "/" + book.GetType().Name;
                    if (first.materials.All(m => m.importedFrom != key))
                    {
                        Undo.RecordObject(first, "Move book into subject");
                        var m = CopyBook(book, go.name, key);
                        first.materials.RemoveAll(x => string.IsNullOrEmpty(x.importedFrom) && x.title == "Lesson 1" && IsSamplePageList(x.pages));
                        first.materials.Insert(0, m);
                        first.EnsureIds();
                        EditorUtility.SetDirty(first);
                        moved.Add("\"" + book.DisplayTitle + "\" (" + PageCount(book) + " pages) -> " + first.DisplayName + " / " + m.title);
                    }
                    Undo.DestroyObjectImmediate(book);
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(yb);
                EditorUtility.SetDirty(yb);

                var line = new StringBuilder(go.name + "  (" + folder + ")");
                foreach (var sh in yb.semesterShelves)
                    line.Append("\n    " + sh.label + ": " + sh.ValidSubjects.Count + " subjects");
                if (created.Count > 0) line.Append("\n    added " + created.Count + " subjects");
                if (removed.Count > 0) line.Append("\n    removed placeholders: " + string.Join(", ", removed));
                foreach (var m in moved) line.Append("\n    moved " + m);
                if (removedSamples > 0) line.Append("\n    removed the old sample book");
                report.Add(line.ToString());
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            string text = "Each year area's bookshelf now has one shelf per semester, each with its subjects:\n\n" +
                          string.Join("\n\n", report) +
                          "\n\nRename subjects and add books: Assets/ARCHIV3D/Subjects > (year) > select a subject.\nThe scene was saved.";
            Debug.Log("[ARCHIV3D] " + text);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ARCHIV3D-SubjectShelves.txt", text);
            EditorUtility.DisplayDialog("ARCHIV3D: subject shelves ready", text, "Nice");
            var firstSubject = shelves.Select(p => p.Key.GetComponent<YearBookshelf>()).Where(y => y != null)
                                      .SelectMany(y => y.Subjects).FirstOrDefault();
            if (firstSubject != null) EditorGUIUtility.PingObject(firstSubject);
        }

        // ------------------------------------------------------------------ subjects

        static SubjectData CreateSubject(string folder, string code, string subjectName, string description, int year, int semester, Color color)
        {
            var s = ScriptableObject.CreateInstance<SubjectData>();
            s.subjectCode = code ?? "";
            s.subjectName = subjectName;
            s.description = description ?? "";
            s.yearLevel = year;
            s.semester = semester;
            s.color = color;
            s.materials = new List<LearningMaterial>();   // books are added in the subject's Materials list
            s.EnsureIds();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Safe(subjectName) + ".asset");
            AssetDatabase.CreateAsset(s, path);
            return s;
        }

        /// <summary>The year bookshelf's shelf for a semester (made if missing).</summary>
        static SemesterShelf Shelf(YearBookshelf yb, int semester)
        {
            var shelf = yb.semesterShelves.FirstOrDefault(x => x.semester == semester);
            if (shelf == null)
            {
                shelf = new SemesterShelf { semester = semester, label = SemesterShelf.LabelFor(semester) };
                yb.semesterShelves.Add(shelf);
            }
            if (shelf.subjects == null) shelf.subjects = new List<SubjectData>();
            if (string.IsNullOrWhiteSpace(shelf.label)) shelf.label = SemesterShelf.LabelFor(semester);
            return shelf;
        }

        static readonly Regex PlaceholderName = new Regex(@"^Subject \d+$");

        /// <summary>A "Subject 2"-style subject the first setup made that still only has its sample book.</summary>
        static bool IsPlaceholder(SubjectData s) =>
            s != null && PlaceholderName.IsMatch(s.subjectName ?? "") &&
            (s.materials == null || s.materials.All(m => m == null ||
                (string.IsNullOrEmpty(m.importedFrom) && m.title == "Lesson 1" && IsSamplePageList(m.pages))));

        static LearningMaterial CopyBook(ShelfBook book, string shelfObjectName, string key)
        {
            // The old book was titled after the shelf ("1st Year Section"); as a subject book it needs its own title.
            string title = SectionName.IsMatch(book.title ?? "") || book.title == shelfObjectName ? "Course Notes" : book.DisplayTitle;
            return new LearningMaterial
            {
                title = title,
                subtitle = book.subtitle == "ARCHIV3D Library" ? "" : book.subtitle,
                description = book.description,
                coverColor = book.coverColor,
                pages = book.pages.Where(p => p != null)
                                  .Select(p => new BookPage { heading = p.heading, text = p.text, picture = p.picture })
                                  .ToList(),
                importedFrom = key,
            };
        }

        static int PageCount(ShelfBook b) => b.pages != null ? b.pages.Count : 0;

        static readonly string[] SampleHeadings = { "Welcome", "Adding pages", "Styling text", "Fill in this subject" };

        static bool IsSamplePageList(List<BookPage> pages) =>
            pages == null || pages.All(p => p == null || SampleHeadings.Contains(p.heading));

        /// <summary>A shelf book that still only has the sample pages the bookshelf setup wrote.</summary>
        static bool IsUntouchedSample(ShelfBook book)
        {
            if (book.pages == null || book.pages.Count == 0) return true;
            if (!IsSamplePageList(book.pages)) return false;
            var welcome = book.pages.FirstOrDefault(p => p != null && p.heading == "Welcome");
            return welcome == null || (welcome.text ?? "").Contains("Replace these pages with your own ideas");
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

        static string EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return path;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            return path;
        }

        static string Safe(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
            return string.IsNullOrWhiteSpace(name) ? "Subject" : name.Trim();
        }
    }
}
