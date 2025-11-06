using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;

namespace Quiz_Application_College.Areas.Admin.Controllers.Coding
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    [Route("Admin/Coding/Enrollment")]
    public class EnrollmentController : Controller
    {
        private readonly ApplicationDbContext _db;
        public EnrollmentController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/Coding/Enrollment
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            // Join Enrollments -> Quizzes -> Users to show Email
            var rows = await _db.Enrollments
                .Include(e => e.Quiz)
                .Where(e => e.Quiz != null && e.Quiz.Type == QuizType.Coding)
                .Select(e => new EnrollmentRow
                {
                    Id = e.Id,
                    QuizId = e.QuizId,
                    QuizTitle = e.Quiz!.Title,
                    UserId = e.UserId,
                    // look up email from AspNetUsers table
                    UserEmail = _db.Users.Where(u => u.Id == e.UserId).Select(u => u.Email).FirstOrDefault() ?? "(unknown)",
                    CreatedAt = e.CreatedAt
                })
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View("~/Areas/Admin/Views/Coding/Enrollment/Index.cshtml", rows);
        }

        // GET: /Admin/Coding/Enrollment/Create
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            await PopulateCodingQuizzes();
            return View("~/Areas/Admin/Views/Coding/Enrollment/Create.cshtml");
        }

        // POST: /Admin/Coding/Enrollment/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid quizId, string email)
        {
            await PopulateCodingQuizzes();

            if (quizId == Guid.Empty)
                ModelState.AddModelError("quizId", "Please select a quiz.");

            var isCodingQuiz = await _db.Quizzes.AnyAsync(q => q.Id == quizId && q.Type == QuizType.Coding);
            if (!isCodingQuiz)
                ModelState.AddModelError("quizId", "Selected quiz must be of type Coding.");

            if (string.IsNullOrWhiteSpace(email))
                ModelState.AddModelError("email", "Student email is required.");

            // resolve email -> userId
            var userId = await _db.Users
                .Where(u => u.Email != null && u.Email.ToLower() == email.Trim().ToLower())
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (userId == null)
                ModelState.AddModelError("email", "No user found with this email.");

            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/Coding/Enrollment/Create.cshtml");

            // prevent duplicates for the same quiz + user
            var already = await _db.Enrollments.AnyAsync(e => e.QuizId == quizId && e.UserId == userId);
            if (already)
            {
                TempData["Err"] = "This student is already enrolled for the selected quiz.";
                return RedirectToAction(nameof(Index));
            }

            var enrollment = new Enrollment
            {
                QuizId = quizId,
                UserId = userId!,                  // store by UserId (foreign key)
                CreatedAt = DateTimeOffset.UtcNow  // remove if your model lacks this column
            };

            _db.Enrollments.Add(enrollment);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Enrollment created.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Coding/Enrollment/Edit/{id}
        [HttpGet("Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var e = await _db.Enrollments.Include(x => x.Quiz)
                .FirstOrDefaultAsync(x => x.Id == id && x.Quiz != null && x.Quiz.Type == QuizType.Coding);
            if (e == null) return NotFound();

            await PopulateCodingQuizzes();

            var currentEmail = await _db.Users.Where(u => u.Id == e.UserId).Select(u => u.Email).FirstOrDefaultAsync() ?? "";

            ViewBag.Item = new EnrollmentRow
            {
                Id = e.Id,
                QuizId = e.QuizId,
                QuizTitle = e.Quiz!.Title,
                UserId = e.UserId,
                UserEmail = currentEmail
            };

            return View("~/Areas/Admin/Views/Coding/Enrollment/Edit.cshtml");
        }

        // POST: /Admin/Coding/Enrollment/Edit/{id}
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

            // resolve email -> userId
            var userId = await _db.Users
                .Where(u => u.Email != null && u.Email.ToLower() == email.Trim().ToLower())
                .Select(u => u.Id)
                .FirstOrDefaultAsync();

            if (userId == null)
                ModelState.AddModelError("email", "No user found with this email.");

            // Optional: block duplicates on edit
            if (userId != null)
            {
                var duplicate = await _db.Enrollments.AnyAsync(x => x.Id != id && x.QuizId == quizId && x.UserId == userId);
                if (duplicate)
                    ModelState.AddModelError("email", "Another enrollment already exists for this quiz + email.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateCodingQuizzes();
                ViewBag.Item = new EnrollmentRow
                {
                    Id = e.Id,
                    QuizId = quizId,
                    QuizTitle = e.Quiz!.Title,
                    UserId = e.UserId,
                    UserEmail = email
                };
                return View("~/Areas/Admin/Views/Coding/Enrollment/Edit.cshtml");
            }

            // Save
            e.QuizId = quizId;
            e.UserId = userId!;
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Enrollment updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Coding/Enrollment/Delete/{id}
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

        // Simple row VM used by views
        public class EnrollmentRow
        {
            public Guid Id { get; set; }
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public string UserId { get; set; } = "";
            public string UserEmail { get; set; } = "";
            public DateTimeOffset CreatedAt { get; set; }
        }
    }
}
