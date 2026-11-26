using System;
using System.Collections.Generic;
using System.IO;
using BlackCompuQuiz;
using UnityEngine;

namespace ARCHIV3D
{
    /// <summary>ERD: PLAYER. The student using this device.</summary>
    [Serializable]
    public class PlayerRecord
    {
        public string playerId;
        public string playerName = LibraryProgress.DefaultPlayerName;   // the name typed on the Character Name Screen
        public bool nameChosen;                                          // false until the player has typed a name
        public string createdAt;
    }

    /// <summary>ERD: PROGRESS. How far the player got in one learning material.</summary>
    [Serializable]
    public class ProgressRecord
    {
        public string materialId;
        public string materialTitle;
        public string subjectId;
        public string subjectName;
        public int yearLevel;
        public int pagesRead;        // furthest page reached
        public int totalPages;       // pages the book had when last read
        public bool completed;       // reached "The End"
        public int timesOpened;
        public string firstOpened;
        public string lastOpened;
        public string completedAt;

        public float Percent => completed ? 1f : totalPages > 0 ? Mathf.Clamp01((float)pagesRead / totalPages) : 0f;
    }

    /// <summary>ERD: LEARNING ACTIVITY. One thing the player did (opened a book, finished it, took a quiz).</summary>
    [Serializable]
    public class ActivityRecord
    {
        public string time;
        public string type;          // OpenedMaterial, FinishedMaterial, StartedQuiz, FinishedQuiz
        public string subjectId;
        public string subjectName;
        public string itemId;        // material or quiz ID
        public string itemTitle;
        public string detail;
    }

    /// <summary>ERD: ANSWER. One answer the player submitted to a quiz question.</summary>
    [Serializable]
    public class AnswerRecord
    {
        public string time;
        public string quizId;
        public string quizTitle;
        public string subjectId;
        public string question;
        public string answer;
        public bool correct;
    }

    /// <summary>A finished quiz attempt (score), linked to the quiz and its subject.</summary>
    [Serializable]
    public class QuizResultRecord
    {
        public string time;
        public string quizId;
        public string quizTitle;
        public string subjectId;
        public string subjectName;
        public int score;
        public int total;
        public int percent;
        public int attempt;          // 1 = first time this quiz was taken on this device
        public int seconds;          // how long the attempt took (0 if unknown)
    }

    /// <summary>A book page the player bookmarked to come back to (the ribbon at the top corner of a page).</summary>
    [Serializable]
    public class BookmarkRecord
    {
        public string time;
        public string materialId;
        public string materialTitle;
        public string subjectId;
        public string subjectName;
        public int yearLevel;
        public int pageIndex;        // which page of the book in the Inspector (Pages list), 0 = first
        public int part;             // which printed page of that entry when its text runs over several, 0 = first
        public int pageNumber;       // page number printed at the bottom when it was bookmarked
        public string heading;       // that page's heading (its topic)
        public string excerpt;       // the first words on the page
    }

    /// <summary>Score summary of one quiz on this device: tries, best, last and average score.</summary>
    public class QuizStats
    {
        public string quizId;
        public int attempts;
        public int best = -1;        // best percent, -1 = never taken
        public int bestScore;        // e.g. 4 (of bestTotal 5)
        public int bestTotal;
        public int last = -1;        // percent of the latest attempt
        public int previous = -1;    // percent of the attempt before the latest
        public float average;        // average percent of all attempts
        public string lastTime = "";

        public bool Taken => attempts > 0;
    }

    /// <summary>
    /// Saves the student's progress, learning activity and quiz answers on the device
    /// (Application.persistentDataPath/archiv3d_progress.json), following the ARCHIV3D ERD.
    /// The bookshelves and BlackCompu computers report to it automatically.
    /// </summary>
    public static class LibraryProgress
    {
        public const string FileName = "archiv3d_progress.json";
        /// <summary>Used until the player names their character.</summary>
        public const string DefaultPlayerName = "Student";
        public const int MaxActivities = 1000;
        public const int MaxAnswers = 2000;

