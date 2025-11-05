using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain.Coding;
using System.ComponentModel.DataAnnotations;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CodeQuestionsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CodeQuestionsController> _logger;

        public CodeQuestionsController(ApplicationDbContext db, ILogger<CodeQuestionsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // GET: /Admin/CodeQuestions
        [HttpGet]
        public async Task<IActionResult> Index(string? q)
        {
            var list = await _db.CodeQuestions
                .OrderBy(x => x.Title)
                .Where(x => string.IsNullOrWhiteSpace(q) || x.Title.Contains(q!))
                .ToListAsync();

            ViewBag.Query = q;
            return View(list);
        }

        // GET: /Admin/CodeQuestions/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CodeQuestion
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

        // POST: /Admin/CodeQuestions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,Prompt,MaxMarks,AllowedLanguagesCsv,StarterCodeJson,TestCases")] CodeQuestion model)
        {
            // --- normalize & validate ---
            NormalizeAndFixIds(model);

            model.TestCases = CleanCases(model.TestCases);
            if (!model.TestCases.Any())
                ModelState.AddModelError("", "Add at least one test case (Input or Expected Output).");

            // basic requireds (in case model attributes are missing)
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Title is required.");
            if (model.MaxMarks <= 0)
                ModelState.AddModelError(nameof(model.MaxMarks), "MaxMarks must be greater than 0.");

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                _db.CodeQuestions.Add(model);
                await _db.SaveChangesAsync();
                TempData["Ok"] = "Coding question created.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create CodeQuestion failed");
                ModelState.AddModelError("", "Failed to create the coding question. " + ex.Message);
                return View(model);
            }
        }

        // GET: /Admin/CodeQuestions/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            var q = await _db.CodeQuestions.Include(x => x.TestCases)
                                           .FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            if (q.TestCases == null || q.TestCases.Count == 0)
                q.TestCases = new List<CodeTestCase> { new CodeTestCase { IsHidden = false, Weight = 1 } };

            return View(q);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Title,Prompt,MaxMarks,AllowedLanguagesCsv,StarterCodeJson,TestCases")] CodeQuestion model)
        {
            var q = await _db.CodeQuestions
                             .Include(x => x.TestCases)
                             .FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            // ---- validation on scalars ----
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Title is required.");
            if (model.MaxMarks <= 0)
                ModelState.AddModelError(nameof(model.MaxMarks), "MaxMarks must be greater than 0.");

            // ---- clean incoming cases (build as brand-new rows) ----
            var incoming = (model.TestCases ?? new List<CodeTestCase>())
                .Where(c => !(string.IsNullOrWhiteSpace(c.Input) && string.IsNullOrWhiteSpace(c.ExpectedOutput)))
                .Select(c => new CodeTestCase
                {
                    Id = Guid.NewGuid(),                 // IMPORTANT: always new IDs (we're re-adding)
                    CodeQuestionId = id,                 // set FK explicitly
                    Input = c.Input?.Trim() ?? "",
                    ExpectedOutput = c.ExpectedOutput?.Trim() ?? "",
                    Weight = c.Weight <= 0 ? 1 : c.Weight,
                    IsHidden = c.IsHidden
                })
                .ToList();

            if (!incoming.Any())
                ModelState.AddModelError("", "Add at least one test case.");

            if (!ModelState.IsValid)
                return View(model); // return with validation errors

            // ---- update parent scalars ----
            q.Title = model.Title;
            q.Prompt = model.Prompt;
            q.MaxMarks = model.MaxMarks;
            q.AllowedLanguagesCsv = string.IsNullOrWhiteSpace(model.AllowedLanguagesCsv) ? "python" : model.AllowedLanguagesCsv;
            q.StarterCodeJson = model.StarterCodeJson;

            // ---- replace children: delete old, then add new ----
            _db.CodeTestCases.RemoveRange(q.TestCases);  // mark deletes
                                                         // (No need to SaveChanges here; we can do it in a single commit)
            await _db.CodeTestCases.AddRangeAsync(incoming);  // add new rows
            q.TestCases = incoming; // keep in navigation for UI re-display if needed

            await _db.SaveChangesAsync();

            TempData["Ok"] = "Coding question updated.";
            return RedirectToAction(nameof(Index));
        }


        // POST: /Admin/CodeQuestions/Delete/{id}
        [HttpPost]
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

        private static void NormalizeAndFixIds(CodeQuestion model)
        {
            if (model == null) return;

            if (model.Id == Guid.Empty)
                model.Id = Guid.NewGuid();

            model.AllowedLanguagesCsv = string.IsNullOrWhiteSpace(model.AllowedLanguagesCsv)
                ? "python"
                : model.AllowedLanguagesCsv;

            if (model.TestCases == null)
                model.TestCases = new List<CodeTestCase>();

            foreach (var c in model.TestCases)
            {
                if (c.Id == Guid.Empty)
                    c.Id = Guid.NewGuid();

                // Weight default
                if (c.Weight <= 0) c.Weight = 1;
            }
        }

        private static List<CodeTestCase> CleanCases(ICollection<CodeTestCase>? raw)
        {
            var list = (raw ?? new List<CodeTestCase>())
                .Where(c => !(string.IsNullOrWhiteSpace(c.Input) && string.IsNullOrWhiteSpace(c.ExpectedOutput)))
                .Select(c => new CodeTestCase
                {
                    Id = c.Id == Guid.Empty ? Guid.NewGuid() : c.Id,
                    Input = c.Input?.Trim() ?? "",
                    ExpectedOutput = c.ExpectedOutput?.Trim() ?? "",
                    Weight = c.Weight <= 0 ? 1 : c.Weight,
                    IsHidden = c.IsHidden
                })
                .ToList();
            return list;
        }
    }
}
