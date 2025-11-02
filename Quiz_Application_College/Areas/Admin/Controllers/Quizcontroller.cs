using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _db;

        public QuizController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/Quiz
        public async Task<IActionResult> Index()
        {
            var list = await _db.Quizzes
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
            return View(list);
        }

        // GET: /Admin/Quiz/Create
        public IActionResult Create() => View(new QuizCreateVm());

        // POST: /Admin/Quiz/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(QuizCreateVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var quiz = new Quiz_Application_College.Domain.Quiz
            {
                Title = vm.Title,
                Description = vm.Description,
                DurationMinutes = vm.DurationMinutes,
                TotalMarks = vm.TotalMarks,
                EnableNegativeMarking = vm.EnableNegativeMarking,
                NegativeMarkPerWrong = vm.NegativeMarkPerWrong,

                // NEW toggles
                ShuffleQuestions = vm.ShuffleQuestions,
                ShuffleOptions = vm.ShuffleOptions,
                ShowReviewOnSubmit = vm.ShowReviewOnSubmit,
                ShowScoreOnSubmit = vm.ShowScoreOnSubmit,

                IsPublished = false, // or your default
                CreatedAt = DateTimeOffset.Now
            };
            _db.Quizzes.Add(quiz);
            await _db.SaveChangesAsync();


            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/Quiz/Assign/{id}
        public async Task<IActionResult> Assign(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            var qlist = await _db.McqQuestions
                .OrderBy(x => x.Text)
                .Select(x => new { x.Id, x.Text })
                .ToListAsync();

            var selected = await _db.QuizQuestions
                .Where(qq => qq.QuizId == id)
                .Select(qq => qq.QuestionId)
                .ToListAsync();

            ViewBag.Quiz = quiz;
            ViewBag.AllQuestions = qlist;
            ViewBag.Selected = selected;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(Guid id, Guid[] questionIds)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();

            var existing = await _db.QuizQuestions.Where(qq => qq.QuizId == id).ToListAsync();
            _db.QuizQuestions.RemoveRange(existing);

            int order = 1;
            foreach (var qid in questionIds.Distinct())
                _db.QuizQuestions.Add(new QuizQuestion { QuizId = id, QuestionId = qid, Order = order++ });

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
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

        [HttpPost]
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

        public async Task<IActionResult> Edit(Guid id)
        {
            var quiz = await _db.Quizzes.FindAsync(id);
            if (quiz == null) return NotFound();
            return View(quiz);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Quiz_Application_College.Domain.Quiz quiz)
        {
            if (id != quiz.Id) return BadRequest();
            if (!ModelState.IsValid) return View(quiz);

            var dbq = await _db.Quizzes.FindAsync(id);
            if (dbq == null) return NotFound();

            // update fields you allow editing
            dbq.Title = quiz.Title;
            dbq.Description = quiz.Description;
            dbq.DurationMinutes = quiz.DurationMinutes;
            dbq.TotalMarks = quiz.TotalMarks;
            dbq.EnableNegativeMarking = quiz.EnableNegativeMarking;
            dbq.NegativeMarkPerWrong = quiz.NegativeMarkPerWrong;

            // new toggles
            dbq.ShuffleQuestions = quiz.ShuffleQuestions;
            dbq.ShuffleOptions = quiz.ShuffleOptions;
            dbq.ShowReviewOnSubmit = quiz.ShowReviewOnSubmit;
            dbq.ShowScoreOnSubmit = quiz.ShowScoreOnSubmit;

            dbq.UpdatedAt = DateTimeOffset.Now;

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


    }
}
