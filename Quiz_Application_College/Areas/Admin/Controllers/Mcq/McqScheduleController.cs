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
                .Where(s => s.Quiz != null && s.Quiz.Type == QuizType.Mcq)
                .OrderByDescending(s => s.StartAt)
                .ToListAsync();

            // ✅ Return the SCHEDULE Index view with a List<QuizSchedule>
            return View("~/Areas/Admin/Views/Mcq/Schedule/Index.cshtml", data);
        }

        // GET: /Admin/MCQ/Schedule/Create
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateFolders();                 // folders dropdown
            await PopulateQuizzesForFolder(null);    // empty quiz list initially

            var vm = new ScheduleCreateVm
            {
                StartAt = DateTimeOffset.Now.AddHours(1),
                EndAt = DateTimeOffset.Now.AddHours(2),
                MaxAttempts = 1,
                Timezone = TimeZoneInfo.Local.Id
            };

            return View("~/Areas/Admin/Views/Mcq/Schedule/Create.cshtml", vm);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScheduleCreateVm vm, int? folderId)
        {
            // Normalize timezone
            var tz = Quiz_Application_College.Utils.TimeHelper.NormalizeTz(
                string.IsNullOrWhiteSpace(vm.Timezone) ? TimeZoneInfo.Local.Id : vm.Timezone!
            );

            var startUtc = vm.StartAt.ToUniversalTime();
            var endUtc = vm.EndAt.ToUniversalTime();

            if (endUtc <= startUtc)
                ModelState.AddModelError(nameof(vm.EndAt), "End time must be after start time.");

            if (vm.QuizId == Guid.Empty)
                ModelState.AddModelError(nameof(vm.QuizId), "Please select a quiz.");

            if (!ModelState.IsValid)
            {
                // Try to infer folder from quiz if not provided
                if (!folderId.HasValue && vm.QuizId != Guid.Empty)
                {
                    var quiz = await _db.Quizzes
                        .AsNoTracking()
                        .FirstOrDefaultAsync(q => q.Id == vm.QuizId);

                    folderId = quiz?.McqQuizFolderId;
                }

                await PopulateFolders(folderId);
                await PopulateQuizzesForFolder(folderId, vm.QuizId);

                vm.Timezone = tz;
                return View("~/Areas/Admin/Views/Mcq/Schedule/Create.cshtml", vm);
            }

            var sched = new QuizSchedule
            {
                QuizId = vm.QuizId,
                StartAt = startUtc,
                EndAt = endUtc,
                MaxAttempts = vm.MaxAttempts <= 0 ? 1 : vm.MaxAttempts,
                Timezone = tz
            };

            _db.QuizSchedules.Add(sched);
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
        private async Task PopulateFolders(int? selectedFolderId = null)
        {
            var folders = await _db.McqQuizFolders
                .OrderBy(f => f.Name)
                .Select(f => new { f.Id, f.Name })
                .ToListAsync();

            ViewBag.FolderOptions = new SelectList(folders, "Id", "Name", selectedFolderId);
        }

        private async Task PopulateQuizzesForFolder(int? folderId, Guid? selectedQuizId = null)
        {
            var query = _db.Quizzes
                .Where(q => q.Type == QuizType.Mcq);

            if (folderId.HasValue)
                query = query.Where(q => q.McqQuizFolderId == folderId);

            var list = await query
                .OrderBy(q => q.Title)
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            ViewBag.QuizOptions = new SelectList(list, "Id", "Title", selectedQuizId);
        }

        // AJAX endpoint: /Admin/MCQ/Schedule/QuizzesByFolder?folderId=1
        [HttpGet("QuizzesByFolder")]
        public async Task<IActionResult> QuizzesByFolder(int? folderId)
        {
            var query = _db.Quizzes
                .Where(q => q.Type == QuizType.Mcq);

            if (folderId.HasValue)
                query = query.Where(q => q.McqQuizFolderId == folderId);

            var list = await query
                .OrderBy(q => q.Title)
                .Select(q => new { id = q.Id, title = q.Title })
                .ToListAsync();

            return Json(list);
        }
    }
}
