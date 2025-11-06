using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;

namespace Quiz_Application_College.Areas.Admin.Controllers.Mcq
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    // Routes under /Admin/MCQ/Schedule...
    [Route("Admin/MCQ/Schedule")]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ScheduleController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/MCQ/Schedule  and /Admin/MCQ/Schedule/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var data = await _db.QuizSchedules
                .Include(s => s.Quiz)
                .OrderByDescending(s => s.StartAt)
                .ToListAsync();

            // ✅ Return the SCHEDULE Index view with a List<QuizSchedule>
            return View("~/Areas/Admin/Views/Mcq/Schedule/Index.cshtml", data);
        }

        // GET: /Admin/MCQ/Schedule/Create
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateQuizzes();
            var vm = new ScheduleCreateVm
            {
                StartAt = DateTimeOffset.Now.AddHours(1),
                EndAt = DateTimeOffset.Now.AddHours(2),
                MaxAttempts = 1,
                Timezone = TimeZoneInfo.Local.Id
            };
            // ✅ Return the SCHEDULE Create view with ScheduleCreateVm
            return View("~/Areas/Admin/Views/Mcq/Schedule/Create.cshtml", vm);
        }

        // POST: /Admin/MCQ/Schedule/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScheduleCreateVm vm)
        {
            if (vm.EndAt <= vm.StartAt)
                ModelState.AddModelError(nameof(vm.EndAt), "End time must be after start time.");

            if (!ModelState.IsValid)
            {
                await PopulateQuizzes();
                return View("~/Areas/Admin/Views/Mcq/Schedule/Create.cshtml", vm);
            }

            var s = new QuizSchedule
            {
                QuizId = vm.QuizId,
                StartAt = vm.StartAt,
                EndAt = vm.EndAt,
                MaxAttempts = vm.MaxAttempts,
                Timezone = vm.Timezone
            };

            _db.QuizSchedules.Add(s);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Schedule created.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/MCQ/Schedule/Delete/{id}
        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var sched = await _db.QuizSchedules.FirstOrDefaultAsync(s => s.Id == id);
            if (sched != null)
            {
                _db.QuizSchedules.Remove(sched);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Schedule deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        // Helpers
        private async Task PopulateQuizzes()
        {
            var list = await _db.Quizzes
                .Where(q => q.Type == QuizType.Mcq)   // only MCQ quizzes appear in dropdown
                .OrderBy(q => q.Title)
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            ViewBag.QuizOptions = new SelectList(list, "Id", "Title");
        }
    }
}
