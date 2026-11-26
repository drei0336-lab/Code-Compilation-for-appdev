using System.Collections.Generic;
using BlackCompuQuiz;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Quiz Menu:
    /// - Open Quiz Menu (Play mode): opens the quiz main menu right away, without walking to a computer.
    /// - Add COMPRO 1 Quizzes: puts the Prelims, Midterms, Prefinals and Finals quizzes (5 questions each,
    ///   20 in all, written from its Course Notes) on Computer Programming 1. Quizzes it already has are kept.
    /// </summary>
    static class QuizMenuTools
    {
        const string Root = "Tools/ARCHIV3D/Quiz Menu/";
        const string Compro1Path = "Assets/ARCHIV3D/Subjects/1st Year/Computer Programming 1.asset";

        // ------------------------------------------------------------------ preview

        [MenuItem(Root + "Open Quiz Menu (Play mode)", false, 45)]
        static void OpenQuizMenu()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Press Play first, then use this menu.", "OK");
                return;
            }

            // Prefer a real computer, so the player is frozen while the menu is open, like in the game.
            var computer = Object.FindAnyObjectByType<BlackCompuComputer>();
            if (computer != null)
            {
                if (computer.IsScreenOpen)
                {
                    Debug.Log("[ARCHIV3D] The quiz screen is already open.", computer);
                    return;
                }
                computer.Open();
                Debug.Log("[ARCHIV3D] Opened the quiz main menu on " + computer.name + ".", computer);
                return;
            }

            var screen = QuizScreen.GetOrCreate();
            if (!screen.IsOpen) screen.OpenQuizMenu();
        }

        [MenuItem(Root + "Open Quiz Menu (Play mode)", true)]
        static bool CanOpenQuizMenu() => Application.isPlaying;

        // ------------------------------------------------------------------ sample quizzes

        [MenuItem(Root + "Add COMPRO 1 Quizzes (20 questions)", false, 60)]
        static void AddSampleQuizzes()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then run this again.", "OK");
                return;
            }

            var subject = FindCompro1();
            if (subject == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D",
                    "Couldn't find the Computer Programming 1 subject (" + Compro1Path + ").", "OK");
                return;
            }

            Undo.RecordObject(subject, "Add COMPRO 1 quizzes");
            if (subject.quizzes == null) subject.quizzes = new List<SubjectQuiz>();

            var added = new List<string>();
            foreach (var quiz in SampleQuizzes())
            {
                if (subject.quizzes.Exists(q => q != null && q.title == quiz.title)) continue;
                subject.quizzes.Add(quiz);
                added.Add(quiz.title);
            }

            subject.EnsureIds();
            EditorUtility.SetDirty(subject);
            AssetDatabase.SaveAssetIfDirty(subject);
            Selection.activeObject = subject;
            EditorGUIUtility.PingObject(subject);

            EditorUtility.DisplayDialog("ARCHIV3D",
                added.Count == 0
                    ? subject.DisplayName + " already has these quizzes."
                    : "Added to " + subject.DisplayName + ":\n\n" + string.Join("\n", added) +
                      "\n\nEdit them in the Inspector under Quizzes. Press Play and use any BlackCompu computer " +
                      "(or Tools > ARCHIV3D > Quiz Menu > Open Quiz Menu) to try them.",
                "Nice");
        }

        static SubjectData FindCompro1()
        {
            var subject = AssetDatabase.LoadAssetAtPath<SubjectData>(Compro1Path);
            if (subject != null) return subject;
            foreach (string guid in AssetDatabase.FindAssets("t:SubjectData"))
            {
                var s = AssetDatabase.LoadAssetAtPath<SubjectData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s == null) continue;
                string code = (s.subjectCode ?? "").Replace(" ", "").ToUpperInvariant();
                if (code == "COMPRO1" || s.DisplayName == "Computer Programming 1") return s;
            }
            return null;
        }

        // Questions taken from Computer Programming 1 > Course Notes, one quiz per term (chapters 1 to 7).
        static IEnumerable<SubjectQuiz> SampleQuizzes()
        {
            yield return new SubjectQuiz
            {
                title = "Prelims Quiz",
                description = "Chapters 1-2: Introduction to Java, Basics of Programming",
                shuffleChoices = true,
                questions = new List<QuizQuestion>
                {
                    Choice("What runs Java bytecode on whatever device it is installed on?",
                        0, "The Java Virtual Machine (JVM)", "javac", "The IDE", "An assembler",
                        explanation: "Source code is compiled into bytecode, and the JVM runs it: \"Write Once, Run Anywhere.\""),
                    Choice("Where does every Java program start?",
                        0, "The main method", "The first comment", "The class name", "The last statement",
                        explanation: "The main method is the front door of the program."),
                    Typed("Which command compiles Java source code? (type the command)",
                        "javac",
                        explanation: "Write the code, compile it with javac, then run it with java."),
                    Choice("Which of these is a high-level language?",
                        0, "Python", "Machine language", "Assembly language", "Binary 1s and 0s",
                        explanation: "High-level languages such as Java, Python and C++ read more like English."),
                    Choice("What is pseudocode?",
                        0, "The algorithm written in everyday words", "The algorithm drawn as a map with arrows",
                        "Code the computer runs directly", "A list of reserved words",
                        explanation: "Pseudocode is the recipe in everyday words; a flowchart is the recipe drawn as a map."),
                },
            };

            yield return new SubjectQuiz
            {
                title = "Midterms Quiz",
                description = "Chapters 3-4: Data Types, Operators and Expressions",
                shuffleChoices = true,
                questions = new List<QuizQuestion>
                {
                    Choice("Which data type fits a student's GPA, like 1.75?",
                        0, "double", "int", "char", "boolean",
                        explanation: "A GPA has decimals, so it is a double."),
                    Choice("Which of these is NOT one of Java's eight primitive types?",
                        0, "String", "char", "boolean", "long",
                        explanation: "String, used for text, is an object type."),
                    Choice("A box whose contents never change is called a...",
                        0, "constant", "variable", "literal", "identifier",
                        explanation: "Your birthday is a constant, but your age is a variable."),
                    Choice("What does the && operator mean?",
                        0, "Both conditions must be true", "Either condition can be true",
                        "Add 1 to the value", "Compare two strings",
                        explanation: "&& is \"ticket AND ID\": both are needed."),
                    Typed("What does x++ add to x? (type the number)",
                        "1", "one",
                        explanation: "x++ is a tally mark: it adds 1."),
                },
            };

            yield return new SubjectQuiz
            {
                title = "Prefinals Quiz",
                description = "Chapters 5-6: Input and Outputs, Control Structures",
                shuffleChoices = true,
                questions = new List<QuizQuestion>
                {
                    Choice("Which statement shows a message and then moves to the next line?",
                        0, "System.out.println", "System.out.print", "Scanner.nextLine", "System.in.read",
                        explanation: "print and println both display messages; println also moves to the next line."),
                    Typed("Which class from java.util reads what the user types on the keyboard? (type the class name)",
                        "Scanner", "java.util.Scanner",
                        explanation: "Scanner listens to the keyboard with methods like nextInt() and nextLine()."),
                    Choice("Why should a program show a prompt before asking for input?",
                        0, "So the user knows what to type", "It makes the program run faster",
                        "Java won't compile without it", "It saves memory",
                        explanation: "Like an ATM showing \"Enter your PIN:\" before it waits for you to type."),
                    Choice("Which loop always runs its body at least once?",
                        0, "do-while", "for", "while", "switch",
                        explanation: "A do-while loop is \"taste the soup at least once, then decide.\""),
                    Choice("\"Found my keys, stop searching.\" Which statement leaves a loop right away?",
                        0, "break", "continue", "else", "case",
                        explanation: "break stops the loop; continue only skips to the next round (\"skip this song\")."),
                },
            };

            yield return new SubjectQuiz
            {
                title = "Finals Quiz",
                description = "Chapter 7: Strings and Arrays",
                shuffleChoices = true,
                questions = new List<QuizQuestion>
                {
                    Choice("What is the position (index) of the first character in a Java string?",
                        0, "0", "1", "-1", "It depends on the string",
                        explanation: "Like cinema seats counted from zero, a string's first character is at 0."),
                    Choice("Which string method turns \"juan\" into \"JUAN\"?",
                        0, "toUpperCase()", "trim()", "concat()", "charAt()",
                        explanation: "toUpperCase() changes every letter to a capital letter."),
                    Typed("Which string method removes the extra spaces around a typed username? (type the method name)",
                        "trim()", "trim",
                        explanation: "trim() cleans the extra spaces from the start and end of the text."),
                    Choice("What is an array?",
                        0, "A fixed row of slots that all hold the same kind of thing",
                        "A list that can hold any mix of types", "A single piece of text", "A loop that repeats",
                        explanation: "An array is like an egg tray or a row of lockers: a set number of slots, opened by number."),
                    Choice("A seating chart with rows and columns is an example of a...",
                        0, "2D (two-dimensional) array", "one-dimensional array", "string", "constant",
                        explanation: "A 2D array has rows and columns, like a seating chart or a spreadsheet."),
                },
            };
        }

        static QuizQuestion Choice(string question, int correct, string a, string b, string c, string d, string explanation = "")
        {
            return new QuizQuestion
            {
                question = question,
                type = QuestionType.MultipleChoice,
                choices = new List<string> { a, b, c, d },
                correctChoice = correct,
                acceptedAnswers = new List<string>(),
                explanation = explanation,
            };
        }

        static QuizQuestion Typed(string question, string answer, string explanation = "") =>
            TypedAny(question, new[] { answer }, explanation);

        static QuizQuestion Typed(string question, string answer, string alsoAccepted, string explanation = "") =>
            TypedAny(question, new[] { answer, alsoAccepted }, explanation);

        static QuizQuestion TypedAny(string question, string[] answers, string explanation)
        {
            return new QuizQuestion
            {
                question = question,
                type = QuestionType.TypeTheAnswer,
                choices = new List<string>(),
                correctChoice = 0,
                acceptedAnswers = new List<string>(answers),
                explanation = explanation,
            };
        }
    }
}
