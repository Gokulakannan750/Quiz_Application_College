using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;
using Quiz_Application_College.Utils;

namespace Quiz_Application_College.Areas.Admin.Controllers.Coding
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    // Routes under /Admin/Coding/Schedule/...
    [Route("Admin/Coding/Schedule")]
    public class CodingScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CodingScheduleController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/Coding/Schedule  and /Admin/Coding/Schedule/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var data = await _db.QuizSchedules
                .Include(s => s.Quiz)
                .Where(s => s.Quiz != null && s.Quiz.Type == QuizType.Coding)
                .OrderByDescending(s => s.StartAt)
                .ToListAsync();

            return View("~/Areas/Admin/Views/Coding/Schedule/Index.cshtml", data);
        }

        // GET: /Admin/Coding/Schedule/Create
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateCodingQuizzes();
            var vm = new ScheduleCreateVm
            {
                StartAt = DateTimeOffset.Now.AddHours(1),
                EndAt = DateTimeOffset.Now.AddHours(2),
                MaxAttempts = 1,
                Timezone = TimeZoneInfo.Local.Id
            };
            return View("~/Areas/Admin/Views/Coding/Schedule/Create.cshtml", vm);
        }

        // POST: /Admin/Coding/Schedule/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScheduleCreateVm vm)
        {
            // Normalize timezone (accepts "Asia/Kolkata" or "India Standard Time")
            var tz = Quiz_Application_College.Utils.TimeHelper.NormalizeTz(
                string.IsNullOrWhiteSpace(vm.Timezone) ? TimeZoneInfo.Local.Id : vm.Timezone!
            );

            // Convert whatever the UI posted to UTC for consistent storage
            var startUtc = vm.StartAt.ToUniversalTime();
            var endUtc = vm.EndAt.ToUniversalTime();

            if (endUtc <= startUtc)
                ModelState.AddModelError(nameof(vm.EndAt), "End time must be after start time.");

            // Ensure a coding quiz is selected
            var isCodingQuiz = await _db.Quizzes.AnyAsync(q => q.Id == vm.QuizId && q.Type == QuizType.Coding);
            if (!isCodingQuiz)
                ModelState.AddModelError(nameof(vm.QuizId), "Please select a Coding quiz.");

            if (!ModelState.IsValid)
            {
                await PopulateCodingQuizzes();
                // Keep user-entered values, but normalize tz for the re-render
                vm.Timezone = tz;
                return View("~/Areas/Admin/Views/Coding/Schedule/Create.cshtml", vm);
            }

            var entity = new QuizSchedule
            {
                QuizId = vm.QuizId,
                StartAt = startUtc,          // ✅ UTC in DB
                EndAt = endUtc,            // ✅ UTC in DB
                MaxAttempts = vm.MaxAttempts <= 0 ? 1 : vm.MaxAttempts,
                Timezone = tz                 // ✅ canonical timezone stored once
            };

            _db.QuizSchedules.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Coding schedule created.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Coding/Schedule/Delete/{id}
        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var sched = await _db.QuizSchedules
                .Include(s => s.Quiz)
                .FirstOrDefaultAsync(s => s.Id == id && s.Quiz != null && s.Quiz.Type == QuizType.Coding);

            if (sched != null)
            {
                _db.QuizSchedules.Remove(sched);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Schedule deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        // Helpers
        private async Task PopulateCodingQuizzes()
        {
            var list = await _db.Quizzes
                .Where(q => q.Type == QuizType.Coding)
                .OrderBy(q => q.Title)
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            ViewBag.QuizOptions = new SelectList(list, "Id", "Title");
        }
    }
}
