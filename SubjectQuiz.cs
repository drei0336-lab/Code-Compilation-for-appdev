using System;
using System.Collections.Generic;
using BlackCompuQuiz;
using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>
    /// ERD: QUIZ. One quiz of a subject (e.g. "Prelims Quiz"), stored on the subject asset next to its books.
    /// Students pick it from the quiz main menu on any BlackCompu computer: subject > quiz > questions.
    /// Fill it in at Assets/ARCHIV3D/Subjects > (year) > (subject) > Quizzes.
    /// </summary>
    [Serializable]
    public class SubjectQuiz : IQuiz
    {
        [Tooltip("Quiz name shown in the subject's quiz list, e.g. Prelims Quiz.")]
        public string title = "New Quiz";

        [TextArea(1, 3)]
        [Tooltip("Optional short line under the title in the quiz list.")]
        public string description = "";

        [Tooltip("Ask the questions in a random order each time.")]
        public bool shuffleQuestions = false;

        [Tooltip("Show multiple-choice answers in a random order each time.")]
        public bool shuffleChoices = false;

        [Tooltip("The questions. Press + to add one.")]
        public List<QuizQuestion> questions = new List<QuizQuestion>();

        // Unique ID used to save best scores and answers. Set automatically; never edit.
        [HideInInspector] public string quizId = "";

        // The subject this quiz is stored on. Filled in when the subject loads; not saved.
        [NonSerialized] SubjectData _owner;

        public SubjectData Owner
        {
            get => _owner;
            set => _owner = value;
        }

        public string DisplayTitle => string.IsNullOrWhiteSpace(title) ? "Untitled quiz" : title;

        public int UsableQuestionCount
        {
            get
            {
                int count = 0;
                if (questions == null) return 0;
                foreach (var q in questions)
                    if (q != null && q.IsUsable) count++;
                return count;
            }
        }

        // ------------------------------------------------------------------ IQuiz

        string IQuiz.Description => description;
        bool IQuiz.ShuffleQuestions => shuffleQuestions;
        bool IQuiz.ShuffleChoices => shuffleChoices;
        IList<QuizQuestion> IQuiz.Questions => questions;
        SubjectData IQuiz.Subject => _owner;
        UnityEngine.Object IQuiz.Context => _owner;

        /// <summary>"subject/&lt;quiz id&gt;", so it never clashes with a computer's quiz ID.</summary>
        public string QuizId => "subject/" + quizId;
    }
}
