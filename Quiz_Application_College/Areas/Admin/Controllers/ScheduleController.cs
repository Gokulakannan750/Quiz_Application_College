using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ScheduleController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/Schedule
        public async Task<IActionResult> Index()
        {
            var data = await _db.QuizSchedules
                .Include(s => s.Quiz)
                .OrderByDescending(s => s.StartAt)
                .ToListAsync();
            return View(data);
        }

        // GET: /Admin/Schedule/Create
        public async Task<IActionResult> Create()
        {
            await PopulateQuizzes();
            return View(new ScheduleCreateVm
            {
                StartAt = DateTimeOffset.UtcNow.AddHours(1),
                EndAt = DateTimeOffset.UtcNow.AddHours(2)
            });
        }

        // POST: /Admin/Schedule/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScheduleCreateVm vm)
        {
            if (vm.EndAt <= vm.StartAt)
                ModelState.AddModelError(nameof(vm.EndAt), "End time must be after start time.");

            if (!ModelState.IsValid)
            {
                await PopulateQuizzes();
                return View(vm);
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

            return RedirectToAction(nameof(Index));
        }

        //  POST: /Admin/Schedule/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id, Guid quizId)
        {
            var sched = await _db.QuizSchedules
                .FirstOrDefaultAsync(s => s.Id == id && s.QuizId == quizId);
            if (sched != null)
            {
                _db.QuizSchedules.Remove(sched);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Schedule deleted.";
            }
            // ✅ Return to the schedules list page
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateQuizzes()
        {
            var list = await _db.Quizzes
                .OrderBy(q => q.Title)
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            ViewBag.QuizOptions = new SelectList(list, "Id", "Title");
        }
    }
}