        [Serializable]
        class SaveData
        {
            public int version = 1;
            public PlayerRecord player = new PlayerRecord();
            public List<ProgressRecord> progress = new List<ProgressRecord>();
            public List<ActivityRecord> activities = new List<ActivityRecord>();
            public List<AnswerRecord> answers = new List<AnswerRecord>();
            public List<QuizResultRecord> quizResults = new List<QuizResultRecord>();
            public List<BookmarkRecord> bookmarks = new List<BookmarkRecord>();
        }

        static SaveData s_Data;

        // When each quiz was started (realtime seconds), to work out how long an attempt took.
        static readonly Dictionary<string, float> s_QuizStartTimes = new Dictionary<string, float>();

        /// <summary>Raised after anything is recorded (to refresh progress shown on screen).</summary>
        public static event Action Changed;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        static SaveData Data
        {
            get
            {
                if (s_Data == null) Load();
                return s_Data;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            s_Data = null;
            s_QuizStartTimes.Clear();
            QuizScreen.QuizStarted -= OnQuizStarted;
            QuizScreen.QuizStarted += OnQuizStarted;
            QuizScreen.QuestionAnswered -= OnQuestionAnswered;
            QuizScreen.QuestionAnswered += OnQuestionAnswered;
            QuizScreen.QuizFinished -= OnQuizFinished;
            QuizScreen.QuizFinished += OnQuizFinished;
        }

        // ------------------------------------------------------------------ load / save

        public static void Load()
        {
            s_Data = null;
            try
            {
                if (File.Exists(FilePath))
                    s_Data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ARCHIV3D] Couldn't read saved progress, starting fresh: " + e.Message);
            }
            if (s_Data == null) s_Data = new SaveData();
            if (s_Data.player == null) s_Data.player = new PlayerRecord();
            if (string.IsNullOrEmpty(s_Data.player.playerId))
            {
                s_Data.player.playerId = Guid.NewGuid().ToString("N");
                s_Data.player.createdAt = Now;
            }
            if (s_Data.progress == null) s_Data.progress = new List<ProgressRecord>();
            if (s_Data.activities == null) s_Data.activities = new List<ActivityRecord>();
            if (s_Data.answers == null) s_Data.answers = new List<AnswerRecord>();
            if (s_Data.quizResults == null) s_Data.quizResults = new List<QuizResultRecord>();
            if (s_Data.bookmarks == null) s_Data.bookmarks = new List<BookmarkRecord>();
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ARCHIV3D] Couldn't save progress: " + e.Message);
            }
            Changed?.Invoke();
        }

        /// <summary>Deletes all saved progress, activity, answers, scores and bookmarks on this device (keeps the player).</summary>
        public static void ResetAll()
        {
            var player = Data.player;
            s_Data = new SaveData { player = player };
            Save();
        }

        static string Now => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // ------------------------------------------------------------------ player

        public static PlayerRecord Player => Data.player;

        /// <summary>The character's name (set on the Character Name Screen when the game starts). Shown on My Scores.</summary>
        public static string PlayerName
        {
            get => string.IsNullOrWhiteSpace(Data.player.playerName) ? DefaultPlayerName : Data.player.playerName;
            set
            {
                bool chosen = !string.IsNullOrWhiteSpace(value);
                Data.player.playerName = chosen ? value.Trim() : DefaultPlayerName;
                Data.player.nameChosen = chosen;
                Save();
            }
        }

        /// <summary>True once the player has typed a name for their character on this device.</summary>
        public static bool HasChosenName => Data.player.nameChosen;

        // ------------------------------------------------------------------ reading

        public static IReadOnlyList<ProgressRecord> AllProgress => Data.progress;
        public static IReadOnlyList<ActivityRecord> Activities => Data.activities;
        public static IReadOnlyList<AnswerRecord> Answers => Data.answers;
        public static IReadOnlyList<QuizResultRecord> QuizResults => Data.quizResults;

        public static ProgressRecord GetProgress(string materialId)
        {
            if (string.IsNullOrEmpty(materialId)) return null;
            foreach (var p in Data.progress)
                if (p.materialId == materialId) return p;
            return null;
        }

        /// <summary>How many of a subject's books the player has finished / started.</summary>
        public static void SubjectSummary(SubjectData subject, out int finished, out int started, out int total)
        {
            finished = started = total = 0;
            if (subject == null || subject.materials == null) return;
            foreach (var m in subject.materials)
            {
                if (m == null) continue;
                total++;
                var p = GetProgress(m.materialId);
                if (p == null) continue;
                started++;
                if (p.completed) finished++;
            }
        }

        /// <summary>Tries, best, last and average score of one quiz (by its QuizId) on this device.</summary>
        public static QuizStats GetQuizStats(string quizId)
        {
            var stats = new QuizStats { quizId = quizId };
            if (string.IsNullOrEmpty(quizId)) return stats;
            int sum = 0;
            foreach (var r in Data.quizResults)
            {
                if (r == null || r.quizId != quizId) continue;
                stats.attempts++;
                sum += r.percent;
                stats.previous = stats.last;
                stats.last = r.percent;
                stats.lastTime = r.time;
                if (r.percent > stats.best)
                {
                    stats.best = r.percent;
                    stats.bestScore = r.score;
                    stats.bestTotal = r.total;
                }
            }
            if (stats.attempts > 0) stats.average = (float)sum / stats.attempts;
            return stats;
        }

        /// <summary>The latest finished quiz attempts, newest first.</summary>
        public static List<QuizResultRecord> RecentQuizResults(int count)
        {
            var list = new List<QuizResultRecord>();
            for (int i = Data.quizResults.Count - 1; i >= 0 && list.Count < count; i--)
                if (Data.quizResults[i] != null) list.Add(Data.quizResults[i]);
            return list;
        }

        // ------------------------------------------------------------------ recording: books

        public static void MaterialOpened(SubjectData subject, LearningMaterial material)
        {
            if (material == null) return;
            var p = GetOrAdd(subject, material);
            p.timesOpened++;
            p.lastOpened = Now;
            if (string.IsNullOrEmpty(p.firstOpened)) p.firstOpened = p.lastOpened;
            Log("OpenedMaterial", subject, material.materialId, material.DisplayTitle, "");
            Save();
        }

        /// <summary>Called by the book screen whenever a new spread is shown.</summary>
        public static void PageReached(SubjectData subject, LearningMaterial material, int page, int totalPages, bool reachedEnd)
        {
            if (material == null) return;
            var p = GetOrAdd(subject, material);
            bool changed = false;
            if (totalPages != p.totalPages) { p.totalPages = totalPages; changed = true; }
            int reached = Mathf.Clamp(page, 0, Mathf.Max(0, totalPages));
            if (reached > p.pagesRead) { p.pagesRead = reached; changed = true; }
            if (reachedEnd && totalPages > 0 && !p.completed)
            {
                p.completed = true;
                p.pagesRead = totalPages;
                p.completedAt = Now;
                Log("FinishedMaterial", subject, material.materialId, material.DisplayTitle, totalPages + " pages");
                changed = true;
            }
            if (changed) Save();
        }

        static ProgressRecord GetOrAdd(SubjectData subject, LearningMaterial material)
        {
            var p = GetProgress(material.materialId);
            if (p == null)
            {
                p = new ProgressRecord { materialId = material.materialId };
                Data.progress.Add(p);
            }
            p.materialTitle = material.DisplayTitle;
            p.subjectId = subject != null ? subject.subjectId : "";
            p.subjectName = subject != null ? subject.DisplayName : "";
            p.yearLevel = subject != null ? subject.yearLevel : 0;
            return p;
        }

        static void Log(string type, SubjectData subject, string itemId, string itemTitle, string detail)
        {
            Data.activities.Add(new ActivityRecord
            {
                time = Now,
                type = type,
                subjectId = subject != null ? subject.subjectId : "",
                subjectName = subject != null ? subject.DisplayName : "",
                itemId = itemId,
                itemTitle = itemTitle,
                detail = detail,
            });
            if (Data.activities.Count > MaxActivities) Data.activities.RemoveRange(0, Data.activities.Count - MaxActivities);
        }

        // ------------------------------------------------------------------ bookmarks

        /// <summary>Every bookmarked page on this device, oldest first.</summary>
        public static IReadOnlyList<BookmarkRecord> Bookmarks => Data.bookmarks;

        /// <summary>The bookmark on one printed page of a book, or null.</summary>
        public static BookmarkRecord FindBookmark(string materialId, int pageIndex, int part)
        {
            if (string.IsNullOrEmpty(materialId)) return null;
            foreach (var b in Data.bookmarks)
                if (b != null && b.materialId == materialId && b.pageIndex == pageIndex && b.part == part) return b;
            return null;
        }

        public static bool IsBookmarked(string materialId, int pageIndex, int part) =>
            FindBookmark(materialId, pageIndex, part) != null;

        /// <summary>Bookmarks a printed page (or refreshes its details if it already is).</summary>
        public static BookmarkRecord AddBookmark(SubjectData subject, LearningMaterial material, int pageIndex, int part,
            int pageNumber, string heading, string excerpt)
        {
            if (material == null || string.IsNullOrEmpty(material.materialId)) return null;
            var b = FindBookmark(material.materialId, pageIndex, part);
            if (b == null)
            {
                b = new BookmarkRecord { materialId = material.materialId, pageIndex = pageIndex, part = part };
                Data.bookmarks.Add(b);
            }
            b.time = Now;
            b.materialTitle = material.DisplayTitle;
            b.subjectId = subject != null ? subject.subjectId : "";
            b.subjectName = subject != null ? subject.DisplayName : "";
            b.yearLevel = subject != null ? subject.yearLevel : 0;
            b.pageNumber = pageNumber;
            b.heading = heading ?? "";
            b.excerpt = excerpt ?? "";
            Log("AddedBookmark", subject, material.materialId, material.DisplayTitle, "page " + pageNumber);
            Save();
            return b;
        }

        public static void RemoveBookmark(BookmarkRecord bookmark)
        {
            if (bookmark == null || !Data.bookmarks.Remove(bookmark)) return;
            Data.activities.Add(new ActivityRecord
            {
                time = Now,
                type = "RemovedBookmark",
                subjectId = bookmark.subjectId,
                subjectName = bookmark.subjectName,
                itemId = bookmark.materialId,
                itemTitle = bookmark.materialTitle,
                detail = "page " + bookmark.pageNumber,
            });
            if (Data.activities.Count > MaxActivities) Data.activities.RemoveRange(0, Data.activities.Count - MaxActivities);
            Save();
        }

        // ------------------------------------------------------------------ recording: quizzes

        static void OnQuizStarted(IQuiz quiz)
        {
            if (quiz == null) return;
            s_QuizStartTimes[quiz.QuizId] = Time.realtimeSinceStartup;
            Log("StartedQuiz", quiz.Subject, quiz.QuizId, quiz.DisplayTitle, "");
            Save();
        }

        static void OnQuestionAnswered(IQuiz quiz, QuizQuestion question, string answer, bool correct)
        {
            if (quiz == null || question == null) return;
            Data.answers.Add(new AnswerRecord
            {
                time = Now,
                quizId = quiz.QuizId,
                quizTitle = quiz.DisplayTitle,
                subjectId = quiz.Subject != null ? quiz.Subject.subjectId : "",
                question = question.question,
                answer = answer,
                correct = correct,
            });
            if (Data.answers.Count > MaxAnswers) Data.answers.RemoveRange(0, Data.answers.Count - MaxAnswers);
            Save();
        }

        static void OnQuizFinished(IQuiz quiz, int score, int total)
        {
            if (quiz == null) return;
            int percent = total > 0 ? Mathf.RoundToInt(100f * score / total) : 0;
            int seconds = 0;
            if (s_QuizStartTimes.TryGetValue(quiz.QuizId, out float started))
            {
                seconds = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - started));
                s_QuizStartTimes.Remove(quiz.QuizId);
            }
            Data.quizResults.Add(new QuizResultRecord
            {
                time = Now,
                quizId = quiz.QuizId,
                quizTitle = quiz.DisplayTitle,
                subjectId = quiz.Subject != null ? quiz.Subject.subjectId : "",
                subjectName = quiz.Subject != null ? quiz.Subject.DisplayName : "",
                score = score,
                total = total,
                percent = percent,
                attempt = GetQuizStats(quiz.QuizId).attempts + 1,
                seconds = seconds,
            });
            Log("FinishedQuiz", quiz.Subject, quiz.QuizId, quiz.DisplayTitle, score + "/" + total + " (" + percent + "%)");
            Save();
        }
    }
}
