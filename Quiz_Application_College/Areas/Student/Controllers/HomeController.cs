using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;

namespace Quiz_Application_College.Areas.Student.Controllers
{
    [Area("Student")]
    [Authorize(Roles = "Student")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        public HomeController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
            var now = DateTimeOffset.Now;

            ViewBag.EnrolledCount = await _db.Enrollments.Where(e => e.UserId == userId).CountAsync();

            ViewBag.OpenNowCount = await _db.Enrollments
                .Where(e => e.UserId == userId)
                .Join(_db.QuizSchedules, e => e.QuizId, s => s.QuizId, (e, s) => s)
                .Where(s => s.StartAt <= now && now <= s.EndAt).CountAsync();

            ViewBag.AttemptsCount = await _db.Attempts.Where(a => a.UserId == userId).CountAsync();

            return View();
        }


    }
}
