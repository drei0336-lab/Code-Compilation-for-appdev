using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Books > Update Data Structures and Algorithms Book
    ///
    /// Writes the "Lesson Collection" book into Data Structures and Algorithms (2nd Year, 1st Semester): a Welcome page, Lessons 1-2 under Prelims, 3-4 Midterms, 5-6 Prefinals, 7-8 Finals, then Study Tips and a Closing page, followed by "~ The End ~", which marks it finished.
    /// It runs inside Unity, so the open Editor and the saved file always match. The book keeps its ID, so
    /// students' progress and bookmarks still work. Safe to run again: it only replaces this book's pages;
    /// other books and quizzes are kept.
    /// </summary>
    static class DataStructuresBook
    {
        const string SubjectPath = "Assets/ARCHIV3D/Subjects/2nd Year/Data Structures and Algorithms.asset";
        const string SubjectName = "Data Structures and Algorithms";
        const string BookTitle = "Lesson Collection";
        const string BookId = "87a45da210be4e0893be9b039d613a19";

        [MenuItem("Tools/ARCHIV3D/Books/Update Data Structures and Algorithms Book", false, 45)]
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

            Undo.RecordObject(subject, "Update Data Structures and Algorithms Book");
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
            book.description = "For BSIT students. Eight short lessons on organizing data efficiently and designing algorithms. Examples use Java-style code; the ideas apply to any language.";
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
                "2nd Year to read it.",
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
                     "<i>Welcome Reader! This is the 2nd year shelf's book.\nThis semester was about organizing data so programs stay fast as they grow, and designing the algorithms that work on it. The examples use Java-style code, but the ideas apply to any language. The lessons ahead follow the same order as the class: algorithm analysis, arrays and linked lists, stacks and queues, recursion, searching and sorting, trees, hash tables and graphs, and algorithm design strategies.</i>"),
                Page("Prelims",
                     "<b><i>LESSON 1: INTRODUCTION AND ALGORITHM ANALYSIS</i></b>\n<i>Objectives: Explain what data structures and algorithms are and measure efficiency.</i>\n\n\u2022 <indent=1.1em><b>Data structure:</b> a way of organizing and storing data so it can be used efficiently (e.g., array, list, tree).</indent>\n\n\u2022 <indent=1.1em><b>Algorithm:</b> a finite, ordered set of steps that solves a problem. Good algorithms are correct, clear, and efficient.</indent>\n\n\u2022 <indent=1.1em><b>Abstract Data Type (ADT):</b> defines what operations a structure supports, not how they are implemented (e.g., Stack, Queue).</indent>\n\n\u2022 <indent=1.1em><b>Time and space complexity:</b> how running time and memory grow as input size n grows.</indent>\n\n\u2022 <indent=1.1em><b>Big-O notation</b> (worst case, common classes from fastest to slowest):</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>O(1) constant</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>O(log n) logarithmic</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>O(n) linear</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>O(n log n) linearithmic</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>O(n\u00B2) quadratic</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>O(2<sup>n</sup>) exponential</indent>\n\n\u2022 <indent=1.1em><b>Counting steps:</b> a single loop over n items is O(n); a loop nested inside another is O(n\u00B2).</indent>\n\n<indent=1.1em><color=#8B4513><noparse>for (int i = 0; i < n; i++) {        // runs n times\n    for (int j = 0; j < n; j++) {    // runs n times for each i\n        count++;                     // total: n * n = O(n\u00B2)\n    }\n}</noparse></color></indent>\n\n<b>Quick check:</b> <i>What is the Big-O of a single loop that prints every element of an array?</i>"),
                Page("Prelims",
                     "<b><i>LESSON 2: ARRAYS AND LINKED LISTS</i></b>\n<i>Objectives: Compare contiguous and linked storage.</i>\n\n\u2022 <indent=1.1em><b>Array:</b> fixed-size block of memory. Access by index is O(1); inserting or deleting in the middle is O(n) because elements must shift.</indent>\n\n\u2022 <indent=1.1em><b>Linked list:</b> nodes that each hold data and a reference (pointer) to the next node. Insertion or deletion at a known position is O(1); access by position is O(n).</indent>\n\n\u2022 <indent=1.1em><b>Variants:</b> singly linked (one direction), doubly linked (next and previous), circular (last node points back to the first).</indent>\n\n\u2022 <indent=1.1em><b>Trade-off:</b> arrays are fast for random access; linked lists are flexible for frequent insertions and deletions.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>class Node {\n    int data;\n    Node next;\n    Node(int data) { this.data = data; }\n}</noparse></color></indent>\n\n<b>Quick check:</b> <i>Which structure is better when you frequently insert at the front of a collection?</i>"),
                Page("Midterms",
                     "<b><i>LESSON 3: STACKS AND QUEUES</i></b>\n<i>Objectives: Apply LIFO and FIFO structures.</i>\n\n\u2022 <indent=1.1em><b>Stack (LIFO, last in, first out):</b> operations <color=#8B4513>push</color> (add to top), <color=#8B4513>pop</color> (remove from top), <color=#8B4513>peek</color> (view top). All run in O(1).</indent>\n\n\u2022 <indent=1.1em><b>Stack uses:</b> undo features, browser back button, function call tracking (call stack), balancing parentheses, expression evaluation.</indent>\n\n\u2022 <indent=1.1em><b>Queue (FIFO, first in, first out):</b> operations <color=#8B4513>enqueue</color> (add at rear), <color=#8B4513>dequeue</color> (remove from front). Also O(1).</indent>\n\n\u2022 <indent=1.1em><b>Queue uses:</b> print jobs, task scheduling, customer service lines, breadth-first search.</indent>\n\n\u2022 <indent=1.1em><b>Variants:</b> circular queue, double-ended queue (deque), priority queue (highest priority leaves first).</indent>\n\n<b>Quick check:</b> <i>Which structure would you use to check whether <color=#8B4513>{ [ ( ) ] }</color> has balanced brackets?</i>"),
                Page("Midterms",
                     "<b><i>LESSON 4: RECURSION</i></b>\n<i>Objectives: Solve problems by having a function call itself.</i>\n\n\u2022 <indent=1.1em><b>Recursion:</b> a function that solves a problem by solving smaller versions of the same problem.</indent>\n\n\u2022 <indent=1.1em><b>Two required parts:</b> a base case (stops the recursion) and a recursive case (moves toward the base case). Without a base case, the program overflows the stack.</indent>\n\n\u2022 <indent=1.1em><b>Classic examples:</b> factorial, Fibonacci, binary search, tree traversal, Tower of Hanoi.</indent>\n\n\u2022 <indent=1.1em><b>Recursion vs. iteration:</b> recursion is often clearer for naturally nested problems; iteration usually uses less memory.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>static int factorial(int n) {\n    if (n <= 1) return 1;              // base case\n    return n * factorial(n - 1);       // recursive case\n}</noparse></color></indent>\n\n<b>Quick check:</b> <i>What is the base case in the factorial function above?</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 5: SEARCHING AND SORTING</i></b>\n<i>Objectives: Choose and trace common searching and sorting algorithms.</i>\n\n\u2022 <indent=1.1em><b>Linear search:</b> check each item in turn. O(n); works on unsorted data.</indent>\n\n\u2022 <indent=1.1em><b>Binary search:</b> repeatedly halve a sorted list by comparing with the middle item. O(log n).</indent>\n\n\u2022 <indent=1.1em><b>Simple sorts (O(n\u00B2)):</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Bubble sort: repeatedly swap adjacent out-of-order items</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Selection sort: repeatedly select the smallest remaining item</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Insertion sort: insert each item into its place in a sorted portion; good for small or nearly sorted data</indent>\n\n\u2022 <indent=1.1em><b>Efficient sorts (O(n log n) typical):</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Merge sort: divide in half, sort each half, merge (stable, uses extra memory)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Quick sort: partition around a pivot, sort the parts (fast in practice; worst case O(n\u00B2))</indent>\n\n\u2022 <indent=1.1em><b>Stable sort:</b> keeps equal items in their original order.</indent>\n\n<indent=1.1em><color=#8B4513><noparse>// Binary search on a sorted array\nint lo = 0, hi = a.length - 1;\nwhile (lo <= hi) {\n    int mid = (lo + hi) / 2;\n    if (a[mid] == target) return mid;\n    else if (a[mid] < target) lo = mid + 1;\n    else hi = mid - 1;\n}\nreturn -1;</noparse></color></indent>\n\n<b>Quick check:</b> <i>Why must the list be sorted before using binary search?</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 6: TREES</i></b>\n<i>Objectives: Represent hierarchical data and traverse trees.</i>\n\n\u2022 <indent=1.1em><b>Tree:</b> a hierarchical structure of nodes with one root; each node has children; nodes with no children are leaves. Key terms: parent, sibling, depth, height.</indent>\n\n\u2022 <indent=1.1em><b>Binary tree:</b> each node has at most two children (left and right).</indent>\n\n\u2022 <indent=1.1em><b>Binary Search Tree (BST):</b> for every node, values in the left subtree are smaller and values in the right subtree are larger. Search, insert, and delete average O(log n) but degrade to O(n) if the tree becomes unbalanced.</indent>\n\n\u2022 <indent=1.1em><b>Traversals:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>In-order (left, node, right): visits a BST in sorted order</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Pre-order (node, left, right): useful for copying a tree</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Post-order (left, right, node): useful for deleting a tree</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Level-order: visit level by level using a queue</indent>\n\n\u2022 <indent=1.1em><b>Related structures:</b> AVL and red-black trees (self-balancing), heaps (used for priority queues and heap sort).</indent>\n\n<b>Quick check:</b> <i>Which traversal returns the values of a BST in ascending order?</i>"),
                Page("Finals",
                     "<b><i>LESSON 7: HASH TABLES AND GRAPHS</i></b>\n<i>Objectives: Use key-value lookup and model networks of relationships.</i>\n\n\u2022 <indent=1.1em><b>Hash table:</b> stores key-value pairs; a hash function converts a key into an array index. Average lookup, insert, and delete are O(1).</indent>\n\n\u2022 <indent=1.1em><b>Collisions:</b> when two keys map to the same index. Handled by chaining (a list at each slot) or open addressing (probe for the next free slot).</indent>\n\n\u2022 <indent=1.1em><b>Uses:</b> dictionaries, caches, database indexes, counting frequencies (Java <color=#8B4513>HashMap</color>, Python <color=#8B4513>dict</color>).</indent>\n\n\u2022 <indent=1.1em><b>Graph:</b> vertices (nodes) connected by edges; can be directed or undirected, weighted or unweighted.</indent>\n\n\u2022 <indent=1.1em><b>Representations:</b> adjacency matrix (fast edge lookup, more memory) and adjacency list (compact for sparse graphs).</indent>\n\n\u2022 <indent=1.1em><b>Traversals:</b> BFS (breadth-first, uses a queue, finds shortest path in unweighted graphs) and DFS (depth-first, uses a stack or recursion).</indent>\n\n\u2022 <indent=1.1em><b>Uses:</b> maps and routing, social networks, network topology, dependency tracking.</indent>\n\n<b>Quick check:</b> <i>What happens when two different keys produce the same hash index, and how can it be resolved?</i>"),
                Page("Finals",
                     "<b><i>LESSON 8: ALGORITHM DESIGN STRATEGIES</i></b>\n<i>Objectives: Recognize common approaches to solving problems.</i>\n\n\u2022 <indent=1.1em><b>Brute force:</b> try every possibility. Simple and correct but often slow.</indent>\n\n\u2022 <indent=1.1em><b>Divide and conquer:</b> split into smaller subproblems, solve each, combine results (merge sort, binary search).</indent>\n\n\u2022 <indent=1.1em><b>Greedy:</b> make the best local choice at each step, hoping for a global optimum (e.g., Dijkstra's shortest path, coin change with standard denominations, Huffman coding).</indent>\n\n\u2022 <indent=1.1em><b>Dynamic programming:</b> store results of overlapping subproblems to avoid recomputation (e.g., Fibonacci with memoization, knapsack, longest common subsequence).</indent>\n\n\u2022 <indent=1.1em><b>Backtracking:</b> build a solution step by step and undo choices that fail (e.g., N-Queens, maze solving, Sudoku).</indent>\n\n\u2022 <indent=1.1em><b>Choosing a structure:</b> match the structure to the operations you need most. Frequent lookups by key suggest a hash table; ordered data suggests a BST; first-come-first-served suggests a queue.</indent>\n\n<b>Quick check:</b> <i>Why is plain recursive Fibonacci slow, and how does dynamic programming fix it?</i>"),
                Page("Study Tips",
                     "<b><i>SUGGESTED STUDY TIPS</i></b>\n\n\u2022 <indent=1.1em>Trace algorithms by hand on small inputs with pencil and paper before coding them.</indent>\n\n\u2022 <indent=1.1em>Draw diagrams of lists, trees, and graphs while working through operations.</indent>\n\n\u2022 <indent=1.1em>Implement each structure from scratch at least once, then learn the built-in library version.</indent>\n\n\u2022 <indent=1.1em>Practice on coding platforms and always state the time and space complexity of your solution.</indent>"),
                Page("Closing",
                     "That is the toolkit behind fast software: measuring efficiency with Big-O, choosing between arrays, lists, stacks, queues, trees, hash tables and graphs, thinking recursively, searching and sorting well, and picking a design strategy that fits the problem. Every program you write from here builds on these eight lessons.")
            };
        }
    }
}
