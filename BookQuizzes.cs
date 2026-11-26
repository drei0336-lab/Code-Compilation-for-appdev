using System.Collections.Generic;
using BlackCompuQuiz;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Quiz Menu > Update Book Quizzes (all subjects)
    ///
    /// Writes the Prelims, Midterms, Prefinals and Finals quizzes (5 questions each, same format as the original
    /// COMPRO 1 quizzes) into every subject that has a lesson book, written from that book:
    /// Computer Programming 1 (Course Notes), Introduction to Computing, Computer Programming 2, Discrete Mathematics,
    /// System Admin and Maintenance (1st Year) and Data Structures and Algorithms (2nd Year).
    /// Students take them on any BlackCompu computer: quiz main menu > subject > quiz.
    /// A quiz with the same title is updated in place, so its ID (and saved scores) stay; other quizzes are kept.
    /// It also makes sure the math symbols font is a fallback of TextMeshPro's default font (used by the quiz
    /// screen), so the Discrete Mathematics symbols show. Safe to run again.
    /// </summary>
    static class BookQuizzes
    {
        [MenuItem("Tools/ARCHIV3D/Quiz Menu/Update Book Quizzes (all subjects)", false, 40)]
        static void UpdateAll()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then run this again.", "OK");
                return;
            }

            SymbolFontSetup.Ensure(out string fontError);

            var lines = new List<string>();
            foreach (var set in Sets())
            {
                var subject = AssetDatabase.LoadAssetAtPath<SubjectData>(set.path);
                if (subject == null)
                {
                    lines.Add(set.name + ": subject not found (" + set.path + ")");
                    continue;
                }

                Undo.RecordObject(subject, "Update book quizzes");
                if (subject.quizzes == null) subject.quizzes = new List<SubjectQuiz>();
                int added = 0, updated = 0;
                foreach (var quiz in set.quizzes)
                {
                    var existing = subject.quizzes.Find(q => q != null && q.title == quiz.title);
                    if (existing == null)
                    {
                        subject.quizzes.Add(quiz);
                        added++;
                    }
                    else
                    {
                        existing.description = quiz.description;
                        existing.shuffleQuestions = quiz.shuffleQuestions;
                        existing.shuffleChoices = quiz.shuffleChoices;
                        existing.questions = quiz.questions;
                        updated++;
                    }
                }

                subject.EnsureIds();
                EditorUtility.SetDirty(subject);
                AssetDatabase.SaveAssetIfDirty(subject);
                lines.Add(subject.DisplayName + ": " + (added + updated) + " quizzes" +
                          (updated > 0 ? " (" + updated + " updated)" : ""));
            }

            EditorUtility.DisplayDialog("ARCHIV3D",
                "Book quizzes are ready (5 questions each):\n\n" + string.Join("\n", lines) +
                (fontError != null ? "\n\nMath symbols font: " + fontError : "") +
                "\n\nPress Play and use any BlackCompu computer (or Tools > ARCHIV3D > Quiz Menu > Open Quiz Menu) to try them.",
                "Nice");
        }

        class QuizSet
        {
            public string name;
            public string path;
            public List<SubjectQuiz> quizzes;
        }

        static SubjectQuiz Quiz(string title, string description, params QuizQuestion[] questions)
        {
            return new SubjectQuiz
            {
                title = title,
                description = description,
                shuffleQuestions = false,
                shuffleChoices = true,
                questions = new List<QuizQuestion>(questions),
            };
        }

        // The right answer is written first; the quiz shuffles the choices.
        static QuizQuestion Choice(string question, string explanation, string right, string wrong1, string wrong2, string wrong3)
        {
            return new QuizQuestion
            {
                question = question,
                type = QuestionType.MultipleChoice,
                choices = new List<string> { right, wrong1, wrong2, wrong3 },
                correctChoice = 0,
                acceptedAnswers = new List<string>(),
                explanation = explanation,
            };
        }

        // Every accepted answer; capital letters and extra spaces are ignored.
        static QuizQuestion Typed(string question, string explanation, params string[] answers)
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

        static IEnumerable<QuizSet> Sets()
        {
            yield return new QuizSet
            {
                name = "Computer Programming 1",
                path = "Assets/ARCHIV3D/Subjects/1st Year/Computer Programming 1.asset",
                quizzes = new List<SubjectQuiz>
                {
                Quiz("Prelims Quiz", "Chapters 1-2: Introduction to Java, Basics of Programming",
                    Choice("Java's promise \"Write Once, Run Anywhere\" works because...",
                        "Source code is compiled into bytecode, and the JVM runs that bytecode on whatever device it is installed on.",
                        "The JVM runs the same bytecode on any device", "Java is rewritten for every device", "The IDE turns Java into Python", "Java only runs on one operating system"),
                    Choice("If a class is a house blueprint, what is an object?",
                        "A class is the blueprint; an object is the actual house built from it.",
                        "The actual house built from it", "The front door", "The street address", "The builder"),
                    Typed("Write, compile with javac, then run. Which command runs a compiled Java program? (type the command)",
                        "The workflow: write the code, compile it with javac, then run it with java.",
                        "java"),
                    Choice("Which translator is used for assembly code?",
                        "A compiler or an interpreter translates high-level code; an assembler translates assembly code.",
                        "An assembler", "A compiler", "An interpreter", "An IDE"),
                    Choice("What is a flowchart?",
                        "An algorithm is the recipe, pseudocode is the recipe in everyday words, and a flowchart is the recipe drawn as a map with arrows.",
                        "The algorithm drawn as a map with arrows", "The algorithm written in everyday words", "Code the computer runs directly", "A list of reserved words")),
                Quiz("Midterms Quiz", "Chapters 3-4: Data Types, Operators and Expressions",
                    Choice("Which data type fits whether a student is enrolled?",
                        "Yes-or-no values like \"enrolled\" are booleans; a name is a String, an age an int, a GPA a double.",
                        "boolean", "char", "String", "double"),
                    Typed("How many primitive data types does Java have? (type the number)",
                        "byte, short, int, long, float, double, char and boolean: eight primitive types.",
                        "8", "eight"),
                    Choice("Values written directly in the code, like 18 or \"Juan\", are called...",
                        "Literals are values written directly in the code.",
                        "literals", "constants", "identifiers", "operators"),
                    Choice("\"Cash OR card\": which operator means either condition will do?",
                        "|| means either one will do; && means both must be true (\"ticket AND ID\").",
                        "||", "&&", "++", "?:"),
                    Choice("What decides which operation in an expression runs first?",
                        "Precedence decides which operation runs first.",
                        "Precedence", "Assignment", "Shifting", "Declaration")),
                Quiz("Prefinals Quiz", "Chapters 5-6: Input and Outputs, Control Structures",
                    Choice("An ATM shows \"Enter your PIN:\" and then waits. The message on the screen is...",
                        "Showing the message is output with a prompt; waiting for you to type is input.",
                        "Output with a prompt", "Input", "A loop", "A comment"),
                    Typed("Which Scanner method reads a whole line of text? (type the method name)",
                        "Scanner listens to the keyboard with methods like nextInt() and nextLine().",
                        "nextLine()", "nextLine"),
                    Choice("\"Do 10 push-ups\" is a known count. Which loop fits it?",
                        "A for loop is a known count; a while loop is an unknown count; a do-while runs at least once.",
                        "for", "while", "do-while", "switch"),
                    Choice("Which statement works like a vending machine: press 5, get the 5th option?",
                        "switch is a vending machine or an elevator: press 5, get the 5th option.",
                        "switch", "if-else", "break", "continue"),
                    Choice("\"Skip this song, play the next.\" Which statement is this?",
                        "continue skips to the next round; break stops the loop; return gives back an answer and ends.",
                        "continue", "break", "return", "else")),
                Quiz("Finals Quiz", "Chapter 7: Strings and Arrays",
                    Choice("Which string method tells you how many characters a string has?",
                        "length() counts the characters; charAt() gets one character by its numbered seat.",
                        "length()", "charAt()", "concat()", "trim()"),
                    Typed("In the string \"JAVA\", which character is at index 0? (type the letter)",
                        "Each character has a numbered seat that starts at 0, like cinema seats counted from zero.",
                        "J"),
                    Choice("Which string method checks whether two strings have the same text?",
                        "equals() compares the text of two strings.",
                        "equals()", "indexOf()", "concat()", "toUpperCase()"),
                    Choice("An egg tray or a row of lockers is most like...",
                        "An array is a fixed row of slots that all hold the same kind of thing, opened by number.",
                        "An array", "A string", "A loop", "A constant"),
                    Choice("An array that is a single row of slots is called...",
                        "A one-dimensional array is a single row; a 2D array has rows and columns, like a seating chart.",
                        "A one-dimensional array", "A 2D array", "A string", "A multi-dimensional table"))
                },
            };
            yield return new QuizSet
            {
                name = "Introduction to Computing",
                path = "Assets/ARCHIV3D/Subjects/1st Year/Introduction to Computing.asset",
                quizzes = new List<SubjectQuiz>
                {
                Quiz("Prelims Quiz", "Lessons 1-2: What Is Computing?, History and Generations of Computers",
                    Choice("What are the four parts of the IPOS cycle?",
                        "A computer accepts input, processes it, stores it, and produces output: Input, Process, Output, Storage.",
                        "Input, Process, Output, Storage", "Install, Program, Operate, Shutdown", "Input, Print, Output, Save", "Internet, Power, Output, Software"),
                    Choice("A list of raw scores like 87, 92, 75 is...",
                        "Data is raw facts. Processed into something meaningful (\"class average is 84.7\") it becomes information.",
                        "Data", "Information", "Knowledge", "An algorithm"),
                    Choice("Which computing field does BSIT focus on: applying technology to solve organizational problems?",
                        "BSIT focuses on applying technology to solve organizational problems: Information Technology.",
                        "Information Technology (IT)", "Computer Science (CS)", "Computer Engineering (CE)", "Software Engineering (SE)"),
                    Typed("Who is considered the first programmer? (type her name)",
                        "Ada Lovelace is considered the first programmer; Babbage designed the Analytical Engine.",
                        "Ada Lovelace", "Lovelace", "Ada"),
                    Choice("Which hardware technology defines the second generation of computers?",
                        "1st: vacuum tubes, 2nd: transistors, 3rd: integrated circuits, 4th: microprocessors.",
                        "Transistors", "Vacuum tubes", "Integrated circuits", "Microprocessors")),
                Quiz("Midterms Quiz", "Lessons 3-4: Computer Hardware, Computer Software",
                    Choice("Which part of the CPU does arithmetic and logic?",
                        "The CPU contains the ALU (arithmetic and logic), the Control Unit, and registers.",
                        "The ALU", "The Control Unit", "The registers", "The cache"),
                    Choice("Why does a computer lose unsaved work when the power is cut?",
                        "RAM is volatile, temporary working space; ROM is non-volatile.",
                        "RAM is volatile", "ROM gets erased", "The SSD resets itself", "The CPU loses its clock speed"),
                    Typed("Which processor is specialized for graphics and parallel computation? (type its 3-letter name)",
                        "The GPU is specialized for graphics and parallel computation.",
                        "GPU"),
                    Choice("Is a web browser system software or application software?",
                        "A browser is application software, like word processors and games. Operating systems and drivers are system software.",
                        "Application software", "System software", "Programming software", "A device driver"),
                    Choice("Which of these is a job of the operating system?",
                        "The OS manages processes, memory, files and devices, and provides the user interface and security.",
                        "Managing memory, files and devices", "Translating source code into machine code", "Showing web pages", "Writing documents")),
                Quiz("Prefinals Quiz", "Lessons 5-6: Number Systems and Data Representation, Networks and the Internet",
                    Typed("What is the binary number 1011 in decimal? (type the number)",
                        "Multiply each bit by its power of 2: 8 + 0 + 2 + 1 = 11.",
                        "11"),
                    Choice("Why do computers use binary?",
                        "Circuits have two states, on (1) and off (0), so base 2 fits them.",
                        "Circuits have two states: on (1) and off (0)", "Binary numbers are always shorter", "People count in twos", "It is the only base with letters"),
                    Choice("How many bits make 1 byte?",
                        "8 bits = 1 byte; 1 KB = 1,024 bytes.",
                        "8", "4", "16", "1,024"),
                    Choice("What does DNS do?",
                        "DNS translates domain names (like example.com) into IP addresses.",
                        "Translates domain names into IP addresses", "Assigns passwords to users", "Connects devices inside a LAN", "Stores web pages offline"),
                    Choice("Which device connects different networks together?",
                        "A router connects networks; a switch connects devices within a LAN.",
                        "A router", "A switch", "An access point", "A scanner")),
                Quiz("Finals Quiz", "Lessons 7-8: Cybersecurity, Ethics and Digital Citizenship, Problem Solving and Programming",
                    Choice("What does the 3-2-1 backup rule mean?",
                        "Keep 3 copies of your data, on 2 media types, with 1 offsite.",
                        "3 copies, 2 media types, 1 offsite", "3 passwords, 2 devices, 1 account", "3 backups a day, 2 a week, 1 a month", "3 drives, 2 computers, 1 cloud"),
                    Choice("Which Philippine law protects personal data?",
                        "The Data Privacy Act of 2012 is RA 10173; the Cybercrime Prevention Act of 2012 is RA 10175.",
                        "Data Privacy Act of 2012 (RA 10173)", "Cybercrime Prevention Act of 2012 (RA 10175)", "Electronic Commerce Act", "Intellectual Property Code"),
                    Typed("What does MFA stand for? (type the full term)",
                        "Multi-factor authentication: enable it along with strong, unique passwords.",
                        "multi-factor authentication", "multifactor authentication", "multi factor authentication"),
                    Choice("In a flowchart, which shape shows a decision?",
                        "Oval = start/end, rectangle = process, diamond = decision, parallelogram = input/output.",
                        "Diamond", "Oval", "Rectangle", "Parallelogram"),
                    Choice("Breaking a big problem into smaller parts is called...",
                        "Computational thinking: decomposition, pattern recognition, abstraction, and algorithm design.",
                        "Decomposition", "Abstraction", "Pattern recognition", "Iteration"))
                },
            };
            yield return new QuizSet
            {
                name = "Computer Programming 2",
                path = "Assets/ARCHIV3D/Subjects/1st Year/Computer Programming 2.asset",
                quizzes = new List<SubjectQuiz>
                {
                Quiz("Prelims Quiz", "Lessons 1-2: Programming Fundamentals and Methods, Arrays and Strings",
                    Choice("Two methods with the same name but different parameter lists is called...",
                        "Overloading: same method name, different parameter lists.",
                        "Overloading", "Overriding", "Encapsulation", "Recursion"),
                    Choice("Where does a variable exist?",
                        "Scope: a variable exists only within the block where it is declared.",
                        "Only within the block where it is declared", "Everywhere in the program", "Only inside main", "Only inside loops"),
                    Typed("An array has 5 elements. What is its last index? (type the number)",
                        "Indexes start at 0, so the last index is length - 1 = 4.",
                        "4", "four"),
                    Choice("How should you compare the text of two Java strings?",
                        "Compare strings with equals(), not ==.",
                        "equals()", "==", "=", "length()"),
                    Choice("Which method returns the single character at a position?",
                        "charAt() returns one character; substring() returns part of the string.",
                        "charAt()", "substring()", "length()", "split()")),
                Quiz("Midterms Quiz", "Lessons 3-4: Introduction to OOP, Encapsulation and Access Control",
                    Choice("What is an object?",
                        "A class is a blueprint; an object is an instance of a class with its own data.",
                        "An instance of a class with its own data", "A blueprint for a class", "A kind of loop", "A reserved word"),
                    Choice("Which special method initializes a new object?",
                        "A constructor initializes a new object, like Student(String name, int year).",
                        "A constructor", "A getter", "The main method", "A setter"),
                    Typed("Which keyword refers to the current object? (type the keyword)",
                        "this refers to the current object, as in this.name = name.",
                        "this"),
                    Choice("Which access modifier allows access from the class only?",
                        "private: class only; protected: class, subclasses, package; public: everywhere.",
                        "private", "public", "protected", "static"),
                    Choice("Why give Account a private balance with a deposit() method instead of a public balance?",
                        "Getters and setters give controlled access, so deposit() can reject amounts that aren't positive.",
                        "So changes can be checked (validated) first", "Because it runs faster", "Because public fields can't be read", "Because Java requires it")),
                Quiz("Prefinals Quiz", "Lessons 5-6: Inheritance and Polymorphism, Abstraction and Interfaces",
                    Typed("Which keyword makes a class inherit from a superclass? (type the keyword)",
                        "class Dog extends Animal: the subclass acquires the superclass's fields and methods.",
                        "extends"),
                    Choice("A Car and its Engine have which relationship?",
                        "Inheritance models is-a (a Student is a Person). A car has an engine: has-a.",
                        "has-a", "is-a", "overrides", "extends"),
                    Choice("Which describes method overriding?",
                        "Overriding: subclass, same signature, chosen at runtime. Overloading: same class, different parameters, compile time.",
                        "A subclass replaces an inherited method with the same signature", "Same class, same name, different parameters", "Calling a method from main", "Making a field private"),
                    Choice("Which of these cannot be instantiated (made into an object directly)?",
                        "An abstract class cannot be instantiated; it may have abstract and concrete methods.",
                        "An abstract class", "A concrete class", "A subclass", "A class with a constructor"),
                    Choice("When is an interface the better choice?",
                        "Interface: a shared capability among unrelated classes. Abstract class: shared state and behavior among related classes.",
                        "When unrelated classes share a capability", "When related classes share code and state", "When you need to create objects directly", "When you need a constructor")),
                Quiz("Finals Quiz", "Lessons 7-8: Exception Handling and File I/O, Collections and GUI",
                    Choice("When does the finally block run?",
                        "try has the risky code, catch responds to the error, and finally always runs, for cleanup.",
                        "Always, whether or not an exception happened", "Only when there is no error", "Only when there is an error", "Only when the program ends"),
                    Choice("IOException is an example of a...",
                        "Checked exceptions like IOException must be handled or declared; unchecked ones like NullPointerException usually mean bugs.",
                        "Checked exception", "Unchecked exception", "Syntax error", "GUI event"),
                    Typed("Which keyword raises an exception? (type the keyword)",
                        "throw raises an exception; throws declares that a method may throw one.",
                        "throw"),
                    Choice("Which collection stores key-value pairs?",
                        "HashMap: key-value pairs. ArrayList: resizable list. HashSet: unique items.",
                        "HashMap", "ArrayList", "HashSet", "An array"),
                    Choice("Why prefer an ArrayList over an array?",
                        "Prefer ArrayList over arrays when the size changes.",
                        "Its size can change", "It is always faster", "It can only hold numbers", "It needs no type"))
                },
            };
            yield return new QuizSet
            {
                name = "Discrete Mathematics",
                path = "Assets/ARCHIV3D/Subjects/1st Year/Discrete Mathematics.asset",
                quizzes = new List<SubjectQuiz>
                {
                Quiz("Prelims Quiz", "Lessons 1-2: Propositional Logic, Predicates and Quantifiers",
                    Choice("Which of these is a proposition?",
                        "A proposition is either true or false. Questions and commands are not propositions.",
                        "7 is prime.", "Close the door.", "Is it raining?", "Please sit down."),
                    Choice("When is p \u2192 q false?",
                        "A conditional is false only when p is true and q is false.",
                        "Only when p is true and q is false", "When both are false", "Whenever p is false", "Never"),
                    Choice("By De Morgan's law, \u00AC(p \u2227 q) is equivalent to...",
                        "De Morgan: \u00AC(p \u2227 q) \u2261 \u00ACp \u2228 \u00ACq, and \u00AC(p \u2228 q) \u2261 \u00ACp \u2227 \u00ACq.",
                        "\u00ACp \u2228 \u00ACq", "\u00ACp \u2227 \u00ACq", "p \u2228 q", "\u00ACp \u2192 q"),
                    Choice("What does \u2203x P(x) mean?",
                        "\u2203 is the existential quantifier; \u2200 (for all) is the universal quantifier.",
                        "There exists an x such that P(x) is true", "P(x) is true for all x", "P(x) is false for every x", "x is a predicate"),
                    Typed("A statement that is always true is called a... (type the word)",
                        "Tautology: always true. Contradiction: always false. Contingency: sometimes true.",
                        "tautology")),
                Quiz("Midterms Quiz", "Lessons 3-4: Sets, Functions and Relations",
                    Choice("If A = {1, 2, 3} and B = {3, 4}, what is A \u2229 B?",
                        "The intersection keeps the elements in both sets. A \u222A B would be {1, 2, 3, 4}.",
                        "{3}", "{1, 2, 3, 4}", "{1, 2}", "\u2205"),
                    Typed("If |A| = 3, how many subsets does A have? (type the number)",
                        "If |A| = n, then |P(A)| = 2^n. With n = 3, that is 8.",
                        "8", "eight"),
                    Choice("By inclusion-exclusion, |A \u222A B| equals...",
                        "Add both sets, then subtract what was counted twice: |A| + |B| \u2212 |A \u2229 B|.",
                        "|A| + |B| \u2212 |A \u2229 B|", "|A| + |B|", "|A| \u00D7 |B|", "|A \u2229 B|"),
                    Choice("Which functions have inverses?",
                        "Only bijective functions (both one-to-one and onto) have inverses.",
                        "Bijective functions", "Any one-to-one function", "Any onto function", "Every function"),
                    Choice("An equivalence relation is...",
                        "Reflexive, symmetric and transitive; it partitions a set into equivalence classes.",
                        "Reflexive, symmetric, and transitive", "Reflexive only", "Symmetric and one-to-one", "A function with an inverse")),
                Quiz("Prefinals Quiz", "Lessons 5-6: Number Theory and Modular Arithmetic, Proof Techniques and Induction",
                    Typed("What is 17 mod 5? (type the number)",
                        "17 = 5 \u00D7 3 + 2, so 17 mod 5 = 2.",
                        "2", "two"),
                    Choice("What is gcd(48, 18)?",
                        "Euclid: gcd(48, 18) \u2192 gcd(18, 12) \u2192 gcd(12, 6) \u2192 gcd(6, 0) = 6.",
                        "6", "3", "9", "18"),
                    Choice("Which of these relies on large primes?",
                        "Number theory is used in hashing, check digits, clock arithmetic and cryptography; RSA relies on large primes.",
                        "RSA cryptography", "DHCP", "ASCII", "Breadth-first search"),
                    Choice("A proof by contrapositive proves p \u2192 q by proving...",
                        "\u00ACq \u2192 \u00ACp is equivalent to p \u2192 q.",
                        "\u00ACq \u2192 \u00ACp", "q \u2192 p", "\u00ACp \u2192 \u00ACq", "p \u2227 q"),
                    Choice("What are the two steps of mathematical induction?",
                        "Base case (usually n = 1), then the inductive step: assume n = k, prove n = k + 1.",
                        "Base case and inductive step", "Hypothesis and conclusion", "Counterexample and contradiction", "Union and intersection")),
                Quiz("Finals Quiz", "Lessons 7-8: Counting and Probability, Graphs and Trees",
                    Typed("How many 4-digit PINs are possible if digits 0-9 may repeat? (type the number)",
                        "Rule of product: 10 \u00D7 10 \u00D7 10 \u00D7 10 = 10,000.",
                        "10000", "10,000"),
                    Choice("In which counting method does order matter?",
                        "Permutations: order matters. Combinations: order does not matter.",
                        "Permutations", "Combinations", "Sets", "The rule of sum"),
                    Choice("11 items go into 10 boxes, so some box holds at least two. This is the...",
                        "Pigeonhole principle: more than n items in n boxes means some box holds at least two.",
                        "Pigeonhole principle", "Rule of sum", "Inclusion-exclusion", "Induction"),
                    Typed("How many edges does a tree with 12 vertices have? (type the number)",
                        "A tree with n vertices has n \u2212 1 edges.",
                        "11", "eleven"),
                    Choice("Which traversal visits a graph level by level?",
                        "BFS goes level by level; DFS goes as deep as possible first.",
                        "Breadth-first search (BFS)", "Depth-first search (DFS)", "Dijkstra's algorithm", "Induction"))
                },
            };
            yield return new QuizSet
            {
                name = "System Admin and Maintenance",
                path = "Assets/ARCHIV3D/Subjects/1st Year/SAM.asset",
                quizzes = new List<SubjectQuiz>
                {
                Quiz("Prelims Quiz", "Lessons 1-2: Introduction to System Administration, OS Installation and Configuration",
                    Choice("Which part of the CIA triad does a regular backup mainly support?",
                        "Backups keep data and services available after a failure: Availability.",
                        "Availability", "Confidentiality", "Integrity", "Authorization"),
                    Choice("Which is a good sysadmin habit?",
                        "Document changes, follow least privilege, test before deploying to production, and keep a change log.",
                        "Test changes before deploying to production", "Give every user administrator rights", "Skip documentation to save time", "Update production first, test later"),
                    Choice("What lets one machine run several operating systems at once?",
                        "A hypervisor (VirtualBox, VMware, Hyper-V) runs virtual machines; ideal for practice labs and snapshots.",
                        "A hypervisor", "A firewall", "A file system", "A web server"),
                    Typed("Which file system is used by Linux in the lesson? (type its name)",
                        "NTFS for Windows, ext4 for Linux, FAT32/exFAT for removable drives.",
                        "ext4"),
                    Choice("Which belongs on the post-install checklist?",
                        "Set hostname, update the system, install drivers, enable the firewall, remove unused services.",
                        "Enable the firewall", "Turn off updates", "Share the admin password", "Keep every default service running")),
                Quiz("Midterms Quiz", "Lessons 3-4: Users, Groups and Permissions, Command Line and Automation",
                    Choice("What is authentication?",
                        "Authentication proves who you are; authorization decides what you may do.",
                        "Proving who you are", "Deciding what you may do", "Encrypting a disk", "Creating a group"),
                    Typed("In Linux permissions, which number stands for read? (type the number)",
                        "read = 4, write = 2, execute = 1.",
                        "4", "four"),
                    Choice("What does chmod 755 file give?",
                        "7 = 4 + 2 + 1 for the owner (full access); 5 = 4 + 1 (read and execute) for group and others.",
                        "Owner full access; group and others read and execute", "Everyone full access", "Owner read only; others nothing", "Group full access; owner read only"),
                    Choice("Which tool runs jobs automatically on a schedule in Linux?",
                        "cron (Linux) and Task Scheduler (Windows) run jobs automatically.",
                        "cron", "grep", "tar", "ssh"),
                    Choice("Which of these is a PowerShell cmdlet?",
                        "PowerShell cmdlets look like Get-Process; dir, ipconfig and tasklist are classic Windows commands.",
                        "Get-Process", "tasklist", "ls", "grep")),
                Quiz("Prefinals Quiz", "Lessons 5-6: Networking Services and Remote Administration, Security Hardening and Updates",
                    Choice("Which service automatically assigns IP addresses?",
                        "DHCP assigns IP addresses; DNS translates names to IP addresses.",
                        "DHCP", "DNS", "SSH", "RDP"),
                    Choice("A user can reach a site by IP address but not by name. Which service is likely failing?",
                        "Reaching the IP works, so the network is fine; translating the name is DNS's job.",
                        "DNS", "DHCP", "RDP", "The firewall's logging"),
                    Typed("Which secure shell is used for remote access to Linux? (type its 3-letter name)",
                        "SSH for Linux, RDP for Windows. Avoid unencrypted tools such as Telnet.",
                        "SSH"),
                    Choice("Why is leaving unused services running a security risk?",
                        "Hardening disables unused services and ports to shrink the attack surface.",
                        "Each one is another way in for attackers", "They slow down the keyboard", "They delete backups", "They change user passwords"),
                    Choice("BitLocker protects data...",
                        "Disk encryption like BitLocker protects data at rest; HTTPS, VPN and SSH protect data in transit.",
                        "At rest, by encrypting the disk", "In transit over the network", "Only inside emails", "Only in RAM")),
                Quiz("Finals Quiz", "Lessons 7-8: Backup, Recovery and Maintenance, Monitoring and Troubleshooting",
                    Choice("Which backup type needs the least storage and time per run?",
                        "Incremental saves only the changes since the last backup of any type.",
                        "Incremental", "Full", "Differential", "Mirror image"),
                    Choice("In disaster recovery, RTO means...",
                        "RTO: how fast service must return. RPO: how much data loss is acceptable.",
                        "How fast service must return", "How much data loss is acceptable", "How many backups to keep", "How often to clean the fans"),
                    Typed("Which RAID level is mirroring? (type the number)",
                        "RAID 0: speed, no redundancy. RAID 1: mirroring. RAID 5: striping with parity. RAID is not a backup.",
                        "1", "RAID 1", "RAID1"),
                    Choice("What is the first step of the troubleshooting method?",
                        "1 Identify and gather information, 2 theory, 3 test, 4 fix, 5 verify, 6 document.",
                        "Identify the problem and gather information", "Plan and implement a fix", "Document findings and actions", "Test the theory"),
                    Choice("Where does Linux keep its log files?",
                        "Windows Event Viewer and Linux /var/log files reveal errors and security events.",
                        "/var/log", "/home/shared", "/backup", "/bin/bash"))
                },
            };
            yield return new QuizSet
            {
                name = "Data Structures and Algorithms",
                path = "Assets/ARCHIV3D/Subjects/2nd Year/Data Structures and Algorithms.asset",
                quizzes = new List<SubjectQuiz>
                {
                Quiz("Prelims Quiz", "Lessons 1-2: Introduction and Algorithm Analysis, Arrays and Linked Lists",
                    Choice("What is the Big-O of a single loop over n items?",
                        "One loop over n items is O(n); a loop nested inside another is O(n\u00B2).",
                        "O(n)", "O(1)", "O(n\u00B2)", "O(log n)"),
                    Choice("Which of these grows the fastest as n gets bigger?",
                        "From fastest to slowest algorithm: O(1), O(log n), O(n), O(n log n), O(n\u00B2), O(2\u207F).",
                        "O(2\u207F)", "O(n\u00B2)", "O(n log n)", "O(log n)"),
                    Typed("What does ADT stand for? (type the full term)",
                        "An Abstract Data Type defines what operations a structure supports, not how they are implemented.",
                        "abstract data type"),
                    Choice("How fast is accessing an array element by its index?",
                        "Array access by index is O(1); inserting in the middle is O(n) because elements must shift.",
                        "O(1)", "O(n)", "O(log n)", "O(n\u00B2)"),
                    Choice("You frequently insert at the front of a collection. Which structure fits better?",
                        "Linked lists are flexible for frequent insertions; arrays must shift every element.",
                        "A linked list", "A fixed-size array", "A 2D array", "A string")),
                Quiz("Midterms Quiz", "Lessons 3-4: Stacks and Queues, Recursion",
                    Choice("A stack follows which order?",
                        "Stack: LIFO, last in, first out. Queue: FIFO, first in, first out.",
                        "LIFO (last in, first out)", "FIFO (first in, first out)", "Sorted order", "Random order"),
                    Choice("Which of these uses a queue?",
                        "Queues: print jobs, task scheduling, service lines, BFS. Stacks: undo, back button, call stack.",
                        "Print jobs", "An undo feature", "A browser back button", "The call stack"),
                    Typed("Which stack operation looks at the top item without removing it? (type it)",
                        "push adds to the top, pop removes from the top, peek views the top.",
                        "peek", "peek()"),
                    Choice("What stops a recursive function from calling itself forever?",
                        "The base case stops the recursion; the recursive case moves toward it.",
                        "The base case", "The recursive case", "A for loop", "The return type"),
                    Choice("What happens to a recursive function with no base case?",
                        "Without a base case, the program overflows the stack.",
                        "The program overflows the stack", "It runs faster", "It returns 0", "It becomes a loop automatically")),
                Quiz("Prefinals Quiz", "Lessons 5-6: Searching and Sorting, Trees",
                    Choice("What must be true before you use binary search?",
                        "Binary search halves a sorted list by comparing with the middle item.",
                        "The list must be sorted", "The list must be a linked list", "The list must have an even length", "The list must have no duplicates"),
                    Choice("How fast is binary search?",
                        "Binary search is O(log n); linear search is O(n).",
                        "O(log n)", "O(n)", "O(n\u00B2)", "O(1)"),
                    Choice("Which sort is stable but uses extra memory?",
                        "Merge sort: divide, sort each half, merge (stable, extra memory). Quick sort is fast in practice.",
                        "Merge sort", "Quick sort", "Selection sort", "Heap sort"),
                    Typed("Which traversal returns the values of a BST in ascending order? (type it)",
                        "In-order (left, node, right) visits a BST in sorted order.",
                        "in-order", "inorder", "in order"),
                    Choice("When does a BST slow down to O(n)?",
                        "Search, insert and delete average O(log n) but degrade to O(n) if the tree becomes unbalanced.",
                        "When it becomes unbalanced", "When it has a root", "When it has leaves", "When it is a heap")),
                Quiz("Finals Quiz", "Lessons 7-8: Hash Tables and Graphs, Algorithm Design Strategies",
                    Choice("Two different keys produce the same hash index. This is a...",
                        "A collision; it is handled by chaining or open addressing.",
                        "Collision", "Traversal", "Rotation", "Partition"),
                    Choice("Which graph traversal uses a queue?",
                        "BFS uses a queue and finds shortest paths in unweighted graphs; DFS uses a stack or recursion.",
                        "BFS", "DFS", "In-order", "Post-order"),
                    Choice("Fibonacci with memoization is an example of...",
                        "Dynamic programming stores results of overlapping subproblems to avoid recomputation.",
                        "Dynamic programming", "Brute force", "Greedy", "Backtracking"),
                    Typed("Which design strategy tries every possibility? (type it)",
                        "Brute force: simple and correct but often slow.",
                        "brute force", "brute-force"),
                    Choice("Your program does frequent lookups by key. Which structure fits best?",
                        "Frequent lookups by key suggest a hash table; ordered data a BST; first-come-first-served a queue.",
                        "A hash table", "A queue", "A linked list", "A stack"))
                },
            };
        }
    }
}
