using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student")] // same base route as DashboardController
    public class HistoryController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HistoryController(ApplicationDbContext db)
        {
            _db = db;
        }

        private Guid Spid()
        {
            return Guid.TryParse(User.FindFirst("spid")?.Value, out var id) ? id : Guid.Empty;
        }

        private string StudentAttemptKey(Guid spid) => $"SP:{spid:D}";

        // GET: /Student/History
        [HttpGet("History")]
        public async Task<IActionResult> Index()
        {
            var spid = Spid();
            if (spid == Guid.Empty)
                return RedirectToAction("Login", "Auth", new { area = "Student" });

            var key = StudentAttemptKey(spid);

            var items = await _db.Attempts
                .Where(a => a.UserId == key && a.SubmittedAt != null)
                .Join(
                    _db.Quizzes,
                    a => a.QuizId,
                    q => q.Id,
                    (a, q) => new { Attempt = a, Quiz = q }
                )
                .OrderByDescending(x => x.Attempt.SubmittedAt)
                .Select(x => new AttemptListItemVm
                {
                    AttemptId = x.Attempt.Id,
                    QuizTitle = x.Quiz.Title,
                    QuizType = x.Quiz.Type.ToString(),
                    StartedAt = x.Attempt.StartedAt,
                    SubmittedAt = x.Attempt.SubmittedAt,
                    Score = x.Attempt.Score,
                    DurationMinutes = x.Quiz.DurationMinutes
                })
                .ToListAsync();

            var model = new HistoryIndexVm
            {
                Items = items
            };

            return View("~/Areas/Student/Views/History/Index.cshtml", model);
        }

        // ===== ViewModels =====

        public class HistoryIndexVm
        {
            public List<AttemptListItemVm> Items { get; set; } = new();
        }

        public class AttemptListItemVm
        {
            public Guid AttemptId { get; set; }

            [Display(Name = "Quiz")]
            public string QuizTitle { get; set; } = string.Empty;

            [Display(Name = "Type")]
            public string QuizType { get; set; } = string.Empty;

            [Display(Name = "Started")]
            public DateTimeOffset StartedAt { get; set; }


            [Display(Name = "Submitted")]
            public DateTimeOffset? SubmittedAt { get; set; }

            [Display(Name = "Score")]
            public decimal Score { get; set; }

            [Display(Name = "Duration (min)")]
            public int DurationMinutes { get; set; }
        }
    }
}
