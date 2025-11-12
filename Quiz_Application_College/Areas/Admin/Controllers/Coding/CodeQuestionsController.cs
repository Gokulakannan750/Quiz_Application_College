using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain.Coding;

namespace Quiz_Application_College.Areas.Admin.Controllers.Coding
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    [Route("Admin/Coding/CodeQuestions")]
    public class CodeQuestionsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CodeQuestionsController> _logger;

        public CodeQuestionsController(ApplicationDbContext db, ILogger<CodeQuestionsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: /Admin/Coding/CodeQuestions
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? q)
        {
            var list = await _db.CodeQuestions
                .Include(x => x.TestCases)
                .Where(x => string.IsNullOrWhiteSpace(q) || x.Title.Contains(q!))
                .OrderBy(x => x.Title)
                .ToListAsync();

            ViewBag.Query = q;
            return View("~/Areas/Admin/Views/Coding/CodeQuestions/Index.cshtml", list);
        }

        // GET: /Admin/Coding/CodeQuestions/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View("~/Areas/Admin/Views/Coding/CodeQuestions/Create.cshtml",
                new CodeQuestion
                {
                    AllowedLanguagesCsv = "python,csharp",
                    MaxMarks = 10m,
                    TestCases = new List<CodeTestCase>
                    {
                        new CodeTestCase { IsHidden = false, Weight = 1 },
                        new CodeTestCase { IsHidden = true,  Weight = 1 }
                    }
                });
        }

        // POST: /Admin/Coding/CodeQuestions/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Questions,MaxMarks,AllowedLanguagesCsv,StarterCodeJson,TestCases")] CodeQuestion model)
        {
            Normalize(model);
            var cleaned = CleanCases(model.TestCases);

            if (!cleaned.Any())
                ModelState.AddModelError("", "Add at least one test case (Input or Expected Output).");
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Title is required.");
            if (model.MaxMarks <= 0)
                ModelState.AddModelError(nameof(model.MaxMarks), "MaxMarks must be greater than 0.");

            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/Coding/CodeQuestions/Create.cshtml", model);

            try
            {
                model.TestCases = cleaned; // <-- includes IsHidden
                _db.CodeQuestions.Add(model);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Coding question created.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create CodeQuestion failed");
                ModelState.AddModelError("", "Failed to create the coding question. " + ex.Message);
                return View("~/Areas/Admin/Views/Coding/CodeQuestions/Create.cshtml", model);
            }
        }

        // GET: /Admin/Coding/CodeQuestions/Edit/{id}
        [HttpGet("Edit/{id:guid}")]
        public async Task<IActionResult> Edit(Guid id)
        {
            var q = await _db.CodeQuestions
                             .Include(x => x.TestCases)
                             .FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            if (q.TestCases == null || q.TestCases.Count == 0)
                q.TestCases = new List<CodeTestCase> { new CodeTestCase { IsHidden = false, Weight = 1 } };

            return View("~/Areas/Admin/Views/Coding/CodeQuestions/Edit.cshtml", q);
        }

        // POST: /Admin/Coding/CodeQuestions/Edit/{id}
        [HttpPost("Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Title,Questions,MaxMarks,AllowedLanguagesCsv,StarterCodeJson,TestCases")] CodeQuestion model)
        {
            var existing = await _db.CodeQuestions.Include(x => x.TestCases)
                                                  .FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return NotFound();

            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Title is required.");
            if (model.MaxMarks <= 0)
                ModelState.AddModelError(nameof(model.MaxMarks), "MaxMarks must be greater than 0.");

            var incoming = CleanCases(model.TestCases)
                .Select(c => new CodeTestCase
                {
                    Id = Guid.NewGuid(),               // replace children to simplify updates
                    CodeQuestionId = id,
                    Input = c.Input,
                    ExpectedOutput = c.ExpectedOutput,
                    Weight = c.Weight <= 0 ? 1 : c.Weight,
                    IsHidden = c.IsHidden              // <-- preserve IsHidden
                })
                .ToList();

            if (!incoming.Any())
                ModelState.AddModelError("", "Add at least one test case.");

            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/Coding/CodeQuestions/Edit.cshtml", model);

            existing.Title = model.Title;
            existing.Questions = model.Questions;
            existing.MaxMarks = model.MaxMarks;
            existing.AllowedLanguagesCsv = string.IsNullOrWhiteSpace(model.AllowedLanguagesCsv) ? "python" : model.AllowedLanguagesCsv;
            existing.StarterCodeJson = model.StarterCodeJson;

            // Replace children (ensures IsHidden is saved)
            _db.CodeTestCases.RemoveRange(existing.TestCases);
            await _db.CodeTestCases.AddRangeAsync(incoming);
            existing.TestCases = incoming;

            await _db.SaveChangesAsync();
            TempData["Ok"] = "Coding question updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/Coding/CodeQuestions/Delete/{id}
        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var q = await _db.CodeQuestions.FirstOrDefaultAsync(x => x.Id == id);
            if (q != null)
            {
                _db.CodeQuestions.Remove(q);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ---------- helpers ----------
        private static void Normalize(CodeQuestion model)
        {
            if (model.Id == Guid.Empty)
                model.Id = Guid.NewGuid();

            model.AllowedLanguagesCsv = string.IsNullOrWhiteSpace(model.AllowedLanguagesCsv)
                ? "python"
                : model.AllowedLanguagesCsv;

            if (model.TestCases == null)
                model.TestCases = new List<CodeTestCase>();

            foreach (var c in model.TestCases)
            {
                if (c.Id == Guid.Empty) c.Id = Guid.NewGuid();
                if (c.Weight <= 0) c.Weight = 1;
                c.Input = c.Input?.Trim() ?? "";
                c.ExpectedOutput = c.ExpectedOutput?.Trim() ?? "";
                // DO NOT touch c.IsHidden here; let the posted value stand
            }
        }

        private static List<CodeTestCase> CleanCases(ICollection<CodeTestCase>? raw)
        {
            // Keep rows that have either Input or ExpectedOutput
            return (raw ?? new List<CodeTestCase>())
                .Where(c => !(string.IsNullOrWhiteSpace(c.Input) && string.IsNullOrWhiteSpace(c.ExpectedOutput)))
                .Select(c => new CodeTestCase
                {
                    Id = c.Id == Guid.Empty ? Guid.NewGuid() : c.Id,
                    Input = c.Input?.Trim() ?? "",
                    ExpectedOutput = c.ExpectedOutput?.Trim() ?? "",
                    Weight = c.Weight <= 0 ? 1 : c.Weight,
                    IsHidden = c.IsHidden // <-- CRITICAL: preserve posted hidden flag
                })
                .ToList();
        }
    }
}
