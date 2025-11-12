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

        // GET: /Admin/Coding/Quiz/ManageQuestions/{id}
        [HttpGet("ManageQuestions/{id:guid}")]
        public async Task<IActionResult> ManageQuestions(Guid id)
        {
            var quiz = await _db.Quizzes.AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == id && q.Type == QuizType.Coding);
            if (quiz == null) return NotFound();

            var attached = await _db.QuizCodingQuestions
                .Where(x => x.QuizId == id)
                .Select(x => x.CodeQuestionId)
                .ToListAsync();

            var questions = await _db.CodeQuestions
                .OrderBy(c => c.Title)
                .Select(c => new ManageQuestionsVm.Item
                {
                    Id = c.Id,
                    Title = c.Title,
                    IsSelected = attached.Contains(c.Id)
                })
                .ToListAsync();

            var vm = new ManageQuestionsVm
            {
                QuizId = id,
                QuizTitle = quiz.Title,
                Items = questions
            };
            return View("~/Areas/Admin/Views/Coding/Quiz/ManageQuestions.cshtml", vm);
        }

        // POST: /Admin/Coding/Quiz/ManageQuestions/{id}
        [HttpPost("ManageQuestions/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageQuestions(Guid id, ManageQuestionsVm form)
        {
            if (id != form.QuizId) return BadRequest();

            // allow single/multiple now; students will see the first by Order
            var selectedIds = form.Items.Where(i => i.IsSelected).Select(i => i.Id).ToList();

            var existing = _db.QuizCodingQuestions.Where(x => x.QuizId == id);
            _db.QuizCodingQuestions.RemoveRange(existing);

            int order = 1;
            foreach (var qid in selectedIds)
            {
                _db.QuizCodingQuestions.Add(new Quiz_Application_College.Domain.Coding.QuizCodingQuestion
                {
                    QuizId = id,
                    CodeQuestionId = qid,
                    Order = order++
                });
            }

            await _db.SaveChangesAsync();
            TempData["Message"] = "Coding questions updated.";
            return RedirectToAction(nameof(Index));
        }

        // VM used by the view
        public class ManageQuestionsVm
        {
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public List<Item> Items { get; set; } = new();

            public class Item
            {
                public Guid Id { get; set; }
                public string Title { get; set; } = "";
                public bool IsSelected { get; set; }
            }
        }
    }
}
