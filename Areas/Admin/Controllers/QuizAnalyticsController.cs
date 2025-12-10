using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Trainer")]
    public class QuizAnalyticsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public QuizAnalyticsController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Analytics for a single MCQ quiz.
        /// URL: /Admin/QuizAnalytics/Quiz?quizId=GUID
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Quiz(Guid quizId)
        {
            if (quizId == Guid.Empty)
                return BadRequest("quizId is required.");

            // Load quiz (only MCQ for now)
            var quiz = await _db.Quizzes
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == quizId && q.Type == QuizType.Mcq);

            if (quiz == null)
                return NotFound("MCQ quiz not found.");

            // All submitted attempts for this quiz
            var attempts = await _db.Attempts
                .Where(a => a.QuizId == quizId && a.SubmittedAt != null)
                .AsNoTracking()
                .ToListAsync();

            var vm = new QuizAnalyticsVm
            {
                QuizId = quiz.Id,
                QuizTitle = quiz.Title,
                DurationMinutes = quiz.DurationMinutes,
                TotalAttempts = attempts.Count,
                DistinctStudents = attempts.Select(a => a.UserId).Distinct().Count()
            };

            if (attempts.Count > 0)
            {
                var scores = attempts
                    .Select(a => a.Score is decimal s ? s : 0m)
                    .OrderBy(s => s)
                    .ToList();

                vm.AverageScore = Math.Round(scores.Average(), 2);
                vm.MaxScore = scores.Max();
                vm.MinScore = scores.Min();
                vm.MedianScore = scores[scores.Count / 2];
            }

            // Question list in quiz order
            var questionIds = await _db.QuizQuestions
                .Where(qq => qq.QuizId == quizId)
                .OrderBy(qq => qq.Order)
                .Select(qq => qq.QuestionId)
                .ToListAsync();

            var questions = await _db.McqQuestions
                .Where(q => questionIds.Contains(q.Id))
                .Select(q => new
                {
                    q.Id,
                    q.Text,
                    q.Marks
                })
                .ToListAsync();

            if (!questions.Any())
            {
                return View(vm); // VM with no QuestionRows
            }

            // All AttemptItems for this quiz (joined with Attempts to filter by quiz)
            var items = await (from ai in _db.AttemptItems
                               join a in _db.Attempts on ai.AttemptId equals a.Id
                               where a.QuizId == quizId && a.SubmittedAt != null
                               select new
                               {
                                   ai.QuestionId,
                                   ai.MarksAwarded
                               }).ToListAsync();

            // Build question-wise stats
            var marksMap = questions.ToDictionary(
                q => q.Id,
                q => q.Marks is decimal m ? m : 0m);

            foreach (var qid in questionIds)
            {
                var q = questions.First(x => x.Id == qid);

                var perQuestionItems = items
                    .Where(i => i.QuestionId == qid)
                    .ToList();

                var total = perQuestionItems.Count;

                int correct = 0;
                if (total > 0)
                {
                    var fullMarks = marksMap[qid];

                    // We treat "correct" as: earned full marks for this question.
                    correct = perQuestionItems.Count(i => i.MarksAwarded == fullMarks);
                }

                var difficulty = total == 0
                    ? 0.0
                    : Math.Round((double)correct * 100.0 / total, 1);

                vm.QuestionRows.Add(new QuizAnalyticsVm.QuestionRow
                {
                    QuestionText = q.Text,
                    MaxMarks = marksMap[qid],
                    TotalEvaluated = total,
                    CorrectCount = correct,
                    OtherCount = total - correct,
                    DifficultyPercent = difficulty
                });
            }

            return View(vm);
        }

        // ------------------------ VM ------------------------

        public class QuizAnalyticsVm
        {
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public int DurationMinutes { get; set; }

            // Quiz-level stats
            public int TotalAttempts { get; set; }
            public int DistinctStudents { get; set; }
            public decimal AverageScore { get; set; }
            public decimal MedianScore { get; set; }
            public decimal MaxScore { get; set; }
            public decimal MinScore { get; set; }

            public List<QuestionRow> QuestionRows { get; set; } = new();

            public class QuestionRow
            {
                public string QuestionText { get; set; } = "";
                public decimal MaxMarks { get; set; }
                public int TotalEvaluated { get; set; }
                public int CorrectCount { get; set; }
                public int OtherCount { get; set; }

                public double DifficultyPercent { get; set; }
            }
        }
    }
}
