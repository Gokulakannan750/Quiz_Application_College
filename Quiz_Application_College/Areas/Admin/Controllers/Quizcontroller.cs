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

        // GET: /Admin/Quiz/Questions/{id}
        [HttpGet]
        public async Task<IActionResult> Questions(Guid id)
        {
            var quiz = await _db.Quizzes.FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound();

            // Current links (questionId -> order)
            var current = await _db.QuizQuestions
                .Where(qq => qq.QuizId == id)
                .Select(qq => new { qq.QuestionId, qq.Order })
                .ToListAsync();
            var currentMap = current.ToDictionary(x => x.QuestionId, x => (int?)x.Order);

            // IMPORTANT: pull only from McqQuestions (NO join to options).
            // This guarantees one row per question.
            var list = await _db.McqQuestions
                .AsNoTracking()
                .OrderBy(q => q.Text)
                .Select(q => new QuestionSelectVm
                {
                    QuestionId = q.Id,
                    Text = q.Text,
                    Selected = currentMap.ContainsKey(q.Id),
                    Order = currentMap.ContainsKey(q.Id) ? currentMap[q.Id] : null
                })
                .ToListAsync();

            ViewBag.QuizId = id;
            ViewBag.QuizTitle = quiz.Title;
            return View(list);
        }

        // POST: /Admin/Quiz/Questions/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Questions(Guid id, Guid[] selectedIds)
        {
            var quiz = await _db.Quizzes.FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound();

            // Normalize the incoming set (distinct)
            var selected = selectedIds?.Distinct().ToHashSet() ?? new HashSet<Guid>();

            // Existing links for this quiz
            var existing = await _db.QuizQuestions
                .Where(qq => qq.QuizId == id)
                .ToListAsync();

            var existingIds = existing.Select(e => e.QuestionId).ToHashSet();

            // Add new links
            var toAdd = selected.Except(existingIds).ToList();
            if (toAdd.Count > 0)
            {
                // Determine next order index
                var nextOrder = existing.Count == 0 ? 1 : existing.Max(e => e.Order) + 1;
                foreach (var qid in toAdd)
                {
                    _db.QuizQuestions.Add(new Domain.QuizQuestion
                    {
                        QuizId = id,
                        QuestionId = qid,
                        Order = nextOrder++
                    });
                }
            }

            // Remove unselected links
            var toRemove = existing.Where(e => !selected.Contains(e.QuestionId)).ToList();
            if (toRemove.Count > 0)
                _db.QuizQuestions.RemoveRange(toRemove);

            await _db.SaveChangesAsync();

            TempData["Info"] = "Questions updated.";
            return RedirectToAction(nameof(Questions), new { id });
        }
    }
}
