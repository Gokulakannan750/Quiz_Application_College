using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain.Coding;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class QuizCodingController : Controller
    {
        private readonly ApplicationDbContext _db;
        public QuizCodingController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/QuizCoding/Manage/{quizId}
        [HttpGet]
        public async Task<IActionResult> Manage(Guid quizId)
        {
            var quiz = await _db.Quizzes.FirstOrDefaultAsync(x => x.Id == quizId);
            if (quiz == null) return NotFound();

            var attached = await _db.QuizCodingQuestions
                .Include(x => x.CodeQuestion)
                .Where(x => x.QuizId == quizId)
                .OrderBy(x => x.Order)
                .ToListAsync();

            var all = await _db.CodeQuestions.OrderBy(x => x.Title).ToListAsync();

            return View(new Vm
            {
                QuizId = quizId,
                QuizTitle = quiz.Title,
                Attached = attached,
                All = all
            });
        }

        // POST: /Admin/QuizCoding/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Guid quizId, Guid codeQuestionId, int order = 0)
        {
            var exists = await _db.QuizCodingQuestions.AnyAsync(x => x.QuizId == quizId && x.CodeQuestionId == codeQuestionId);
            if (!exists)
            {
                _db.QuizCodingQuestions.Add(new QuizCodingQuestion { QuizId = quizId, CodeQuestionId = codeQuestionId, Order = order });
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Manage), new { quizId });
        }

        // POST: /Admin/QuizCoding/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(Guid quizId, Guid codeQuestionId)
        {
            var link = await _db.QuizCodingQuestions.FirstOrDefaultAsync(x => x.QuizId == quizId && x.CodeQuestionId == codeQuestionId);
            if (link != null)
            {
                _db.QuizCodingQuestions.Remove(link);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Manage), new { quizId });
        }

        // POST: /Admin/QuizCoding/Sort  (order by posted ID sequence)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sort(Guid quizId, List<Guid> orderedIds)
        {
            var rows = await _db.QuizCodingQuestions.Where(x => x.QuizId == quizId).ToListAsync();
            int i = 0;
            foreach (var id in orderedIds)
            {
                var row = rows.FirstOrDefault(x => x.CodeQuestionId == id);
                if (row != null) row.Order = i++;
            }
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Manage), new { quizId });
        }

        public class Vm
        {
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public List<QuizCodingQuestion> Attached { get; set; } = new();
            public List<CodeQuestion> All { get; set; } = new();
        }
    }
}
