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
    public class SecurityDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;

        public SecurityDashboardController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Load attempts + quizzes once
            var attempts = await (from a in _db.Attempts
                                  join q in _db.Quizzes on a.QuizId equals q.Id
                                  select new
                                  {
                                      Attempt = a,
                                      Quiz = q
                                  }).ToListAsync();

            var vm = new SecurityDashboardVm();

            if (!attempts.Any())
            {
                return View(vm);
            }

            // Global stats
            vm.TotalAttempts = attempts.Count;
            vm.TotalMcqAttempts = attempts.Count(x => x.Quiz.Type == QuizType.Mcq);
            vm.TotalCodingAttempts = attempts.Count(x => x.Quiz.Type == QuizType.Coding);

            // Suspicious = IP changed or device (UserAgent) changed
            bool IsSuspicious(Quiz_Application_College.Domain.Attempt a)
            {
                var ipChanged =
                    !string.IsNullOrWhiteSpace(a.StartIpAddress) &&
                    !string.IsNullOrWhiteSpace(a.SubmitIpAddress) &&
                    !string.Equals(a.StartIpAddress, a.SubmitIpAddress,
                        StringComparison.OrdinalIgnoreCase);

                var uaChanged =
                    !string.IsNullOrWhiteSpace(a.StartUserAgent) &&
                    !string.IsNullOrWhiteSpace(a.SubmitUserAgent) &&
                    !string.Equals(a.StartUserAgent, a.SubmitUserAgent,
                        StringComparison.OrdinalIgnoreCase);

                return ipChanged || uaChanged;
            }

            var suspicious = attempts.Where(x => IsSuspicious(x.Attempt)).ToList();

            vm.TotalSuspiciousAttempts = suspicious.Count;
            vm.SuspiciousMcqAttempts = suspicious.Count(x => x.Quiz.Type == QuizType.Mcq);
            vm.SuspiciousCodingAttempts = suspicious.Count(x => x.Quiz.Type == QuizType.Coding);

            // Distinct users (UserId is "SP:<Guid>")
            vm.TotalStudentsAttempted = attempts.Select(x => x.Attempt.UserId).Distinct().Count();
            vm.TotalStudentsWithSuspicious = suspicious.Select(x => x.Attempt.UserId).Distinct().Count();

            // Per-quiz summary
            vm.QuizSummaries = attempts
                .GroupBy(x => x.Quiz.Id)
                .Select(g =>
                {
                    var quiz = g.First().Quiz;
                    var total = g.Count();
                    var susp = g.Count(x => IsSuspicious(x.Attempt));

                    return new QuizSecuritySummaryVm
                    {
                        QuizId = quiz.Id,
                        Title = quiz.Title,
                        Type = quiz.Type == QuizType.Mcq ? "MCQ" :
                               quiz.Type == QuizType.Coding ? "Coding" :
                               quiz.Type.ToString(),
                        TotalAttempts = total,
                        SuspiciousAttempts = susp
                    };
                })
                .OrderByDescending(q => q.SuspiciousAttempts)
                .ThenBy(q => q.Title)
                .ToList();

            return View(vm);
        }

        // ----------------- View models -----------------
        public class SecurityDashboardVm
        {
            // Global totals
            public int TotalAttempts { get; set; }
            public int TotalMcqAttempts { get; set; }
            public int TotalCodingAttempts { get; set; }

            public int TotalSuspiciousAttempts { get; set; }
            public int SuspiciousMcqAttempts { get; set; }
            public int SuspiciousCodingAttempts { get; set; }

            public int TotalStudentsAttempted { get; set; }
            public int TotalStudentsWithSuspicious { get; set; }

            public List<QuizSecuritySummaryVm> QuizSummaries { get; set; } = new();
        }

        public class QuizSecuritySummaryVm
        {
            public Guid QuizId { get; set; }
            public string Title { get; set; }
            public string Type { get; set; } // MCQ / Coding
            public int TotalAttempts { get; set; }
            public int SuspiciousAttempts { get; set; }

            public double SuspiciousPercent =>
                TotalAttempts == 0 ? 0.0 :
                Math.Round((double)SuspiciousAttempts * 100.0 / TotalAttempts, 1);
        }
    }
}
