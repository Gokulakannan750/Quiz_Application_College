using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        public DashboardController(ApplicationDbContext db) => _db = db;

        private Guid CurrentProfileId()
            => Guid.TryParse(User.FindFirst("spid")?.Value, out var id) ? id : Guid.Empty;

        [HttpGet("")]
        [HttpGet("Dashboard")]
        public async Task<IActionResult> Index()
        {
            var spid = CurrentProfileId();
            if (spid == Guid.Empty)
                return RedirectToAction("Login", "Auth", new { area = "Student" });

            var now = DateTimeOffset.UtcNow;
            var soon = now.AddDays(7);

            // MCQ stats
            var mcqAvailableNow = await (from e in _db.Enrollments
                                         join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                                         join q in _db.Quizzes on s.QuizId equals q.Id
                                         where e.StudentProfileId == spid
                                               && q.Type == QuizType.Mcq
                                               && s.StartAt <= now && now <= s.EndAt
                                         select s.Id).CountAsync();

            var mcqUpcoming = await (from e in _db.Enrollments
                                     join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                                     join q in _db.Quizzes on s.QuizId equals q.Id
                                     where e.StudentProfileId == spid
                                           && q.Type == QuizType.Mcq
                                           && s.StartAt > now && s.StartAt <= soon
                                     select s.Id).CountAsync();

            // Coding stats
            var codingAvailableNow = await (from e in _db.Enrollments
                                            join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                                            join q in _db.Quizzes on s.QuizId equals q.Id
                                            where e.StudentProfileId == spid
                                                  && q.Type == QuizType.Coding
                                                  && s.StartAt <= now && now <= s.EndAt
                                            select s.Id).CountAsync();

            var codingUpcoming = await (from e in _db.Enrollments
                                        join s in _db.QuizSchedules on e.QuizId equals s.QuizId
                                        join q in _db.Quizzes on s.QuizId equals q.Id
                                        where e.StudentProfileId == spid
                                              && q.Type == QuizType.Coding
                                              && s.StartAt > now && s.StartAt <= soon
                                        select s.Id).CountAsync();

            var vm = new StudentPanelsVm
            {
                McqAvailableNow = mcqAvailableNow,
                McqUpcoming = mcqUpcoming,
                CodingAvailableNow = codingAvailableNow,
                CodingUpcoming = codingUpcoming
            };

            return View("~/Areas/Student/Views/Dashboard/Index.cshtml", vm);
        }
    }

    public class StudentPanelsVm
    {
        public int McqAvailableNow { get; set; }
        public int McqUpcoming { get; set; }
        public int CodingAvailableNow { get; set; }
        public int CodingUpcoming { get; set; }
    }
}
