using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;
using Quiz_Application_College.ViewModels.Coding;

namespace Quiz_Application_College.Areas.Admin.Controllers.Coding
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    [Route("Admin/Coding/Quiz")]
    public class CodingQuizController : Controller
    {
        private readonly ApplicationDbContext _db;
        public CodingQuizController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/Coding/Quiz  and /Admin/Coding/Quiz/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var list = await _db.Quizzes
                .Where(q => q.Type == QuizType.Coding)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            return View("~/Areas/Admin/Views/Coding/Quiz/Index.cshtml", list);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            var vm = new QuizCreateVm
            {
                DurationMinutes = 60,
                TotalMarks = 0, // computed later from testcases
                EnableNegativeMarking = false,
                NegativeMarkPerWrong = 0,
                ShuffleQuestions = false,
                ShuffleOptions = false,
                ShowReviewOnSubmit = true,
                ShowScoreOnSubmit = true
            };
            return View("~/Areas/Admin/Views/Coding/Quiz/Create.cshtml", vm);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuizCreateVm vm)
        {
            // enforce coding-safe flags
            vm.EnableNegativeMarking = false;
            vm.NegativeMarkPerWrong = 0;
            vm.ShuffleQuestions = false;
            vm.ShuffleOptions = false;
            vm.TotalMarks = 0; // computed later from testcases

            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/Coding/Quiz/Create.cshtml", vm);

            var quiz = new Quiz
            {
                Title = vm.Title,
                Description = vm.Description,
                DurationMinutes = vm.DurationMinutes,
                TotalMarks = vm.TotalMarks,
                EnableNegativeMarking = false,
                NegativeMarkPerWrong = 0,
                ShuffleQuestions = false,
                ShuffleOptions = false,
                ShowReviewOnSubmit = vm.ShowReviewOnSubmit,
                ShowScoreOnSubmit = vm.ShowScoreOnSubmit,
                IsPublished = false,
                CreatedAt = DateTimeOffset.Now,

                // NEW: mark as Coding
                Type = QuizType.Coding
            };

            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            return View("~/Areas/Admin/Views/Coding/Quiz/Edit.cshtml", quiz);
        }

        [HttpPost("Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Quiz form)
        {
            if (id != form.Id) return BadRequest();
            if (!ModelState.IsValid) return View("~/Areas/Admin/Views/Coding/Quiz/Edit.cshtml", form);

            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            quiz.Title = form.Title;
            quiz.Description = form.Description;
            quiz.DurationMinutes = form.DurationMinutes;

            // coding-safe flags (no negative/shuffle)
            quiz.EnableNegativeMarking = false;
            quiz.NegativeMarkPerWrong = 0;
            quiz.ShuffleQuestions = false;
            quiz.ShuffleOptions = false;

            quiz.ShowReviewOnSubmit = form.ShowReviewOnSubmit;
            quiz.ShowScoreOnSubmit = form.ShowScoreOnSubmit;
            quiz.UpdatedAt = DateTimeOffset.Now;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Publish/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();
            quiz.IsPublished = true;
            quiz.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Unpublish/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unpublish(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();
            quiz.IsPublished = false;
            quiz.UpdatedAt = DateTimeOffset.Now;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
