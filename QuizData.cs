using System.Collections.Generic;
using UnityEngine;

namespace BlackCompuQuiz
{
    public enum QuestionType
    {
        MultipleChoice,
        TypeTheAnswer
    }

    /// <summary>One question inside a quiz. You fill these in from the Inspector.</summary>
    [System.Serializable]
    public class QuizQuestion
    {
        [TextArea(2, 6)]
        public string question = "Write your question here";

        public QuestionType type = QuestionType.MultipleChoice;

        [Tooltip("Multiple Choice only: the answers shown as buttons.")]
        public List<string> choices = new List<string> { "Choice 1", "Choice 2", "Choice 3", "Choice 4" };

        [Tooltip("Multiple Choice only: which choice is right (0 = the first choice).")]
        [Min(0)]
        public int correctChoice = 0;

        [Tooltip("Type The Answer only: every answer that should count as right. Capital letters and extra spaces are ignored.")]
        public List<string> acceptedAnswers = new List<string> { "answer" };

        [TextArea(1, 4)]
        [Tooltip("Optional. Shown after the question is answered.")]
        public string explanation = "";

        /// <summary>True when the question has enough filled in to be asked.</summary>
        public bool IsUsable
        {
            get
            {
                if (string.IsNullOrWhiteSpace(question)) return false;

                if (type == QuestionType.MultipleChoice)
                    return choices != null && choices.Count >= 2 &&
                           correctChoice >= 0 && correctChoice < choices.Count;

                if (acceptedAnswers == null) return false;
                foreach (var a in acceptedAnswers)
                    if (!string.IsNullOrWhiteSpace(a)) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Old quiz asset type, kept only so existing .asset files still open.
    /// Computers now use their own quiz components (BlackCompu1Quiz, BlackCompu2Quiz, ...) instead.
    /// </summary>
    public class QuizData : ScriptableObject
    {
        [Tooltip("Shown in BlackCompu's quiz list.")]
        public string title = "New Quiz";

        [TextArea(1, 3)]
        public string description = "";

        [Tooltip("Ask the questions in a random order each time.")]
        public bool shuffleQuestions = false;

        [Tooltip("Show multiple-choice answers in a random order each time.")]
        public bool shuffleChoices = false;

        public List<QuizQuestion> questions = new List<QuizQuestion>();

        public string DisplayTitle => string.IsNullOrWhiteSpace(title) ? name : title;

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

        void OnValidate()
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
