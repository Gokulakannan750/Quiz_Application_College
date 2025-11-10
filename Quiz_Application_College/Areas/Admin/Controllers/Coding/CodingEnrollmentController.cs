using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;
using Quiz_Application_College.ViewModels.Coding;

namespace Quiz_Application_College.Areas.Admin.Controllers.Coding
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    // All actions live under /Admin/Coding/Enrollment/...
    [Route("Admin/Coding/Enrollment")]
    public class CodingEnrollmentController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CodingEnrollmentController(ApplicationDbContext db) => _db = db;

        private static string? Normalize(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return null;
            var t = v.Trim();
            return (t.Equals("All", StringComparison.OrdinalIgnoreCase)
                 || t.Equals("-- All --", StringComparison.OrdinalIgnoreCase))
                 ? null : t;
        }

        private async Task PopulateCodingQuizzes()
        {
            var list = await _db.Quizzes
                .Where(q => q.Type == QuizType.Coding)
                .OrderBy(q => q.Title)
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            ViewBag.QuizOptions = new SelectList(list, "Id", "Title");
        }

        // ------------------ INDEX (SUMMARY LIST) ------------------

        // GET: /Admin/Coding/Enrollment  OR /Admin/Coding/Enrollment/Index
        [HttpGet("", Name = "AdminCodingEnrollmentIndex")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var data = await _db.Enrollments
                .Where(e => e.Quiz != null && e.Quiz.Type == QuizType.Coding)
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

            return View("~/Areas/Admin/Views/Coding/Enrollment/Index.cshtml", data);
        }

        // POST: /Admin/Coding/Enrollment/DeleteGroup
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

            return RedirectToAction(nameof(Index));
        }

        // ------------------ CREATE (filter + bulk enroll) ------------------

        // GET: /Admin/Coding/Enrollment/Create
        [HttpGet("Create", Name = "AdminCodingEnrollmentCreate")]
        public async Task<IActionResult> Create([FromQuery] StudentEnrolledBrowseVm vm)
        {
            vm.College = Normalize(vm.College);
            vm.Department = Normalize(vm.Department);
            vm.Search = string.IsNullOrWhiteSpace(vm.Search) ? null : vm.Search.Trim();

            var colleges = await _db.StudentProfiles.Select(p => p.College).Distinct().OrderBy(s => s).ToListAsync();
            vm.CollegeOptions = new SelectList(colleges);

            var deptQ = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College))
                deptQ = deptQ.Where(p => p.College == vm.College);
            var depts = await deptQ.Select(p => p.Department).Distinct().OrderBy(s => s).ToListAsync();
            vm.DepartmentOptions = new SelectList(depts);

            var q = (from p in _db.StudentProfiles
                     join u in _db.Users on p.UserId equals u.Id into gj
                     from u in gj.DefaultIfEmpty()
                     select new { p, u }).AsQueryable();

            if (!string.IsNullOrWhiteSpace(vm.College)) q = q.Where(x => x.p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department)) q = q.Where(x => x.p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.ToLower();
                q = q.Where(x =>
                    (x.p.Name != null && x.p.Name.ToLower().Contains(s)) ||
                    (x.p.RollNumber != null && x.p.RollNumber.ToLower().Contains(s)) ||
                    (x.u != null && x.u.Email != null && x.u.Email.ToLower().Contains(s)));
            }

            vm.Total = await q.CountAsync();
            var skip = (vm.Page - 1) * vm.PageSize;

            vm.Rows = await q
                .OrderBy(x => x.p.College).ThenBy(x => x.p.Department).ThenBy(x => x.p.RollNumber)
                .Skip(skip).Take(vm.PageSize)
                .Select(x => new StudentEnrolledBrowseVm.Row
                {
                    UserId = x.p.UserId,
                    College = x.p.College,
                    Department = x.p.Department,
                    RollNumber = x.p.RollNumber,
                    Name = x.p.Name,
                    Email = x.u != null ? (x.u.Email ?? "") : "",
                    CreatedAt = x.p.CreatedAt
                }).ToListAsync();

            var items = await _db.QuizSchedules.Include(s => s.Quiz)
                .Where(s => s.Quiz != null && s.Quiz.Type == QuizType.Coding)
                .OrderByDescending(s => s.StartAt)
                .Select(s => new {
                    s.Id,
                    Title = s.Quiz!.Title + " — " + s.StartAt.ToString("g") + " to " + s.EndAt.ToString("g")
                }).ToListAsync();

            ViewBag.ScheduleOptions = new SelectList(items, "Id", "Title");
            return View("~/Areas/Admin/Views/Coding/Enrollment/Create.cshtml", vm);
        }

        // POST: /Admin/Coding/Enrollment/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid scheduleId, StudentEnrolledBrowseVm vm)
        {
            vm.College = Normalize(vm.College);
            vm.Department = Normalize(vm.Department);
            vm.Search = string.IsNullOrWhiteSpace(vm.Search) ? null : vm.Search.Trim();

            var schedule = await _db.QuizSchedules
                .Include(s => s.Quiz)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Quiz != null && s.Quiz.Type == QuizType.Coding);

            if (schedule == null)
            {
                TempData["Err"] = "Please select a valid Coding schedule.";
                return RedirectToRoute("AdminCodingEnrollmentCreate", vm);
            }

            var q = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College)) q = q.Where(p => p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department)) q = q.Where(p => p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.ToLower();
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
            return RedirectToRoute("AdminCodingEnrollmentCreate", vm);
        }

        // ------------------ EDIT / DELETE (single) ------------------

        [HttpGet("Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var e = await _db.Enrollments.Include(x => x.Quiz)
                .FirstOrDefaultAsync(x => x.Id == id && x.Quiz != null && x.Quiz.Type == QuizType.Coding);
            if (e == null) return NotFound();

            await PopulateCodingQuizzes();

            var currentEmail = await _db.Users.Where(u => u.Id == e.UserId).Select(u => u.Email).FirstOrDefaultAsync() ?? "";

            ViewBag.Item = new EnrollmentRowVm
            {
                Id = e.Id,
                QuizId = e.QuizId,
                QuizTitle = e.Quiz!.Title,
                UserId = e.UserId,
                Email = currentEmail
            };

            return View("~/Areas/Admin/Views/Coding/Enrollment/Edit.cshtml");
        }

        [HttpPost("Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Guid quizId, string email)
        {
            var e = await _db.Enrollments.Include(x => x.Quiz)
                .FirstOrDefaultAsync(x => x.Id == id && x.Quiz != null && x.Quiz.Type == QuizType.Coding);
            if (e == null) return NotFound();

            if (quizId == Guid.Empty)
                ModelState.AddModelError("quizId", "Please select a quiz.");

            var isCodingQuiz = await _db.Quizzes.AnyAsync(q => q.Id == quizId && q.Type == QuizType.Coding);
            if (!isCodingQuiz)
                ModelState.AddModelError("quizId", "Selected quiz must be of type Coding.");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Student email is required.");

            var userId = await _db.Users
                .Where(u => u.Email != null && u.Email.ToLower() == email.Trim().ToLower())
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (userId == null)
                ModelState.AddModelError("email", "No user found with this email.");

            if (userId != null)
            {
                var duplicate = await _db.Enrollments.AnyAsync(x => x.Id != id && x.QuizId == quizId && x.UserId == userId);
                if (duplicate)
                    ModelState.AddModelError("email", "Another enrollment already exists for this quiz + email.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCodingQuizzes();
                ViewBag.Item = new EnrollmentRowVm
                {
                    Id = e.Id,
                    QuizId = quizId,
                    QuizTitle = e.Quiz!.Title,
                    UserId = e.UserId,
                    Email = email
                };
                return View("~/Areas/Admin/Views/Coding/Enrollment/Edit.cshtml");
            }

            e.QuizId = quizId;
            e.UserId = userId!;
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Enrollment updated.";
            return RedirectToRoute("AdminCodingEnrollmentIndex");
        }

        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var e = await _db.Enrollments.Include(x => x.Quiz)
                .FirstOrDefaultAsync(x => x.Id == id && x.Quiz != null && x.Quiz.Type == QuizType.Coding);
            if (e != null)
            {
                _db.Enrollments.Remove(e);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Enrollment deleted.";
            }
            return RedirectToRoute("AdminCodingEnrollmentIndex");
        }
    }
}
