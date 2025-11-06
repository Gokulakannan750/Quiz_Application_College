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
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateQuizzes();
            return View("~/Areas/Admin/Views/Mcq/Enrollment/Create.cshtml", new EnrollmentCreateVm());
        }

        // POST: /Admin/MCQ/Enrollment/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EnrollmentCreateVm vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateQuizzes();
                return View("~/Areas/Admin/Views/Mcq/Enrollment/Create.cshtml", vm);
            }

            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null)
            {
                ModelState.AddModelError(nameof(vm.Email), "User not found. Make sure the student has registered.");
                await PopulateQuizzes();
                return View("~/Areas/Admin/Views/Mcq/Enrollment/Create.cshtml", vm);
            }

            var exists = await _db.Enrollments.AnyAsync(e => e.QuizId == vm.QuizId && e.UserId == user.Id);
            if (exists)
            {
                ModelState.AddModelError("", "This user is already enrolled for the selected quiz.");
                await PopulateQuizzes();
                return View("~/Areas/Admin/Views/Mcq/Enrollment/Create.cshtml", vm);
            }

            _db.Enrollments.Add(new Enrollment { QuizId = vm.QuizId, UserId = user.Id, Status = "Active" });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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
