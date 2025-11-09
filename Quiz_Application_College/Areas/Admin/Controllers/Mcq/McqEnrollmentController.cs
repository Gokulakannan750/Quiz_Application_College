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
    // All MCQ enrollment URLs live under /Admin/MCQ/Enrollment/...
    [Route("Admin/MCQ/Enrollment")]
    public class McqEnrollmentController : Controller
    {
        private readonly ApplicationDbContext _db;
        public McqEnrollmentController(ApplicationDbContext db) => _db = db;

        // ----------------- LIST -----------------

        // GET: /Admin/MCQ/Enrollment  and /Admin/MCQ/Enrollment/Index
        [HttpGet("", Name = "AdminMcqEnrollmentIndex")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var rows = await _db.Enrollments
                .Include(e => e.Quiz)
                .Include(e => e.StudentProfile)
                .Where(e => e.Quiz != null && e.Quiz.Type == QuizType.Mcq)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    e.Id,
                    e.QuizId,
                    QuizTitle = e.Quiz!.Title,
                    Roll = e.StudentProfile!.RollNumber,
                    Name = e.StudentProfile!.Name,
                    Email = e.StudentProfile!.Email,
                    e.CreatedAt
                })
                .ToListAsync();

            return View("~/Areas/Admin/Views/Mcq/Enrollment/Index.cshtml", rows);
        }

        // ----------------- CREATE (filter + bulk enroll) -----------------

        // GET: /Admin/MCQ/Enrollment/Create
        [HttpGet("Create", Name = "AdminMcqEnrollmentCreate")]
        public async Task<IActionResult> Create([FromQuery] StudentEnrolledBrowseVm vm)
        {
            // Dropdowns
            var colleges = await _db.StudentProfiles
                .Select(p => p.College).Distinct().OrderBy(s => s).ToListAsync();
            vm.CollegeOptions = new SelectList(colleges);

            var deptQ = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College))
                deptQ = deptQ.Where(p => p.College == vm.College);
            var depts = await deptQ.Select(p => p.Department).Distinct().OrderBy(s => s).ToListAsync();
            vm.DepartmentOptions = new SelectList(depts);

            // Filtered PROFILES (no Identity join)
            var q = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College)) q = q.Where(p => p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department)) q = q.Where(p => p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim().ToLower();
                q = q.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(s)) ||
                    (p.RollNumber != null && p.RollNumber.ToLower().Contains(s)) ||
                    (p.Email != null && p.Email.ToLower().Contains(s)));
            }

            vm.Total = await q.CountAsync();
            var skip = (vm.Page - 1) * vm.PageSize;

            vm.Rows = await q
                .OrderBy(p => p.College).ThenBy(p => p.Department).ThenBy(p => p.RollNumber)
                .Skip(skip).Take(vm.PageSize)
                .Select(p => new StudentEnrolledBrowseVm.Row
                {
                    // We don’t need UserId anymore
                    College = p.College,
                    Department = p.Department,
                    RollNumber = p.RollNumber,
                    Name = p.Name,
                    Email = p.Email ?? "",
                    CreatedAt = p.CreatedAt,
                    // Keep an internal id if your VM supports it (not required for UI table)
                    // StudentProfileId = p.Id
                })
                .ToListAsync();

            // MCQ schedules
            var items = await _db.QuizSchedules.Include(s => s.Quiz)
                .Where(s => s.Quiz != null && s.Quiz.Type == QuizType.Mcq)
                .OrderByDescending(s => s.StartAt)
                .Select(s => new {
                    s.Id,
                    Title = s.Quiz!.Title + " — " + s.StartAt.ToString("g") + " to " + s.EndAt.ToString("g")
                })
                .ToListAsync();
            ViewBag.ScheduleOptions = new SelectList(items, "Id", "Title");

            return View("~/Areas/Admin/Views/Mcq/Enrollment/Create.cshtml", vm);
        }

        // POST: /Admin/MCQ/Enrollment/Create  (Enroll ALL filtered)
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid scheduleId, StudentEnrolledBrowseVm vm)
        {
            var schedule = await _db.QuizSchedules
                .Include(s => s.Quiz)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Quiz != null && s.Quiz.Type == QuizType.Mcq);

            if (schedule == null)
            {
                TempData["Err"] = "Please select a valid MCQ schedule.";
                return RedirectToRoute("AdminMcqEnrollmentCreate", vm);
            }

            // 1) Build the FILTER (profiles only; no Identity dependency)
            var q = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College))
                q = q.Where(p => p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department))
                q = q.Where(p => p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim().ToLower();
                q = q.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(s)) ||
                    (p.RollNumber != null && p.RollNumber.ToLower().Contains(s)) ||
                    (p.Email != null && p.Email.ToLower().Contains(s)));
            }

            // 2) Distinct profile ids to guard against dupes from the query
            var profileIds = (await q.Select(p => p.Id).ToListAsync()).Distinct().ToList();

            // 3) Preload existing enrollments for this quiz (idempotent server-side)
            var existingSet = (await _db.Enrollments
                .Where(e => e.QuizId == schedule.QuizId)
                .Select(e => e.StudentProfileId)
                .ToListAsync())
                .ToHashSet();

            // 4) Prepare new rows only for not-yet-enrolled profiles
            var toInsert = new List<Enrollment>(capacity: profileIds.Count);
            foreach (var pid in profileIds)
            {
                if (pid == Guid.Empty || existingSet.Contains(pid)) continue;

                toInsert.Add(new Enrollment
                {
                    QuizId = schedule.QuizId,
                    StudentProfileId = pid,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                existingSet.Add(pid); // keep set updated in-memory to prevent duplicates in this batch
            }

            // 5) Insert in one shot; handle race with unique index gracefully
            int created = 0, skipped = profileIds.Count - toInsert.Count;
            if (toInsert.Count > 0)
            {
                _db.Enrollments.AddRange(toInsert);
                try
                {
                    created = await _db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // In case a concurrent request inserted some rows, re-check how many exist now
                    var nowExisting = await _db.Enrollments
                        .Where(e => e.QuizId == schedule.QuizId && profileIds.Contains(e.StudentProfileId))
                        .CountAsync();
                    created = Math.Max(0, nowExisting - (profileIds.Count - toInsert.Count));
                    skipped = profileIds.Count - created;
                }
            }

            TempData["Ok"] = $"Enrolled: {created}, Already enrolled (same profile): {skipped}.";
            return RedirectToRoute("AdminMcqEnrollmentCreate", vm);
        }


        // ----------------- DELETE (single) -----------------

        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var e = await _db.Enrollments.Include(x => x.Quiz)
                .FirstOrDefaultAsync(x => x.Id == id && x.Quiz != null && x.Quiz.Type == QuizType.Mcq);
            if (e != null)
            {
                _db.Enrollments.Remove(e);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Enrollment deleted.";
            }
            return RedirectToRoute("AdminMcqEnrollmentIndex");
        }
    }
}
