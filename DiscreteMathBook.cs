using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Books > Update Discrete Mathematics Book
    ///
    /// Writes the "Lesson Collection" book into Discrete Mathematics (1st Year, 2nd Semester): a Welcome page, Lessons 1-2 under Prelims, 3-4 Midterms, 5-6 Prefinals, 7-8 Finals, then Study Tips and a Closing page (same format as Computer Programming 1's Course Notes). It first runs Add Math Symbols Font, so the logic and set symbols don't show as boxes.
    /// It runs inside Unity, so the open Editor and the saved file always match. The book keeps its ID, so
    /// students' progress and bookmarks still work. Safe to run again: it only replaces this book's pages;
    /// other books and quizzes are kept.
    /// </summary>
    static class DiscreteMathBook
    {
        const string SubjectPath = "Assets/ARCHIV3D/Subjects/1st Year/Discrete Mathematics.asset";
        const string SubjectName = "Discrete Mathematics";
        const string BookTitle = "Lesson Collection";
        const string BookId = "8dcafeb0f03a4aabb6d1b16726fa82f4";

        [MenuItem("Tools/ARCHIV3D/Books/Update Discrete Mathematics Book", false, 48)]
        static void UpdateBook()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Stop Play mode first, then run this again.", "OK");
                return;
            }

            int fontsUpdated = SymbolFontSetup.Ensure(out string fontError);

            var subject = FindSubject();
            if (subject == null)
            {
                EditorUtility.DisplayDialog("ARCHIV3D", "Couldn't find the " + SubjectName + " subject (" + SubjectPath + ").", "OK");
                return;
            }

            Undo.RecordObject(subject, "Update Discrete Mathematics Book");
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
            book.description = "For BSIT students. Eight short lessons covering the math behind computing: logic, sets, proofs, counting, relations, and graphs.";
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
                "1st Year to read it." +
                (fontError != null ? "\n\nMath symbols font: " + fontError : fontsUpdated > 0 ? "\n\nAdded the math symbols font to " + fontsUpdated + " ARCHIV3D font(s), so the logic and set symbols show." : ""),
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
                     "<i>Welcome Reader! This is the 1st year shelf's book.\nThis semester was about the math behind computing: the logic, sets and structures that every program quietly relies on. The lessons ahead follow the same order as the class: propositional logic, predicates and quantifiers, sets, functions and relations, number theory, proofs and induction, counting and probability, and graphs and trees.</i>"),
                Page("Prelims",
                     "<b><i>LESSON 1: PROPOSITIONAL LOGIC</i></b>\n<i>Objectives: Evaluate logical statements and build truth tables.</i>\n\n\u2022 <indent=1.1em><b>Proposition:</b> a statement that is either true or false (e.g., \"7 is prime\"). Questions and commands are not propositions.</indent>\n\n\u2022 <indent=1.1em><b>Connectives:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Negation: \u00ACp (not p)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Conjunction: p \u2227 q (p and q); true only when both are true</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Disjunction: p \u2228 q (p or q); false only when both are false</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Conditional: p \u2192 q (if p then q); false only when p is true and q is false</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Biconditional: p \u2194 q; true when p and q have the same truth value</indent>\n\n\u2022 <indent=1.1em><b>Tautology:</b> always true. <b>Contradiction:</b> always false. <b>Contingency:</b> sometimes true, sometimes false.</indent>\n\n\u2022 <indent=1.1em><b>Key equivalences:</b> De Morgan's laws: \u00AC(p \u2227 q) \u2261 \u00ACp \u2228 \u00ACq and \u00AC(p \u2228 q) \u2261 \u00ACp \u2227 \u00ACq. Also p \u2192 q \u2261 \u00ACp \u2228 q.</indent>\n\n\u2022 <indent=1.1em><b>IT link:</b> conditions in if statements, database queries, and digital circuits.</indent>\n\n<b>Quick check:</b> <i>Is p \u2192 q true when p is false and q is true?</i>"),
                Page("Prelims",
                     "<b><i>LESSON 2: PREDICATES AND QUANTIFIERS</i></b>\n<i>Objectives: Express statements about groups of objects.</i>\n\n\u2022 <indent=1.1em><b>Predicate:</b> a statement with variables, such as P(x): \"x is even\". It becomes a proposition once x is given a value.</indent>\n\n\u2022 <indent=1.1em><b>Universal quantifier:</b> \u2200x P(x) means \"for all x, P(x) is true\".</indent>\n\n\u2022 <indent=1.1em><b>Existential quantifier:</b> \u2203x P(x) means \"there exists an x such that P(x) is true\".</indent>\n\n\u2022 <indent=1.1em><b>Negation rules:</b> \u00AC\u2200x P(x) \u2261 \u2203x \u00ACP(x), and \u00AC\u2203x P(x) \u2261 \u2200x \u00ACP(x).</indent>\n\n\u2022 <indent=1.1em><b>Example:</b> \"Every student has a laptop\" is \u2200x Laptop(x). Its negation: \"Some student has no laptop.\"</indent>\n\n<b>Quick check:</b> <i>Negate \"All servers are online.\"</i>"),
                Page("Midterms",
                     "<b><i>LESSON 3: SETS</i></b>\n<i>Objectives: Use set notation and perform set operations.</i>\n\n\u2022 <indent=1.1em><b>Set:</b> an unordered collection of distinct elements, written {1, 2, 3}. Notation: x \u2208 A (element of), A \u2286 B (subset), \u2205 (empty set), |A| (cardinality).</indent>\n\n\u2022 <indent=1.1em><b>Operations:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Union: A \u222A B (in A or B)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Intersection: A \u2229 B (in both)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Difference: A \u2212 B (in A, not in B)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Complement: A\u2032 (not in A, relative to the universal set U)</indent>\n\n\u2022 <indent=1.1em><b>Power set:</b> P(A) is the set of all subsets; if |A| = n, then |P(A)| = 2<sup>n</sup>.</indent>\n\n\u2022 <indent=1.1em><b>Inclusion-exclusion:</b> |A \u222A B| = |A| + |B| \u2212 |A \u2229 B|.</indent>\n\n\u2022 <indent=1.1em><b>IT link:</b> database tables, SQL <color=#8B4513>UNION</color> / <color=#8B4513>INTERSECT</color>, permissions, and tags.</indent>\n\n<b>Quick check:</b> <i>If A = {1, 2, 3} and B = {3, 4}, find A \u222A B and A \u2229 B.</i>"),
                Page("Midterms",
                     "<b><i>LESSON 4: FUNCTIONS AND RELATIONS</i></b>\n<i>Objectives: Describe mappings and classify relations.</i>\n\n\u2022 <indent=1.1em><b>Function:</b> assigns each input in the domain exactly one output in the codomain; written f: A \u2192 B.</indent>\n\n\u2022 <indent=1.1em><b>Types:</b> one-to-one (injective), onto (surjective), and bijective (both). Only bijective functions have inverses.</indent>\n\n\u2022 <indent=1.1em><b>Relation:</b> a set of ordered pairs from A \u00D7 B.</indent>\n\n\u2022 <indent=1.1em><b>Properties of a relation on a set:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Reflexive: every a relates to itself</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Symmetric: if aRb then bRa</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Transitive: if aRb and bRc then aRc</indent>\n\n\u2022 <indent=1.1em><b>Equivalence relation:</b> reflexive, symmetric, and transitive. It partitions a set into equivalence classes.</indent>\n\n\u2022 <indent=1.1em><b>IT link:</b> relational databases are built on relations; hash functions map keys to values.</indent>\n\n<b>Quick check:</b> <i>Is \"is equal to\" an equivalence relation? Is \"is less than\"?</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 5: NUMBER THEORY AND MODULAR ARITHMETIC</i></b>\n<i>Objectives: Apply divisibility, modular arithmetic, and base conversion.</i>\n\n\u2022 <indent=1.1em><b>Divisibility:</b> a | b means b is a multiple of a. <b>Prime:</b> exactly two positive divisors (1 and itself).</indent>\n\n\u2022 <indent=1.1em><b>Division algorithm:</b> a = bq + r, where <noparse>0 \u2264 r < b</noparse>.</indent>\n\n\u2022 <indent=1.1em><b>GCD:</b> the largest common divisor, found with the Euclidean algorithm: gcd(a, b) = gcd(b, a mod b).</indent>\n\n\u2022 <indent=1.1em><b>Modular arithmetic:</b> a \u2261 b (mod n) when n divides a \u2212 b. Example: 17 mod 5 = 2.</indent>\n\n\u2022 <indent=1.1em><b>Applications:</b> hashing, check digits, clock arithmetic, and cryptography (e.g., RSA relies on large primes).</indent>\n\n<indent=1.1em><color=#8B4513><noparse>gcd(48, 18) \u2192 gcd(18, 12) \u2192 gcd(12, 6) \u2192 gcd(6, 0) = 6</noparse></color></indent>\n\n<b>Quick check:</b> <i>Compute 29 mod 7 and gcd(36, 24).</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 6: PROOF TECHNIQUES AND INDUCTION</i></b>\n<i>Objectives: Construct basic mathematical proofs.</i>\n\n\u2022 <indent=1.1em><b>Direct proof:</b> assume the hypothesis and show the conclusion follows step by step.</indent>\n\n\u2022 <indent=1.1em><b>Proof by contrapositive:</b> prove \u00ACq \u2192 \u00ACp instead of p \u2192 q (they are equivalent).</indent>\n\n\u2022 <indent=1.1em><b>Proof by contradiction:</b> assume the statement is false and derive an impossibility.</indent>\n\n\u2022 <indent=1.1em><b>Counterexample:</b> a single case that disproves a universal claim.</indent>\n\n\u2022 <indent=1.1em><b>Mathematical induction:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>1. Base case: show the statement holds for the first value (usually n = 1).</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>2. Inductive step: assume it holds for n = k, then prove it for n = k + 1.</indent>\n\n\u2022 <indent=1.1em><b>Example:</b> 1 + 2 + \u2026 + n = n(n + 1)/2 can be proven by induction.</indent>\n\n\u2022 <indent=1.1em><b>IT link:</b> proving correctness of loops and recursive algorithms.</indent>\n\n<b>Quick check:</b> <i>Why is a single counterexample enough to disprove \"all primes are odd\"?</i>"),
                Page("Finals",
                     "<b><i>LESSON 7: COUNTING AND PROBABILITY</i></b>\n<i>Objectives: Count arrangements and selections.</i>\n\n\u2022 <indent=1.1em><b>Rule of sum:</b> if tasks are alternatives with m and n ways, there are m + n ways total.</indent>\n\n\u2022 <indent=1.1em><b>Rule of product:</b> if tasks are done in sequence with m and n ways, there are m \u00D7 n ways total.</indent>\n\n\u2022 <indent=1.1em><b>Permutations</b> (order matters): P(n, r) = n! / (n \u2212 r)!</indent>\n\n\u2022 <indent=1.1em><b>Combinations</b> (order does not matter): C(n, r) = n! / (r!(n \u2212 r)!)</indent>\n\n\u2022 <indent=1.1em><b>Pigeonhole principle:</b> if more than n items go into n boxes, some box holds at least two.</indent>\n\n\u2022 <indent=1.1em><b>Basic probability:</b> P(E) = favorable outcomes / total outcomes.</indent>\n\n\u2022 <indent=1.1em><b>Example:</b> the number of 4-digit PINs (digits 0-9, repeats allowed) is 10<sup>4</sup> = 10,000.</indent>\n\n\u2022 <indent=1.1em><b>IT link:</b> password strength, algorithm analysis, and data compression.</indent>\n\n<b>Quick check:</b> <i>In how many ways can 3 officers be chosen from 10 members if each holds a different position?</i>"),
                Page("Finals",
                     "<b><i>LESSON 8: GRAPHS AND TREES</i></b>\n<i>Objectives: Model connections using graphs and trees.</i>\n\n\u2022 <indent=1.1em><b>Graph:</b> a set of vertices (nodes) joined by edges. Can be directed or undirected, weighted or unweighted.</indent>\n\n\u2022 <indent=1.1em><b>Terms:</b> degree (edges at a vertex), path, cycle, connected graph.</indent>\n\n\u2022 <indent=1.1em><b>Representations:</b> adjacency matrix and adjacency list.</indent>\n\n\u2022 <indent=1.1em><b>Traversal:</b> breadth-first search (BFS, level by level) and depth-first search (DFS, as deep as possible first).</indent>\n\n\u2022 <indent=1.1em><b>Shortest path:</b> Dijkstra's algorithm for weighted graphs.</indent>\n\n\u2022 <indent=1.1em><b>Tree:</b> a connected graph with no cycles; a tree with n vertices has n \u2212 1 edges. A rooted tree has a root, parents, children, and leaves; a binary tree has at most two children per node.</indent>\n\n\u2022 <indent=1.1em><b>IT link:</b> networks and routing, social networks, file systems, and decision trees.</indent>\n\n<b>Quick check:</b> <i>How many edges does a tree with 12 vertices have?</i>"),
                Page("Study Tips",
                     "<b><i>SUGGESTED STUDY TIPS</i></b>\n\n\u2022 <indent=1.1em>Work through truth tables and small examples by hand before relying on shortcuts.</indent>\n\n\u2022 <indent=1.1em>Translate English statements into logic symbols, and back again, for practice.</indent>\n\n\u2022 <indent=1.1em>Draw diagrams: Venn diagrams for sets, node-and-edge sketches for graphs.</indent>\n\n\u2022 <indent=1.1em>Connect each topic to code, such as implementing gcd, BFS, or a set operation in a language you know.</indent>"),
                Page("Closing",
                     "That is the math behind computing: logic to reason about conditions, sets and relations to organize data, number theory for hashing and security, proofs to show that code is correct, counting to measure possibilities, and graphs to model networks. Every algorithm you meet from here builds on these eight lessons.")
            };
        }
    }
}
