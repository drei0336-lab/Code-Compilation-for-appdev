using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ARCHIV3D.EditorTools
{
    /// <summary>
    /// Tools > ARCHIV3D > Student Progress: look at or clear what LibraryProgress saved on this computer
    /// (player, reading progress, learning activity, quiz answers). Phones keep their own copy.
    /// </summary>
    static class StudentProgressMenu
    {
        const string Root = "Tools/ARCHIV3D/Student Progress/";
        const string ReportPath = "Logs/ARCHIV3D-StudentProgress.txt";
        const string ScoresCsvPath = "Logs/ARCHIV3D-QuizScores.csv";

        [MenuItem(Root + "Show Summary", false, 70)]
        static void ShowSummary()
        {
            LibraryProgress.Load();
            var sb = new StringBuilder();
            var player = LibraryProgress.Player;
            sb.AppendLine($"Player: {player.playerName}  (id {player.playerId}, since {player.createdAt})");
            sb.AppendLine($"Saved in: {LibraryProgress.FilePath}");
            sb.AppendLine();

            sb.AppendLine("== PROGRESS (learning materials) ==");
            if (LibraryProgress.AllProgress.Count == 0) sb.AppendLine("  (no books opened yet)");
            foreach (var p in LibraryProgress.AllProgress.OrderBy(x => x.yearLevel).ThenBy(x => x.subjectName))
            {
                string state = p.completed ? "finished" : "page " + p.pagesRead + "/" + p.totalPages;
                sb.AppendLine($"  Year {p.yearLevel}  {p.subjectName,-28} {p.materialTitle,-28} {state,-14} " +
                              $"opened {p.timesOpened}x, last {p.lastOpened}");
            }
            sb.AppendLine();

            sb.AppendLine("== QUIZ SCORES (per quiz) ==");
            if (LibraryProgress.QuizResults.Count == 0) sb.AppendLine("  (no quizzes finished yet)");
            foreach (var g in LibraryProgress.QuizResults.Where(r => r != null).GroupBy(r => r.quizId))
            {
                var stats = LibraryProgress.GetQuizStats(g.Key);
                var any = g.Last();
                string subject = string.IsNullOrEmpty(any.subjectName) ? "-" : any.subjectName;
                sb.AppendLine($"  {subject,-28} {any.quizTitle,-24} tries {stats.attempts,-3} best {stats.best,3}%  " +
                              $"last {stats.last,3}%  average {Mathf.RoundToInt(stats.average),3}%");
            }
            sb.AppendLine();

            sb.AppendLine("== QUIZ ATTEMPTS ==");
            if (LibraryProgress.QuizResults.Count == 0) sb.AppendLine("  (no quizzes finished yet)");
            foreach (var r in LibraryProgress.QuizResults)
                sb.AppendLine($"  {r.time}  {r.quizTitle,-30} {r.score}/{r.total} ({r.percent}%)  " +
                              $"attempt {r.attempt}  {(r.seconds > 0 ? r.seconds + "s" : "")}");
            sb.AppendLine();

            sb.AppendLine($"== BOOKMARKS ({LibraryProgress.Bookmarks.Count}) ==");
            if (LibraryProgress.Bookmarks.Count == 0) sb.AppendLine("  (no pages bookmarked yet)");
            foreach (var b in LibraryProgress.Bookmarks.Where(x => x != null).OrderBy(x => x.yearLevel).ThenBy(x => x.subjectName)
                         .ThenBy(x => x.materialTitle).ThenBy(x => x.pageIndex).ThenBy(x => x.part))
                sb.AppendLine($"  {b.time}  {b.subjectName,-28} {b.materialTitle,-28} page {b.pageNumber,-4} {b.heading}");
            sb.AppendLine();

            sb.AppendLine($"== ANSWERS ({LibraryProgress.Answers.Count}) - last 20 ==");
            foreach (var a in LibraryProgress.Answers.Skip(Mathf.Max(0, LibraryProgress.Answers.Count - 20)))
                sb.AppendLine("  " + a.time + "  [" + (a.correct ? "right" : "wrong") + "] " + a.quizTitle + ": " + a.question + " -> " + a.answer);
            sb.AppendLine();

            sb.AppendLine($"== LEARNING ACTIVITY ({LibraryProgress.Activities.Count}) - last 30 ==");
            foreach (var a in LibraryProgress.Activities.Skip(Mathf.Max(0, LibraryProgress.Activities.Count - 30)))
                sb.AppendLine($"  {a.time}  {a.type,-17} {a.subjectName} / {a.itemTitle} {a.detail}");

            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("[ARCHIV3D] Student progress (also in " + ReportPath + "):\n" + sb);
        }

        /// <summary>Every finished quiz attempt as a spreadsheet (opens in Excel / Google Sheets).</summary>
        [MenuItem(Root + "Export Quiz Scores (CSV)", false, 72)]
        static void ExportScores()
        {
            LibraryProgress.Load();
            var player = LibraryProgress.Player;
            var sb = new StringBuilder();
            sb.AppendLine("Date,Student,Year Level,Subject,Quiz,Score,Total,Percent,Attempt,Seconds");
            var years = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var p in LibraryProgress.AllProgress)
                if (!string.IsNullOrEmpty(p.subjectId)) years[p.subjectId] = p.yearLevel;
            foreach (string guid in AssetDatabase.FindAssets("t:SubjectData"))
            {
                var s = AssetDatabase.LoadAssetAtPath<SubjectData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && !string.IsNullOrEmpty(s.subjectId)) years[s.subjectId] = s.yearLevel;
            }
            foreach (var r in LibraryProgress.QuizResults)
            {
                if (r == null) continue;
                string year = r.subjectId != null && years.TryGetValue(r.subjectId, out int y) ? y.ToString() : "";
                sb.AppendLine(string.Join(",", Csv(r.time), Csv(player.playerName), year, Csv(r.subjectName), Csv(r.quizTitle),
                    r.score, r.total, r.percent, r.attempt, r.seconds));
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ScoresCsvPath, sb.ToString(), new UTF8Encoding(true));
            Debug.Log("[ARCHIV3D] Exported " + LibraryProgress.QuizResults.Count + " quiz attempt(s) to " + ScoresCsvPath);
            EditorUtility.RevealInFinder(ScoresCsvPath);
        }

        static string Csv(string value)
        {
            value = value ?? "";
            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        [MenuItem(Root + "Open Save Folder", false, 71)]
        static void OpenFolder()
        {
            if (File.Exists(LibraryProgress.FilePath)) EditorUtility.RevealInFinder(LibraryProgress.FilePath);
            else EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        [MenuItem(Root + "Reset Progress (this computer)", false, 90)]
        static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("ARCHIV3D", "Delete all saved reading progress, learning activity, quiz answers and quiz scores " +
                                                         "on this computer? (Phones keep their own.)", "Delete", "Cancel"))
                return;
            LibraryProgress.Load();
            LibraryProgress.ResetAll();
            Debug.Log("[ARCHIV3D] Student progress on this computer was reset.");
        }
    }
}
