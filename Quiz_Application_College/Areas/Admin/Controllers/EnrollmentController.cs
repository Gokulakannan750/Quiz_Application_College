using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    public class EnrollmentController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public EnrollmentController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET: /Admin/Enrollment
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

            return View(data);
        }

        // GET: /Admin/Enrollment/Create
        public async Task<IActionResult> Create()
        {
            await PopulateQuizzes();
            return View(new EnrollmentCreateVm());
        }

        // POST: /Admin/Enrollment/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EnrollmentCreateVm vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateQuizzes();
                return View(vm);
            }

            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null)
            {
                ModelState.AddModelError(nameof(vm.Email), "User not found. Make sure the student has registered.");
                await PopulateQuizzes();
                return View(vm);
            }

            var exists = await _db.Enrollments.AnyAsync(e => e.QuizId == vm.QuizId && e.UserId == user.Id);
            if (exists)
            {
                ModelState.AddModelError("", "This user is already enrolled for the selected quiz.");
                await PopulateQuizzes();
                return View(vm);
            }

            _db.Enrollments.Add(new Enrollment { QuizId = vm.QuizId, UserId = user.Id, Status = "Active" });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Enrollment/Delete/{id}
        [HttpPost]
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
                .OrderBy(q => q.Title)
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            ViewBag.QuizOptions = new SelectList(list, "Id", "Title");
        }
    }
}
