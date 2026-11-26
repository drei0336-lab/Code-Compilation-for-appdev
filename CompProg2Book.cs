using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Books > Update Computer Programming 2 Book
    ///
    /// Writes the "Lesson Collection" book into Computer Programming 2 (1st Year, 2nd Semester): a Welcome page, Lessons 1-2 under Prelims, 3-4 Midterms, 5-6 Prefinals, 7-8 Finals, then Study Tips and a Closing page (same format as Computer Programming 1's Course Notes).
    /// It runs inside Unity, so the open Editor and the saved file always match. The book keeps its ID, so
    /// students' progress and bookmarks still work. Safe to run again: it only replaces this book's pages;
    /// other books and quizzes are kept.
    /// </summary>
    static class CompProg2Book
    {
        const string SubjectPath = "Assets/ARCHIV3D/Subjects/1st Year/Computer Programming 2.asset";
        const string SubjectName = "Computer Programming 2";
        const string BookTitle = "Lesson Collection";
        const string BookId = "0567f3eb8999435b9697a0f418e2f19f";

        [MenuItem("Tools/ARCHIV3D/Books/Update Computer Programming 2 Book", false, 49)]
        static void UpdateBook()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then run this again.", "OK");
                return;
            }


            var subject = FindSubject();
            if (subject == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find the " + SubjectName + " subject (" + SubjectPath + ").", "OK");
                return;
            }

            Undo.RecordObject(subject, "Update Computer Programming 2 Book");
            if (subject.materials == null) subject.materials = new List<LearningMaterial>();

            var book = subject.materials.Find(m => m != null && m.title == BookTitle);
            if (book == null)
            {
                book = new LearningMaterial { materialId = BookId };
                subject.materials.Add(book);
            }
            if (string.IsNullOrEmpty(book.materialId)) book.materialId = BookId;

            book.title = BookTitle;
            book.subtitle = "Lessons 1 to 8";
            book.description = "For BSIT students. Eight short lessons that build on programming fundamentals and introduce object-oriented programming. Examples use Java; the concepts carry over to C#, Python, and other OOP languages.";
            book.coverColor = subject.color;
            book.pages = Pages();

            subject.EnsureIds();
            EditorUtility.SetDirty(subject);
            AssetDatabase.SaveAssetIfDirty(subject);
            Selection.activeObject = subject;
            EditorGUIUtility.PingObject(subject);

            var headings = new List<string>();
            foreach (var p in book.pages) headings.Add(p.heading);
            EditorUtility.DisplayDialog("ARCHIV3D",
                subject.DisplayName + " > " + BookTitle + " now has " + book.pages.Count + " pages:\n\n" +
                string.Join(", ", headings) + "\n\nPress Play, then Tools > ARCHIV3D > Preview Bookshelf (Play mode) > " +
                "1st Year to read it.",
                "Nice");
        }

        static SubjectData FindSubject()
        {
            var subject = AssetDatabase.LoadAssetAtPath<SubjectData>(SubjectPath);
            if (subject != null) return subject;
            foreach (string guid in AssetDatabase.FindAssets("t:SubjectData"))
            {
                var s = AssetDatabase.LoadAssetAtPath<SubjectData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.subjectName == SubjectName) return s;
            }
            return null;
        }

        static BookPage Page(string heading, string text) => new BookPage { heading = heading, text = text, picture = null };

        static List<BookPage> Pages()
        {
            return new List<BookPage>
            {
                Page("<i><b>Welcome!</b></i>",
                     "<i>Welcome Reader! This is the 1st year shelf's book.\nThis semester built on Computer Programming 1: we went from writing programs that work to designing programs that grow, with Java for the examples, though the ideas carry over to C# and Python too. The lessons ahead follow the same order as the class: methods, arrays and strings, classes and objects, encapsulation, inheritance and polymorphism, abstraction and interfaces, exceptions and files, and collections.</i>"),
                Page("Prelims",
                     "<b><i>LESSON 1: REVIEW OF PROGRAMMING FUNDAMENTALS AND METHODS</i></b>\n<i>Objectives: Refresh core constructs and write reusable methods.</i>\n\n\u2022 <indent=1.1em><b>Core constructs:</b> variables and data types, operators, selection (<color=#8B4513>if</color> / <color=#8B4513>switch</color>), iteration (<color=#8B4513>for</color> / <color=#8B4513>while</color> / <color=#8B4513>do-while</color>).</indent>\n\n\u2022 <indent=1.1em><b>Method:</b> a named block of code that performs a task. Benefits: reuse, readability, easier testing.</indent>\n\n\u2022 <indent=1.1em><b>Parts of a method:</b> return type, name, parameters, body.</indent>\n\n\u2022 <indent=1.1em><b>Overloading:</b> same method name, different parameter lists.</indent>\n\n\u2022 <indent=1.1em><b>Scope:</b> a variable exists only within the block where it is declared.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>static int square(int n) {\n    return n * n;\n}</noparse></color></indent>\n\n<b>Quick check:</b> <i>What is the difference between a parameter and an argument?</i>"),
                Page("Prelims",
                     "<b><i>LESSON 2: ARRAYS AND STRINGS</i></b>\n<i>Objectives: Store and process collections of values and text.</i>\n\n\u2022 <indent=1.1em><b>Array:</b> a fixed-size, indexed collection of same-type elements. Indexes start at 0; the last index is <color=#8B4513>length - 1</color>.</indent>\n\n\u2022 <indent=1.1em><b>Common array tasks:</b> traversal, searching (linear search), finding min/max, sorting (e.g., bubble sort), summing.</indent>\n\n\u2022 <indent=1.1em><b>2D arrays:</b> rows and columns, processed with nested loops.</indent>\n\n\u2022 <indent=1.1em><b>Strings:</b> sequences of characters; in Java they are immutable objects. Useful methods: <color=#8B4513>length()</color>, <color=#8B4513>charAt()</color>, <color=#8B4513>substring()</color>, <color=#8B4513>equals()</color>, <color=#8B4513>toUpperCase()</color>, <color=#8B4513>split()</color>.</indent>\n\n\u2022 <indent=1.1em><b>Pitfall:</b> compare strings with <color=#8B4513>equals()</color>, not <color=#8B4513>==</color>.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>int[] scores = {88, 92, 75};\nint total = 0;\nfor (int s : scores) total += s;</noparse></color></indent>\n\n<b>Quick check:</b> <i>What happens if you access <color=#8B4513>scores[3]</color> in the array above?</i>"),
                Page("Midterms",
                     "<b><i>LESSON 3: INTRODUCTION TO OBJECT-ORIENTED PROGRAMMING</i></b>\n<i>Objectives: Explain classes and objects and create them.</i>\n\n\u2022 <indent=1.1em><b>Class:</b> a blueprint defining attributes (fields) and behaviors (methods).</indent>\n\n\u2022 <indent=1.1em><b>Object:</b> an instance of a class with its own data.</indent>\n\n\u2022 <indent=1.1em><b>Constructor:</b> special method that initializes a new object.</indent>\n\n\u2022 <indent=1.1em><b><color=#8B4513>this</color> keyword:</b> refers to the current object.</indent>\n\n\u2022 <indent=1.1em><b>Why OOP:</b> models real-world entities, organizes large programs, and encourages reuse.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>class Student {\n    String name;\n    int year;\n\n    Student(String name, int year) {\n        this.name = name;\n        this.year = year;\n    }\n\n    void introduce() {\n        System.out.println(name + \", Year \" + year);\n    }\n}</noparse></color></indent>\n\n<b>Quick check:</b> <i>What is the difference between a class and an object?</i>"),
                Page("Midterms",
                     "<b><i>LESSON 4: ENCAPSULATION AND ACCESS CONTROL</i></b>\n<i>Objectives: Protect object data and control access to it.</i>\n\n\u2022 <indent=1.1em><b>Encapsulation:</b> bundling data and methods together and hiding internal details.</indent>\n\n\u2022 <indent=1.1em><b>Access modifiers:</b> <color=#8B4513>private</color> (class only), <color=#8B4513>protected</color> (class, subclasses, package), <color=#8B4513>public</color> (everywhere), default (package).</indent>\n\n\u2022 <indent=1.1em><b>Getters and setters:</b> controlled access to <color=#8B4513>private</color> fields, allowing validation.</indent>\n\n\u2022 <indent=1.1em><b>Static members:</b> belong to the class, not to any single object (e.g., a counter of objects created).</indent>\n\n<indent=1.1em><color=#8B4513><noparse>class Account {\n    private double balance;\n\n    public double getBalance() { return balance; }\n\n    public void deposit(double amount) {\n        if (amount > 0) balance += amount;\n    }\n}</noparse></color></indent>\n\n<b>Quick check:</b> <i>Why make <color=#8B4513>balance</color> private instead of public?</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 5: INHERITANCE AND POLYMORPHISM</i></b>\n<i>Objectives: Reuse code through inheritance and apply polymorphism.</i>\n\n\u2022 <indent=1.1em><b>Inheritance:</b> a subclass acquires fields and methods of a superclass using <color=#8B4513>extends</color>. It models an is-a relationship (a <color=#8B4513>Student</color> is a <color=#8B4513>Person</color>).</indent>\n\n\u2022 <indent=1.1em><b><color=#8B4513>super</color>:</b> calls the superclass constructor or methods.</indent>\n\n\u2022 <indent=1.1em><b>Method overriding:</b> a subclass provides its own version of an inherited method. Use <color=#8B4513>@Override</color>.</indent>\n\n\u2022 <indent=1.1em><b>Polymorphism:</b> one interface, many forms. A superclass reference can refer to subclass objects, and the correct overridden method runs at runtime.</indent>\n\n\u2022 <indent=1.1em><b>Overloading vs. overriding:</b> overloading = same class, different parameters (compile time); overriding = subclass, same signature (runtime).</indent>\n\n<indent=1.1em><color=#8B4513><noparse>class Animal { void sound() { System.out.println(\"...\"); } }\nclass Dog extends Animal {\n    @Override void sound() { System.out.println(\"Woof\"); }\n}\nAnimal a = new Dog();\na.sound();   // prints Woof</noparse></color></indent>\n\n<b>Quick check:</b> <i>Is a <color=#8B4513>Car</color> has-a or is-a relationship with <color=#8B4513>Engine</color>?</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 6: ABSTRACTION, ABSTRACT CLASSES, AND INTERFACES</i></b>\n<i>Objectives: Design flexible systems using abstraction.</i>\n\n\u2022 <indent=1.1em><b>Abstraction:</b> exposing what an object does while hiding how it does it.</indent>\n\n\u2022 <indent=1.1em><b>Abstract class:</b> cannot be instantiated; may contain abstract (unimplemented) and concrete methods. Use when related classes share code.</indent>\n\n\u2022 <indent=1.1em><b>Interface:</b> a contract of methods a class promises to implement; a class can implement many interfaces. Use when unrelated classes share a capability.</indent>\n\n\u2022 <indent=1.1em><b>Choosing:</b> abstract class for shared state and behavior; interface for a shared capability.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>interface Payable {\n    double computePay();\n}\nabstract class Employee implements Payable {\n    abstract double computePay();\n}</noparse></color></indent>\n\n<b>Quick check:</b> <i>Why can't you create an object directly from an abstract class?</i>"),
                Page("Finals",
                     "<b><i>LESSON 7: EXCEPTION HANDLING AND FILE I/O</i></b>\n<i>Objectives: Write programs that handle errors and persist data.</i>\n\n\u2022 <indent=1.1em><b>Exception:</b> an unexpected event that disrupts normal flow (e.g., dividing by zero, missing file, invalid input).</indent>\n\n\u2022 <indent=1.1em><b>Handling:</b> <color=#8B4513>try</color> (risky code), <color=#8B4513>catch</color> (respond to error), <color=#8B4513>finally</color> (always runs, for cleanup). <color=#8B4513>throw</color> raises an exception; <color=#8B4513>throws</color> declares one.</indent>\n\n\u2022 <indent=1.1em><b>Checked vs. unchecked:</b> checked exceptions (e.g., <color=#8B4513>IOException</color>) must be handled or declared; unchecked (e.g., <color=#8B4513>NullPointerException</color>) usually indicate bugs.</indent>\n\n\u2022 <indent=1.1em><b>File I/O:</b> read and write text files with <color=#8B4513>Scanner</color>, <color=#8B4513>FileReader</color> / <color=#8B4513>BufferedReader</color>, <color=#8B4513>FileWriter</color> / <color=#8B4513>PrintWriter</color>. Use try-with-resources to close files automatically.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>try (BufferedReader br = new BufferedReader(new FileReader(\"data.txt\"))) {\n    System.out.println(br.readLine());\n} catch (IOException e) {\n    System.out.println(\"Could not read file.\");\n}</noparse></color></indent>\n\n<b>Quick check:</b> <i>When does the <color=#8B4513>finally</color> block run?</i>"),
                Page("Finals",
                     "<b><i>LESSON 8: COLLECTIONS, BASIC GUI, AND MINI-PROJECT</i></b>\n<i>Objectives: Use dynamic data structures and apply all concepts in a small application.</i>\n\n\u2022 <indent=1.1em><b>Collections Framework:</b> <color=#8B4513>ArrayList</color> (resizable list), <color=#8B4513>HashMap</color> (key-value pairs), <color=#8B4513>HashSet</color> (unique items). Prefer <color=#8B4513>ArrayList</color> over arrays when the size changes.</indent>\n\n\u2022 <indent=1.1em><b>Generics:</b> specify the element type, e.g., <color=#8B4513><noparse>ArrayList<String></noparse></color>, for type safety.</indent>\n\n\u2022 <indent=1.1em><b>Event-driven GUI (overview):</b> components (buttons, text fields, labels), layouts, and event listeners that react to user actions. Common toolkits: Swing or JavaFX.</indent>\n\n\u2022 <indent=1.1em><b>Mini-project ideas:</b> student record manager, library borrowing system, simple inventory with file storage. Apply classes, inheritance, collections, exceptions, and file I/O together.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>ArrayList<String> names = new ArrayList<>();\nnames.add(\"Ana\");\nnames.add(\"Ben\");\nfor (String n : names) System.out.println(n);</noparse></color></indent>\n\n<b>Quick check:</b> <i>When would you choose a <color=#8B4513>HashMap</color> over an <color=#8B4513>ArrayList</color>?</i>"),
                Page("Study Tips",
                     "<b><i>SUGGESTED STUDY TIPS</i></b>\n\n\u2022 <indent=1.1em>Type out and run every example; do not just read it.</indent>\n\n\u2022 <indent=1.1em>Sketch classes and relationships (a simple UML diagram) before coding.</indent>\n\n\u2022 <indent=1.1em>Practice debugging: read error messages top to bottom and test small pieces.</indent>\n\n\u2022 <indent=1.1em>Build a small project early and add one feature per lesson.</indent>"),
                Page("Closing",
                     "That is the bridge from writing programs to designing them: reusable methods, arrays and strings, classes and objects, encapsulation, inheritance and polymorphism, abstraction, exceptions and files, and collections. Every bigger program you build from here, in Java, C# or Python, stands on these eight lessons.")
            };
        }
    }
}
