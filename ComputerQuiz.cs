using System.Collections.Generic;
using UnityEngine;

namespace BlackCompuQuiz
{
    /// <summary>
    /// The shared quiz logic every computer uses. Each Blackcompu has its own component
    /// (BlackCompu1Quiz, BlackCompu2Quiz, ...) that inherits from this, so every computer
    /// keeps its own questions but they all work the same way.
    /// Fill the questions in on the computer's Inspector.
    /// </summary>
    public abstract class ComputerQuiz : MonoBehaviour, IQuiz
    {
        [Tooltip("Shown on the computer screen.")]
        public string title = "Quiz";

        [TextArea(1, 3)]
        public string description = "";

        [Tooltip("Ask the questions in a random order each time.")]
        public bool shuffleQuestions = false;

        [Tooltip("Show multiple-choice answers in a random order each time.")]
        public bool shuffleChoices = false;

        [Tooltip("Optional: the subject this quiz belongs to (Assets/ARCHIV3D/Subjects). " +
                 "Used to link the student's answers and scores to that subject.")]
        public ARCHIV3D.SubjectData subject;

        public List<QuizQuestion> questions = new List<QuizQuestion>();

        public string DisplayTitle => string.IsNullOrWhiteSpace(title) ? GetType().Name : title;

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

        /// <summary>Key used to remember the best score for this quiz on the device.</summary>
        public string SaveKey
        {
            get
            {
                var sameType = GetComponents(GetType());
                int index = System.Array.IndexOf(sameType, this);
                return gameObject.scene.name + "/" + GetType().Name + (index > 0 ? "#" + index : "");
            }
        }

        /// <summary>ID used when saving the student's answers and scores.</summary>
        public string QuizId => SaveKey;

        // IQuiz, so the quiz screen can ask this quiz the same way as a subject's quiz.
        string IQuiz.Description => description;
        bool IQuiz.ShuffleQuestions => shuffleQuestions;
        bool IQuiz.ShuffleChoices => shuffleChoices;
        IList<QuizQuestion> IQuiz.Questions => questions;
        ARCHIV3D.SubjectData IQuiz.Subject => subject;
        Object IQuiz.Context => this;

        protected virtual void Reset()
        {
            title = gameObject.name + " Quiz";
            questions = new List<QuizQuestion> { new QuizQuestion() };
        }

        protected virtual void OnValidate()
        {
            if (questions == null) return;
            foreach (var q in questions)
            {
                if (q == null || q.choices == null || q.choices.Count == 0) continue;
                q.correctChoice = Mathf.Clamp(q.correctChoice, 0, q.choices.Count - 1);
            }
        }
    }
}
