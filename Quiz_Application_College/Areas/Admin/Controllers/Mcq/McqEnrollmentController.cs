using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    // Make the URL nice: /Admin/MCQ/Enrollment/...
    [Route("Admin/MCQ/Enrollment")]
    public class McqEnrollmentController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public McqEnrollmentController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET: /Admin/MCQ/Enrollment  and /Admin/MCQ/Enrollment/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var data = await _db.Enrollments
                .Include(e => e.Quiz)
                .Where(e => e.Quiz != null && e.Quiz.Type == QuizType.Mcq)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            // Load user emails in-memory (small join)
            var userIds = data.Select(d => d.UserId).Distinct().ToList();
            var emails = new Dictionary<string, string>();
            foreach (var id in userIds)
            {
                var u = await _userManager.FindByIdAsync(id);
                if (u != null) emails[id] = u.Email ?? id;
            }
            ViewBag.UserEmails = emails;

            return View("~/Areas/Admin/Views/Mcq/Enrollment/Index.cshtml", data);
        }

        // GET: /Admin/MCQ/Enrollment/Create
        // GET: /Admin/MCQ/Enrollment/Create  --> Filter + Enroll UI
        [HttpGet("Create")]
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

            // Students (filtered + paged)
            var q = _db.StudentProfiles.Join(_db.Users, p => p.UserId, u => u.Id, (p, u) => new { p, u }).AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College)) q = q.Where(x => x.p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department)) q = q.Where(x => x.p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim().ToLower();
                q = q.Where(x =>
                    (x.p.Name != null && x.p.Name.ToLower().Contains(s)) ||
                    (x.p.RollNumber != null && x.p.RollNumber.ToLower().Contains(s)) ||
                    (x.u.Email != null && x.u.Email.ToLower().Contains(s)));
            }

            vm.Total = await q.CountAsync();
            var skip = (vm.Page - 1) * vm.PageSize;

            vm.Rows = await q.OrderBy(x => x.p.College).ThenBy(x => x.p.Department).ThenBy(x => x.p.RollNumber)
                .Skip(skip).Take(vm.PageSize)
                .Select(x => new StudentEnrolledBrowseVm.Row
                {
                    UserId = x.p.UserId,
                    College = x.p.College,
                    Department = x.p.Department,
                    RollNumber = x.p.RollNumber,
                    Name = x.p.Name,
                    Email = x.u.Email ?? "",
                    CreatedAt = x.p.CreatedAt
                }).ToListAsync();

            // Schedule dropdown (MCQ only)
            var items = await _db.QuizSchedules.Include(s => s.Quiz)
                .Where(s => s.Quiz != null && s.Quiz.Type == QuizType.Mcq)
                .OrderByDescending(s => s.StartAt)
                .Select(s => new {
                    s.Id,
                    Title = s.Quiz!.Title + " — " + s.StartAt.ToString("g") + " to " + s.EndAt.ToString("g")
                })
                .ToListAsync();
            ViewBag.ScheduleOptions = new SelectList(items, "Id", "Title");

            // Reuse Create view (now shows filter+enroll)
            return View("~/Areas/Admin/Views/Mcq/Enrollment/Create.cshtml", vm);
        }

        // POST: /Admin/MCQ/Enrollment/Create  --> Enroll ALL filtered into selected schedule
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid scheduleId, StudentEnrolledBrowseVm vm)
        {
            var schedule = await _db.QuizSchedules.Include(s => s.Quiz)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Quiz != null && s.Quiz.Type == QuizType.Mcq);

            if (schedule == null)
            {
                TempData["Err"] = "Please select a valid MCQ schedule.";
                return RedirectToAction(nameof(Create), vm);
            }

            // Build filtered cohort again (without paging!)
            var q = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College)) q = q.Where(p => p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department)) q = q.Where(p => p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim().ToLower();
                q = q.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(s)) ||
                    (p.RollNumber != null && p.RollNumber.ToLower().Contains(s)));
            }

            var userIds = await q.Select(p => p.UserId).ToListAsync();

            int created = 0, skipped = 0;
            foreach (var uid in userIds)
            {
                bool exists = await _db.Enrollments.AnyAsync(e => e.QuizId == schedule.QuizId && e.UserId == uid);
                if (exists) { skipped++; continue; }

                _db.Enrollments.Add(new Enrollment
                {
                    QuizId = schedule.QuizId,
                    UserId = uid,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                created++;
            }

            await _db.SaveChangesAsync();
            TempData["Ok"] = $"Enrolled: {created}, Already enrolled: {skipped}.";
            // Force the MCQ route explicitly so we don't lose the "MCQ" segment
            var college = Uri.EscapeDataString(vm.College ?? "");
            var department = Uri.EscapeDataString(vm.Department ?? "");
            var search = Uri.EscapeDataString(vm.Search ?? "");
            return Redirect($"/Admin/MCQ/Enrollment/Create?College={college}&Department={department}&Search={search}&Page={vm.Page}&PageSize={vm.PageSize}");
        }


        // POST: /Admin/MCQ/Enrollment/Delete/{id}
        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var e = await _db.Enrollments.FindAsync(id);
            if (e != null)
            {
                _db.Enrollments.Remove(e);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("FilterEnroll")]
        public async Task<IActionResult> FilterEnroll([FromQuery] StudentEnrolledBrowseVm vm)
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

            // Filtered rows
            var q = _db.StudentProfiles.Join(_db.Users, p => p.UserId, u => u.Id, (p, u) => new { p, u }).AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College)) q = q.Where(x => x.p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department)) q = q.Where(x => x.p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim().ToLower();
                q = q.Where(x =>
                    (x.p.Name != null && x.p.Name.ToLower().Contains(s)) ||
                    (x.p.RollNumber != null && x.p.RollNumber.ToLower().Contains(s)) ||
                    (x.u.Email != null && x.u.Email.ToLower().Contains(s)));
            }

            vm.Total = await q.CountAsync();
            var skip = (vm.Page - 1) * vm.PageSize;

            vm.Rows = await q.OrderBy(x => x.p.College).ThenBy(x => x.p.Department).ThenBy(x => x.p.RollNumber)
                .Skip(skip).Take(vm.PageSize)
                .Select(x => new StudentEnrolledBrowseVm.Row
                {
                    UserId = x.p.UserId,
                    College = x.p.College,
                    Department = x.p.Department,
                    RollNumber = x.p.RollNumber,
                    Name = x.p.Name,
                    Email = x.u.Email ?? "",
                    CreatedAt = x.p.CreatedAt
                }).ToListAsync();

            // Schedules dropdown (MCQ only)
            var items = await _db.QuizSchedules.Include(s => s.Quiz)
                .Where(s => s.Quiz != null && s.Quiz.Type == QuizType.Mcq)
                .OrderByDescending(s => s.StartAt)
                .Select(s => new { s.Id, Title = s.Quiz!.Title + " — " + s.StartAt.ToString("g") + " to " + s.EndAt.ToString("g") })
                .ToListAsync();

            ViewBag.ScheduleOptions = new SelectList(items, "Id", "Title");
            return View("~/Areas/Admin/Views/Mcq/Enrollment/FilterEnroll.cshtml", vm);
        }

        // ---------- POST: Enroll filtered (MCQ) ----------
        [HttpPost("FilterEnroll")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FilterEnroll(Guid scheduleId, StudentEnrolledBrowseVm vm)
        {
            var schedule = await _db.QuizSchedules.Include(s => s.Quiz)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Quiz != null && s.Quiz.Type == QuizType.Mcq);

            if (schedule == null)
            {
                TempData["Err"] = "Select a valid MCQ schedule.";
                return RedirectToAction(nameof(FilterEnroll), vm);
            }

            // Reapply filter
            var q = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College)) q = q.Where(p => p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department)) q = q.Where(p => p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim().ToLower();
                q = q.Where(p => (p.Name != null && p.Name.ToLower().Contains(s))
                              || (p.RollNumber != null && p.RollNumber.ToLower().Contains(s)));
            }

            var userIds = await q.Select(p => p.UserId).ToListAsync();
            int created = 0, skipped = 0;

            foreach (var uid in userIds)
            {
                bool exists = await _db.Enrollments.AnyAsync(e => e.QuizId == schedule.QuizId && e.UserId == uid);
                if (exists) { skipped++; continue; }

                _db.Enrollments.Add(new Enrollment
                {
                    QuizId = schedule.QuizId,
                    UserId = uid,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                created++;
            }

            await _db.SaveChangesAsync();
            TempData["Ok"] = $"Enrolled: {created}, Skipped existing: {skipped}.";
            return RedirectToAction(nameof(FilterEnroll), vm);
        }
        private async Task PopulateQuizzes()
        {
            var list = await _db.Quizzes
                .Where(q => q.Type == QuizType.Mcq)   // NEW filter
                .OrderBy(q => q.Title)
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            ViewBag.QuizOptions = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(list, "Id", "Title");
        }

    }
}
