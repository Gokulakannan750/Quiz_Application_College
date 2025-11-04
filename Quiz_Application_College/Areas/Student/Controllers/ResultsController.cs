using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class ResultsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ResultsController(ApplicationDbContext db) => _db = db;

        // GET: /Student/Results
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

            var list = await (from a in _db.Attempts
                              join q in _db.Quizzes on a.QuizId equals q.Id
                              where a.UserId == userId
                              orderby a.StartedAt descending
                              select new ResultRow
                              {
                                  AttemptId = a.Id,
                                  QuizTitle = q.Title,
                                  StartedAt = a.StartedAt,
                                  SubmittedAt = a.SubmittedAt,
                                  Score = a.Score
                              }).ToListAsync();

            return View(list);
        }

        public class ResultRow
        {
            public Guid AttemptId { get; set; }
            public string QuizTitle { get; set; } = "";
            public DateTimeOffset StartedAt { get; set; }
            public DateTimeOffset? SubmittedAt { get; set; }
            public decimal Score { get; set; }
        }
    }
}
