using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>
    /// ERD: LEARNING MATERIAL. One book on a subject's shelf (a module, a chapter set, a handout).
    /// Belongs to one Subject, so it is categorized by that subject and its year level.
    /// </summary>
    [Serializable]
    public class LearningMaterial
    {
        [Tooltip("Book title, shown on its first page and in the book list.")]
        public string title = "New Book";

        [Tooltip("Optional smaller line, e.g. \"Prelims\" or \"Module 1\".")]
        public string subtitle = "";

        [TextArea(2, 6)]
        [Tooltip("Short text on the book's first page, under the title.")]
        public string description = "";

        [Tooltip("Color of this book's button in the book list.")]
        public Color coverColor = new Color32(104, 46, 30, 255);

        [Tooltip("The pages. Long text continues onto the next page by itself.")]
        public List<BookPage> pages = new List<BookPage>();

        // Unique ID used to save each student's progress. Set automatically; never edit.
        [HideInInspector] public string materialId = "";

        // Where this book came from if it was moved here from an older shelf component.
        [HideInInspector] public string importedFrom = "";

        public string DisplayTitle => string.IsNullOrWhiteSpace(title) ? "Untitled book" : title;
    }

    /// <summary>
    /// ERD: SUBJECT. A course taught in one year level and semester (e.g. Computer Programming 1, 1st year, 1st sem).
    /// Each subject sits on its semester's shelf of its year's bookshelf and holds its learning materials.
    /// Create one with Assets > Create > ARCHIV3D > Subject, or Tools > ARCHIV3D > Set Up Year Subject Shelves.
    /// </summary>
    [CreateAssetMenu(menuName = "ARCHIV3D/Subject", fileName = "New Subject", order = 0)]
    public class SubjectData : ScriptableObject
    {
        [Tooltip("Course code, e.g. CC102 (optional). Shown under the subject name.")]
        public string subjectCode = "";

        [Tooltip("Subject name shown on the shelf, e.g. Computer Programming 1.")]
        public string subjectName = "New Subject";

        [Range(1, 4)]
        [Tooltip("Year level this subject belongs to.")]
        public int yearLevel = 1;

        [Range(1, 3)]
        [Tooltip("Semester: 1 = 1st semester, 2 = 2nd semester, 3 = summer. Decides which shelf it goes on.")]
        public int semester = 1;

        [TextArea(2, 5)]
        [Tooltip("Optional short description, shown in the subject list.")]
        public string description = "";

        [Tooltip("Color of this subject's button on the bookshelf.")]
        public Color color = new Color32(70, 44, 26, 255);

        [Tooltip("The books on this subject's shelf. Press + to add a book.")]
        public List<LearningMaterial> materials = new List<LearningMaterial>();

        [Tooltip("This subject's quizzes. Students pick them from the quiz main menu on the BlackCompu computers. " +
                 "Press + to add a quiz, then + under its Questions.")]
        public List<SubjectQuiz> quizzes = new List<SubjectQuiz>();

        // Unique ID used to save progress. Set automatically; never edit.
        [HideInInspector] public string subjectId = "";

        public string DisplayName => string.IsNullOrWhiteSpace(subjectName) ? name : subjectName;

        /// <summary>The quizzes that have at least one question filled in, ready to be asked.</summary>
        public List<SubjectQuiz> PlayableQuizzes
        {
            get
            {
                var list = new List<SubjectQuiz>();
                if (quizzes == null) return list;
                foreach (var q in quizzes)
                {
                    if (q == null) continue;
                    q.Owner = this;
                    if (string.IsNullOrEmpty(q.quizId)) q.quizId = Guid.NewGuid().ToString("N");
                    if (q.UsableQuestionCount > 0) list.Add(q);
                }
                return list;
            }
        }

        /// <summary>Makes sure the subject and every book have their own ID (also at runtime, as a fallback).</summary>
        public void EnsureIds()
        {
            bool changed = false;
            if (string.IsNullOrEmpty(subjectId)) { subjectId = Guid.NewGuid().ToString("N"); changed = true; }
            if (materials == null) materials = new List<LearningMaterial>();
            var seen = new HashSet<string>();
            foreach (var m in materials)
            {
                if (m == null) continue;
                // Pressing + in the Inspector copies the last book, ID included: give the copy its own.
                if (string.IsNullOrEmpty(m.materialId) || !seen.Add(m.materialId))
                {
                    m.materialId = Guid.NewGuid().ToString("N");
                    seen.Add(m.materialId);
                    changed = true;
                }
            }
            if (quizzes == null) quizzes = new List<SubjectQuiz>();
            var seenQuizzes = new HashSet<string>();
            foreach (var q in quizzes)
            {
                if (q == null) continue;
                q.Owner = this;
                // Same as books: + copies the last quiz, ID included, so give the copy its own.
                if (string.IsNullOrEmpty(q.quizId) || !seenQuizzes.Add(q.quizId))
                {
                    q.quizId = Guid.NewGuid().ToString("N");
                    seenQuizzes.Add(q.quizId);
                    changed = true;
                }
                if (q.questions == null) continue;
                foreach (var question in q.questions)
                {
                    if (question == null || question.choices == null || question.choices.Count == 0) continue;
                    question.correctChoice = Mathf.Clamp(question.correctChoice, 0, question.choices.Count - 1);
                }
            }
#if UNITY_EDITOR
            if (changed && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        void OnValidate() => EnsureIds();
        void OnEnable() => EnsureIds();

        void Reset()
        {
            subjectName = name;
            materials = new List<LearningMaterial> { new LearningMaterial { title = "Lesson 1", pages = new List<BookPage> { new BookPage() } } };
            EnsureIds();
        }
    }
}
