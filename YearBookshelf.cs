using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>ERD: SHELF. One shelf of a year's bookshelf, holding one semester's subjects.</summary>
    [Serializable]
    public class SemesterShelf
    {
        [Tooltip("Shown in the game, e.g. 1st Semester.")]
        public string label = "1st Semester";

        [Range(1, 3)]
        [Tooltip("1 = 1st semester, 2 = 2nd semester, 3 = summer.")]
        public int semester = 1;

        [Tooltip("This semester's subjects. Drag subject assets in from Assets/ARCHIV3D/Subjects.")]
        public List<SubjectData> subjects = new List<SubjectData>();

        /// <summary>The filled-in subjects (empty slots and repeats skipped).</summary>
        public List<SubjectData> ValidSubjects
        {
            get
            {
                var list = new List<SubjectData>();
                if (subjects != null)
                    foreach (var s in subjects)
                        if (s != null && !list.Contains(s)) list.Add(s);
                return list;
            }
        }

        public static string LabelFor(int semester) =>
            semester == 3 ? "Summer" : YearBookshelf.Ordinal(Mathf.Max(1, semester)) + " Semester";
    }

    /// <summary>
    /// ERD: BOOKSHELF. The bookshelf that stands in one year level's area of the library.
    /// It has one shelf per semester; each shelf holds that semester's Subjects, and each subject
    /// holds its learning materials (books).
    ///
    /// In the game: look at the bookshelf, tap "Read Book", pick a semester, a subject, then a book.
    /// Works together with Bookshelf Reader on the same object.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ARCHIV3D/Year Bookshelf")]
    public class YearBookshelf : MonoBehaviour
    {
        [Range(1, 4)]
        [Tooltip("Year level of this area (1 = 1st year).")]
        public int yearLevel = 1;

        [Tooltip("Shown at the top of the semester list, e.g. 1st Year Area.")]
        public string areaName = "1st Year Area";

        [Tooltip("One shelf per semester, each with its subjects.")]
        public List<SemesterShelf> semesterShelves = new List<SemesterShelf>();

        // Subjects from before semesters existed. Tools > ARCHIV3D > Set Up Year Subject Shelves moves them onto
        // their semester's shelf; until then they show as one shelf.
        [HideInInspector] public List<SubjectData> shelves = new List<SubjectData>();

        /// <summary>The shelves to show in the game (only shelves that have subjects).</summary>
        public List<SemesterShelf> ShelvesToShow
        {
            get
            {
                var list = new List<SemesterShelf>();
                if (semesterShelves != null)
                    foreach (var shelf in semesterShelves)
                        if (shelf != null && shelf.ValidSubjects.Count > 0) list.Add(shelf);
                if (list.Count == 0 && shelves != null && shelves.Count > 0)
                    list.Add(new SemesterShelf { label = "Subjects", semester = 1, subjects = new List<SubjectData>(shelves) });
                return list;
            }
        }

        /// <summary>Every subject on this bookshelf.</summary>
        public List<SubjectData> Subjects
        {
            get
            {
                var list = new List<SubjectData>();
                foreach (var shelf in ShelvesToShow)
                    foreach (var s in shelf.ValidSubjects)
                        if (!list.Contains(s)) list.Add(s);
                return list;
            }
        }

        public static string Ordinal(int n)
        {
            int mod100 = n % 100;
            string suffix = mod100 >= 11 && mod100 <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + suffix;
        }

        void Reset()
        {
            areaName = Ordinal(yearLevel) + " Year Area";
            semesterShelves = new List<SemesterShelf>
            {
                new SemesterShelf { label = SemesterShelf.LabelFor(1), semester = 1 },
                new SemesterShelf { label = SemesterShelf.LabelFor(2), semester = 2 },
            };
        }

        void OnValidate()
        {
            if (semesterShelves == null) return;
            foreach (var shelf in semesterShelves)
            {
                if (shelf == null || shelf.subjects == null) continue;
                foreach (var s in shelf.subjects)
                {
                    if (s == null) continue;
                    if (s.yearLevel != yearLevel)
                        Debug.LogWarning($"[ARCHIV3D] \"{s.DisplayName}\" is a {Ordinal(s.yearLevel)} year subject but sits on the " +
                                         $"{Ordinal(yearLevel)} year bookshelf ({name}).", this);
                    else if (s.semester != shelf.semester)
                        Debug.LogWarning($"[ARCHIV3D] \"{s.DisplayName}\" is set to semester {s.semester} but sits on the " +
                                         $"\"{shelf.label}\" shelf of {name}.", this);
                }
            }
        }
    }
}
