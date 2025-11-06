using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain.Coding;

namespace Quiz_Application_College.Areas.Admin.Controllers.Coding
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class QuizCodingController : Controller
    {
        private readonly ApplicationDbContext _db;

        public QuizCodingController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /Admin/QuizCoding/Manage?quizId=...
        [HttpGet]
        public async Task<IActionResult> Manage(Guid quizId)
        {
            var quiz = await _db.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null) return NotFound();

            var attached = await _db.QuizCodingQuestions
                .Where(x => x.QuizId == quizId)
                .Join(_db.CodeQuestions,
                      link => link.CodeQuestionId,
                      cq => cq.Id,
                      (link, cq) => new AttachedVm
                      {
                          CodeQuestionId = cq.Id,
                          Title = cq.Title,
                          Order = link.Order
                      })
                .OrderBy(x => x.Order)
                .ToListAsync();

            var attachedIds = attached.Select(a => a.CodeQuestionId).ToHashSet();

            var available = await _db.CodeQuestions
                .Where(cq => !attachedIds.Contains(cq.Id))
                .OrderBy(cq => cq.Title)
                .ToListAsync();

            var vm = new ManageVm
            {
                QuizId = quizId,
                QuizTitle = quiz.Title,
                Available = available,
                Attached = attached
            };
            return View("~/Areas/Admin/Views/Coding/Quiz/Index.cshtml");
        }

        // POST: /Admin/QuizCoding/Attach
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Attach(Guid quizId, Guid codeQuestionId, int order = 0)
        {
            var exists = await _db.QuizCodingQuestions
                .AnyAsync(x => x.QuizId == quizId && x.CodeQuestionId == codeQuestionId);
            if (!exists)
            {
                _db.QuizCodingQuestions.Add(new QuizCodingQuestion
                {
                    QuizId = quizId,
                    CodeQuestionId = codeQuestionId,
                    Order = order
                });
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Coding question attached.";
            }
            return RedirectToAction(nameof(Manage), new { quizId });
        }

        // POST: /Admin/QuizCoding/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(Guid quizId, Guid codeQuestionId)
        {
            var link = await _db.QuizCodingQuestions
                .FirstOrDefaultAsync(x => x.QuizId == quizId && x.CodeQuestionId == codeQuestionId);
            if (link != null)
            {
                _db.QuizCodingQuestions.Remove(link);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Removed.";
            }
            return RedirectToAction(nameof(Manage), new { quizId });
        }

        // POST: /Admin/QuizCoding/SaveOrder
        // expects: quizId + items[i].CodeQuestionId + items[i].Order
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOrder(Guid quizId, List<OrderItemDto> items)
        {
            if (quizId == Guid.Empty) return BadRequest("quizId missing.");

            var links = await _db.QuizCodingQuestions
                .Where(x => x.QuizId == quizId)
                .ToListAsync();

            foreach (var link in links)
            {
                var match = items.FirstOrDefault(i => i.CodeQuestionId == link.CodeQuestionId);
                if (match != null)
                    link.Order = match.Order;
            }

            await _db.SaveChangesAsync();
            TempData["Ok"] = "Order saved.";
            return RedirectToAction(nameof(Manage), new { quizId });
        }

        public class OrderItemDto
        {
            public Guid CodeQuestionId { get; set; }
            public int Order { get; set; }
        }

        public class ManageVm
        {
            public Guid QuizId { get; set; }
            public string QuizTitle { get; set; } = "";
            public List<CodeQuestion> Available { get; set; } = new();
            public List<AttachedVm> Attached { get; set; } = new();
        }

        public class AttachedVm
        {
            public Guid CodeQuestionId { get; set; }
            public string Title { get; set; } = "";
            public int Order { get; set; }
        }
    }
}
