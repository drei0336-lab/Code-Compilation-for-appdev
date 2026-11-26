using System.Collections.Generic;

namespace BlackCompuQuiz
{
    /// <summary>
    /// Anything the quiz screen can ask: a computer's own quiz component (ComputerQuiz)
    /// or a quiz stored on a subject asset (ARCHIV3D.SubjectQuiz).
    /// </summary>
    public interface IQuiz
    {
        /// <summary>Shown in the quiz list and at the top of the screen.</summary>
        string DisplayTitle { get; }

        /// <summary>Optional line shown under the title in the quiz list.</summary>
        string Description { get; }

        bool ShuffleQuestions { get; }
        bool ShuffleChoices { get; }

        IList<QuizQuestion> Questions { get; }

        /// <summary>Questions that are filled in enough to be asked.</summary>
        int UsableQuestionCount { get; }

        /// <summary>ID used to save the best score and the student's answers. Never changes.</summary>
        string QuizId { get; }

        /// <summary>The subject this quiz belongs to (can be null for a computer's own quiz).</summary>
        ARCHIV3D.SubjectData Subject { get; }

        /// <summary>Object to point at in Console messages.</summary>
        UnityEngine.Object Context { get; }
    }
}
