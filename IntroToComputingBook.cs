using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Books > Update Introduction to Computing Book
    ///
    /// Writes the "Lesson Collection" book into Introduction to Computing (1st Year, 1st Semester): a Welcome page, Lessons 1-2 under Prelims, 3-4 Midterms, 5-6 Prefinals, 7-8 Finals, then Study Tips and a Closing page (same format as Computer Programming 1's Course Notes).
    /// It runs inside Unity, so the open Editor and the saved file always match. The book keeps its ID, so
    /// students' progress and bookmarks still work. Safe to run again: it only replaces this book's pages;
    /// other books and quizzes are kept.
    /// </summary>
    static class IntroToComputingBook
    {
        const string SubjectPath = "Assets/ARCHIV3D/Subjects/1st Year/Introduction to Computing.asset";
        const string SubjectName = "Introduction to Computing";
        const string BookTitle = "Lesson Collection";
        const string BookId = "0a8876d3a06e4c79b65d1fed7f73f810";

        [MenuItem("Tools/ARCHIV3D/Books/Update Introduction to Computing Book", false, 50)]
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

            Undo.RecordObject(subject, "Update Introduction to Computing Book");
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
            book.description = "For 1st-year BSIT students. Eight short lessons, each with key ideas and a quick check.";
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
                     "<i>Welcome Reader! This is the 1st year shelf's book.\nThis semester was about understanding the computer itself before learning to program it. The lessons ahead follow the same order as the class: what computing is, how computers developed, the hardware inside them, the software that runs on them, how they store everything as numbers, how they connect through networks, how to use them safely and responsibly, and how to solve problems step by step.</i>"),
                Page("Prelims",
                     "<b><i>LESSON 1: WHAT IS COMPUTING?</i></b>\n<i>Objectives: Define computing, and distinguish data, information, and knowledge.</i>\n\n\u2022 <indent=1.1em><b>Computer:</b> an electronic device that accepts input, processes it, stores it, and produces output (the IPOS cycle: Input, Process, Output, Storage).</indent>\n\n\u2022 <indent=1.1em><b>Data</b> = raw facts (e.g., 87, 92, 75). <b>Information</b> = processed, meaningful data (e.g., class average is 84.7). <b>Knowledge</b> = understanding gained from information.</indent>\n\n\u2022 <indent=1.1em><b>Computing fields:</b> Information Technology (IT), Computer Science (CS), Information Systems (IS), Software Engineering (SE), Computer Engineering (CE). BSIT focuses on applying technology to solve organizational problems.</indent>\n\n<b>Quick check:</b> <i>Is a list of student grades data or information? What turns it into the other?</i>"),
                Page("Prelims",
                     "<b><i>LESSON 2: HISTORY AND GENERATIONS OF COMPUTERS</i></b>\n<i>Objectives: Trace major milestones in computing.</i>\n\n\u2022 <indent=1.1em><b>Early tools:</b> abacus, Pascal's calculator, Babbage's Analytical Engine (concept of a programmable machine), Ada Lovelace (considered the first programmer).</indent>\n\n\u2022 <indent=1.1em><b>Five generations:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>1. Vacuum tubes (1940s-50s)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>2. Transistors (1950s-60s)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>3. Integrated circuits (1960s-70s)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>4. Microprocessors (1970s-present)</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>5. Artificial intelligence and quantum/parallel computing (present and emerging)</indent>\n\n\u2022 <indent=1.1em><b>Trend:</b> each generation became smaller, faster, cheaper, and more reliable.</indent>\n\n<b>Quick check:</b> <i>Which hardware technology defines the second generation?</i>"),
                Page("Midterms",
                     "<b><i>LESSON 3: COMPUTER HARDWARE</i></b>\n<i>Objectives: Identify and explain the function of core hardware components.</i>\n\n\u2022 <indent=1.1em><b>CPU:</b> the \"brain\"; contains the ALU (arithmetic and logic), the Control Unit, and registers. Speed is measured in GHz; more cores allow more parallel work.</indent>\n\n\u2022 <indent=1.1em><b>Memory:</b> RAM is volatile and temporary working space; ROM is non-volatile and holds startup instructions; cache is very fast memory close to the CPU.</indent>\n\n\u2022 <indent=1.1em><b>Storage:</b> HDD (magnetic, cheaper), SSD (flash, faster), cloud storage (remote).</indent>\n\n\u2022 <indent=1.1em><b>Input devices:</b> keyboard, mouse, scanner, microphone. <b>Output devices:</b> monitor, printer, speakers.</indent>\n\n\u2022 <indent=1.1em><b>Motherboard:</b> connects all components. <b>GPU:</b> specialized for graphics and parallel computation.</indent>\n\n<b>Quick check:</b> <i>Why does a computer lose unsaved work when power is cut? (Hint: volatility.)</i>"),
                Page("Midterms",
                     "<b><i>LESSON 4: COMPUTER SOFTWARE</i></b>\n<i>Objectives: Classify software and explain the role of an operating system.</i>\n\n\u2022 <indent=1.1em><b>System software:</b> operating systems (Windows, macOS, Linux, Android), device drivers, utilities.</indent>\n\n\u2022 <indent=1.1em><b>Application software:</b> word processors, browsers, games, enterprise systems.</indent>\n\n\u2022 <indent=1.1em><b>Programming software:</b> compilers, interpreters, IDEs, debuggers.</indent>\n\n\u2022 <indent=1.1em><b>Operating system functions:</b> process management, memory management, file management, device management, user interface, and security.</indent>\n\n\u2022 <indent=1.1em><b>Licensing:</b> proprietary, open source, freeware, shareware. Respect licenses; piracy is illegal and risky.</indent>\n\n<b>Quick check:</b> <i>Is a web browser system software or application software? Why?</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 5: NUMBER SYSTEMS AND DATA REPRESENTATION</i></b>\n<i>Objectives: Convert between number systems and explain how computers store data.</i>\n\n\u2022 <indent=1.1em>Computers use <b>binary</b> (base 2) because circuits have two states: on (1) and off (0).</indent>\n\n\u2022 <indent=1.1em><b>Common systems:</b> binary (base 2), octal (base 8), decimal (base 10), hexadecimal (base 16).</indent>\n\n\u2022 <indent=1.1em><b>Binary to decimal:</b> multiply each bit by its power of 2. Example: 1011<sub>2</sub> = 8 + 0 + 2 + 1 = 11<sub>10</sub>.</indent>\n\n\u2022 <indent=1.1em><b>Decimal to binary:</b> divide repeatedly by 2 and read remainders from bottom to top. Example: 13 = 1101<sub>2</sub>.</indent>\n\n\u2022 <indent=1.1em><b>Units:</b> 1 bit; 8 bits = 1 byte; 1 KB = 1,024 bytes; then MB, GB, TB.</indent>\n\n\u2022 <indent=1.1em><b>Text:</b> characters are encoded as numbers (ASCII, Unicode). Images use pixels; audio uses samples.</indent>\n\n<b>Quick check:</b> <i>Convert 25<sub>10</sub> to binary and 101<sub>2</sub> to decimal.</i>"),
                Page("Prefinals",
                     "<b><i>LESSON 6: NETWORKS AND THE INTERNET</i></b>\n<i>Objectives: Describe network types and how the Internet works.</i>\n\n\u2022 <indent=1.1em><b>Network types:</b> PAN, LAN, MAN, WAN. The Internet is a global network of networks.</indent>\n\n\u2022 <indent=1.1em><b>Key devices:</b> router (connects networks), switch (connects devices within a LAN), modem, access point.</indent>\n\n\u2022 <indent=1.1em><b>IP address:</b> identifies a device on a network (IPv4 example: 192.168.1.1). <b>DNS</b> translates domain names (like example.com) into IP addresses.</indent>\n\n\u2022 <indent=1.1em><b>Common protocols:</b> HTTP/HTTPS (web), FTP (file transfer), SMTP/IMAP (email), TCP/IP (the Internet's foundation).</indent>\n\n\u2022 <indent=1.1em><b>Client-server model:</b> clients request services; servers provide them.</indent>\n\n\u2022 <indent=1.1em><b>Cloud computing:</b> on-demand services over the Internet (IaaS, PaaS, SaaS).</indent>\n\n<b>Quick check:</b> <i>What problem does DNS solve?</i>"),
                Page("Finals",
                     "<b><i>LESSON 7: CYBERSECURITY, ETHICS, AND DIGITAL CITIZENSHIP</i></b>\n<i>Objectives: Recognize common threats and practice responsible computing.</i>\n\n\u2022 <indent=1.1em><b>Threats:</b> malware (viruses, worms, ransomware, spyware), phishing, social engineering, password attacks, data breaches.</indent>\n\n\u2022 <indent=1.1em><b>Protection practices:</b></indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Use strong, unique passwords and a password manager.</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Enable multi-factor authentication (MFA).</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Keep software updated and back up data (3-2-1 rule: 3 copies, 2 media types, 1 offsite).</indent>\n<indent=1.1em>\u2013 </indent><indent=2.2em>Verify links and attachments before opening.</indent>\n\n\u2022 <indent=1.1em><b>Ethics and law:</b> respect intellectual property and give proper credit; avoid plagiarism. In the Philippines, relevant laws include the Data Privacy Act of 2012 (RA 10173) and the Cybercrime Prevention Act of 2012 (RA 10175).</indent>\n\n\u2022 <indent=1.1em><b>Professional conduct:</b> honesty, confidentiality, and accountability for your work.</indent>\n\n<b>Quick check:</b> <i>List three signs of a phishing email.</i>"),
                Page("Finals",
                     "<b><i>LESSON 8: PROBLEM SOLVING AND INTRODUCTION TO PROGRAMMING</i></b>\n<i>Objectives: Apply computational thinking and read simple algorithms.</i>\n\n\u2022 <indent=1.1em><b>Computational thinking:</b> decomposition (break the problem down), pattern recognition, abstraction (focus on what matters), and algorithm design.</indent>\n\n\u2022 <indent=1.1em><b>Algorithm:</b> a finite, ordered set of clear steps to solve a problem. Represent it with pseudocode or a flowchart (oval = start/end, rectangle = process, diamond = decision, parallelogram = input/output).</indent>\n\n\u2022 <indent=1.1em><b>Core constructs:</b> sequence, selection (if/else), and iteration (loops).</indent>\n\n\u2022 <indent=1.1em><b>Example (pseudocode):</b></indent>\n<indent=1.1em><color=#8B4513>INPUT score\nIF score >= 75 THEN\n    PRINT \"Passed\"\nELSE\n    PRINT \"Failed\"\nEND IF</color></indent>\n\n\u2022 <indent=1.1em><b>Programming languages:</b> high-level (Python, Java, C#) are translated to machine code by a compiler or interpreter.</indent>\n\n<b>Quick check:</b> <i>Write pseudocode that prints the numbers 1 to 5.</i>"),
                Page("Study Tips",
                     "<b><i>SUGGESTED STUDY TIPS</i></b>\n\n\u2022 <indent=1.1em>Practice number conversions daily; fluency comes from repetition.</indent>\n\n\u2022 <indent=1.1em>Build and open up a computer (or use an online simulator) to see hardware in action.</indent>\n\n\u2022 <indent=1.1em>Try a beginner coding exercise after each week of lessons.</indent>\n\n\u2022 <indent=1.1em>Form small study groups and explain concepts to each other.</indent>"),
                Page("Closing",
                     "That is the foundation of computing: what a computer is and where it came from, the hardware and software that make it work, how it turns everything into numbers, how it connects to the world, how to use it safely and responsibly, and how to solve problems step by step. Every IT subject ahead builds on these eight lessons.")
            };
        }
    }
}
