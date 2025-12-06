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

        // ----------------- LIST (SUMMARY) -----------------

        // GET: /Admin/MCQ/Enrollment  and /Admin/MCQ/Enrollment/Index
        [HttpGet("", Name = "AdminMcqEnrollmentIndex")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var data = await _db.Enrollments
                .Where(e => e.Quiz != null && e.Quiz.Type == QuizType.Mcq)
                .Select(e => new
                {
                    e.QuizId,
                    QuizTitle = e.Quiz.Title,
                    College = e.StudentProfile != null ? e.StudentProfile.College : null,
                    Department = e.StudentProfile != null ? e.StudentProfile.Department : null
                })
                .GroupBy(x => new
                {
                    x.QuizId,
                    x.QuizTitle,
                    College = x.College ?? "(Unknown)",
                    Department = x.Department ?? "(Unknown)"
                })
                .Select(g => new EnrollmentSummaryVm
                {
                    QuizId = g.Key.QuizId,
                    QuizTitle = g.Key.QuizTitle,
                    College = g.Key.College,
                    Department = g.Key.Department,
                    TotalStudents = g.Count()
                })
                .OrderBy(x => x.QuizTitle)
                .ThenBy(x => x.College)
                .ThenBy(x => x.Department)
                .ToListAsync();

            return View("~/Areas/Admin/Views/Mcq/Enrollment/Index.cshtml", data);
        }

        // POST: /Admin/MCQ/Enrollment/DeleteGroup
        [HttpPost("DeleteGroup")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGroup(Guid quizId, string college, string department)
        {
            college = string.IsNullOrWhiteSpace(college) ? null : college;
            department = string.IsNullOrWhiteSpace(department) ? null : department;

            var q = _db.Enrollments.Where(e => e.QuizId == quizId);

            if (college == "(Unknown)")
                q = q.Where(e => e.StudentProfile == null || e.StudentProfile.College == null);
            else
                q = q.Where(e => e.StudentProfile != null && e.StudentProfile.College == college);

            if (department == "(Unknown)")
                q = q.Where(e => e.StudentProfile == null || e.StudentProfile.Department == null);
            else
                q = q.Where(e => e.StudentProfile != null && e.StudentProfile.Department == department);

            var toDelete = await q.ToListAsync();
            if (toDelete.Count > 0)
            {
                _db.Enrollments.RemoveRange(toDelete);
                await _db.SaveChangesAsync();
                TempData["Ok"] = $"Deleted {toDelete.Count} enrollment(s).";
            }
            else
            {
                TempData["Info"] = "No enrollments found for the selected group.";
            }

            return RedirectToRoute("AdminMcqEnrollmentIndex");
        }

        // ----------------- CREATE (filter + bulk enroll) -----------------

        // GET: /Admin/MCQ/Enrollment/Create
        [HttpGet("Create", Name = "AdminMcqEnrollmentCreate")]
        public async Task<IActionResult> Create([FromQuery] StudentEnrolledBrowseVm vm)
        {
            var colleges = await _db.StudentProfiles
                .Select(p => p.College).Distinct().OrderBy(s => s).ToListAsync();
            vm.CollegeOptions = new SelectList(colleges);

            var deptQ = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College))
                deptQ = deptQ.Where(p => p.College == vm.College);
            var depts = await deptQ.Select(p => p.Department).Distinct().OrderBy(s => s).ToListAsync();
            vm.DepartmentOptions = new SelectList(depts);

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
                    College = p.College,
                    Department = p.Department,
                    RollNumber = p.RollNumber,
                    Name = p.Name,
                    Email = p.Email ?? "",
                    CreatedAt = p.CreatedAt,
                })
                .ToListAsync();

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

        // POST: /Admin/MCQ/Enrollment/Create
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

            var profileIds = (await q.Select(p => p.Id).ToListAsync()).Distinct().ToList();

            var existingSet = (await _db.Enrollments
                .Where(e => e.QuizId == schedule.QuizId)
                .Select(e => e.StudentProfileId)
                .ToListAsync())
                .ToHashSet();

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
                existingSet.Add(pid);
            }

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
