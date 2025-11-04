using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
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
    public class QuestionBankController : Controller
    {
        private readonly ApplicationDbContext _db;
        public QuestionBankController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var list = await _db.McqQuestions
                .Include(q => q.Options)
                .OrderByDescending(x => x.Id)
                .ToListAsync();
            return View(list);
        }

        public IActionResult Create() => View(new McqCreateVm());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(McqCreateVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (vm.Options.Count(o => !string.IsNullOrWhiteSpace(o)) < 2)
                ModelState.AddModelError("", "Provide at least two options.");

            if (vm.CorrectIndex is null || vm.CorrectIndex < 0 || vm.CorrectIndex > 3)
                ModelState.AddModelError(nameof(vm.CorrectIndex), "Select the correct option.");

            if (!ModelState.IsValid) return View(vm);

            var q = new McqQuestion { Text = vm.Text.Trim(), Marks = vm.Marks, Tag = vm.Tag?.Trim() };

            for (int i = 0; i < vm.Options.Length; i++)
            {
                var text = vm.Options[i]?.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                q.Options.Add(new McqOption
                {
                    Text = text,
                    IsCorrect = (vm.CorrectIndex == i)
                });
            }

            _db.McqQuestions.Add(q);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/QuestionBank/Edit/{id}
        public async Task<IActionResult> Edit(Guid id)
        {
            var q = await _db.McqQuestions
                .Include(x => x.Options)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            var vm = new McqEditVm
            {
                Id = q.Id,
                Text = q.Text,
                Marks = q.Marks,
                Options = q.Options
                    .OrderBy(o => o.Id) // simple stable order
                    .Select(o => new McqEditVm.OptionVm
                    {
                        Id = o.Id,
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList()
            };

            return View(vm);
        }

        // POST: /Admin/QuestionBank/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, McqEditVm vm)
        {
            if (id != vm.Id) return BadRequest();
            if (!ModelState.IsValid) return View(vm);

            // Ensure exactly one correct option
            if (vm.Options.Count(o => o.IsCorrect) != 1)
            {
                ModelState.AddModelError("", "Please mark exactly one option as correct.");
                return View(vm);
            }

            var q = await _db.McqQuestions
                .Include(x => x.Options)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            // Update question
            q.Text = vm.Text;
            q.Marks = vm.Marks;

            // Sync options: update existing, add new, remove deleted
            var existing = q.Options.ToDictionary(o => o.Id, o => o);

            // mark all existing as unseen
            var seen = new HashSet<Guid>();

            foreach (var optVm in vm.Options)
            {
                if (optVm.Id.HasValue && existing.TryGetValue(optVm.Id.Value, out var opt))
                {
                    // update existing
                    opt.Text = optVm.Text;
                    opt.IsCorrect = optVm.IsCorrect;
                    seen.Add(opt.Id);
                }
                else
                {
                    // add new
                    var newOpt = new McqOption
                    {
                        QuestionId = q.Id,
                        Text = optVm.Text,
                        IsCorrect = optVm.IsCorrect
                    };
                    _db.McqOptions.Add(newOpt);
                }
            }

            // delete removed options
            var toRemove = q.Options.Where(o => !seen.Contains(o.Id)).ToList();
            if (toRemove.Any())
                _db.McqOptions.RemoveRange(toRemove);

            q.NormalizedText = BuildNormalized(q.Text);

            await _db.SaveChangesAsync();
            TempData["Info"] = "Question updated.";
            return RedirectToAction(nameof(Index)); // back to question list
        }

        // POST: /Admin/QuestionBank/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var q = await _db.McqQuestions
                .Include(x => x.Options)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            // remove mappings to quizzes first (avoid FK errors)
            var links = await _db.QuizQuestions.Where(qq => qq.QuestionId == id).ToListAsync();
            _db.QuizQuestions.RemoveRange(links);

            // remove options then question
            _db.McqOptions.RemoveRange(q.Options);
            _db.McqQuestions.Remove(q);

            await _db.SaveChangesAsync();
            TempData["Info"] = "Question deleted.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/QuestionBank/Import
        [HttpGet]
        public async Task<IActionResult> Import()
        {
            var vm = new QuestionImportVm
            {
                Quizzes = await _db.Quizzes
                    .OrderBy(q => q.Title)
                    .Select(q => new SelectListItem { Value = q.Id.ToString(), Text = q.Title })
                    .ToListAsync()
            };
            return View(vm);
        }

        // POST: /Admin/QuestionBank/Import
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(QuestionImportVm vm, IFormFile file)
        {
            // re-populate quizzes for redisplay on error/success
            vm.Quizzes = await _db.Quizzes
                .OrderBy(q => q.Title)
                .Select(q => new SelectListItem { Value = q.Id.ToString(), Text = q.Title })
                .ToListAsync();

            if (file == null || file.Length == 0)
            {
                vm.Errors.Add("Please choose an .xlsx file.");
                return View(vm);
            }
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                vm.Errors.Add("Only .xlsx files are supported.");
                return View(vm);
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;

            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheets.First();

            int row = 2; // header at row 1
            int nextOrderCounter = 0;

            // If we are assigning to a quiz, pre-compute the next order
            if (vm.AssignToQuiz && vm.QuizId.HasValue)
            {
                nextOrderCounter = await _db.QuizQuestions
                    .Where(qq => qq.QuizId == vm.QuizId.Value)
                    .Select(qq => (int?)qq.Order)
                    .MaxAsync() ?? 0;
            }

            while (true)
            {
                var text = ws.Cell(row, 1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(text)) break; // stop on first blank row

                vm.TotalRows++;

                var marksStr = ws.Cell(row, 2).GetString().Trim();
                var optA = ws.Cell(row, 3).GetString().Trim();
                var optB = ws.Cell(row, 4).GetString().Trim();
                var optC = ws.Cell(row, 5).GetString().Trim();
                var optD = ws.Cell(row, 6).GetString().Trim();
                var correct = ws.Cell(row, 7).GetString().Trim();
                var tag = ws.Cell(row, 8).GetString().Trim();

                if (!decimal.TryParse(marksStr, out var marks) || marks < 0)
                {
                    vm.Errors.Add($"Row {row}: Invalid Marks '{marksStr}'.");
                    vm.Skipped++; row++; continue;
                }

                var options = new List<(string Text, bool IsCorrect)>
                {
                    (optA, string.Equals(correct, "A", StringComparison.OrdinalIgnoreCase)),
                    (optB, string.Equals(correct, "B", StringComparison.OrdinalIgnoreCase)),
                    (optC, string.Equals(correct, "C", StringComparison.OrdinalIgnoreCase)),
                    (optD, string.Equals(correct, "D", StringComparison.OrdinalIgnoreCase)),
                };

                if (options.All(o => string.IsNullOrWhiteSpace(o.Text)))
                {
                    vm.Errors.Add($"Row {row}: All options are empty.");
                    vm.Skipped++; row++; continue;
                }
                if (options.Count(o => o.IsCorrect) != 1)
                {
                    vm.Errors.Add($"Row {row}: 'Correct' must be exactly one of A, B, C, or D.");
                    vm.Skipped++; row++; continue;
                }
                if (options.Any(o => o.IsCorrect && string.IsNullOrWhiteSpace(o.Text)))
                {
                    vm.Errors.Add($"Row {row}: Correct option selected but its text is empty.");
                    vm.Skipped++; row++; continue;
                }

                // --- DEDUPE: normalize and check existing ---
                var norm = BuildNormalized(text);

                // Try find existing by normalized text
                var existingQ = await _db.McqQuestions
                    .FirstOrDefaultAsync(x => x.NormalizedText == norm);

                if (existingQ != null)
                {
                    // Already in DB -> skip creating another
                    vm.Skipped++;
                    vm.Errors.Add($"Row {row}: Duplicate skipped (already exists).");

                    // If admin selected "Assign to quiz", link the existing question
                    if (vm.AssignToQuiz && vm.QuizId.HasValue)
                    {
                        bool linked = await _db.QuizQuestions
                            .AnyAsync(qq => qq.QuizId == vm.QuizId.Value && qq.QuestionId == existingQ.Id);

                        if (!linked)
                        {
                            // append to end
                            var nextOrder = await _db.QuizQuestions
                                .Where(qq => qq.QuizId == vm.QuizId.Value)
                                .Select(qq => (int?)qq.Order).MaxAsync() ?? 0;

                            _db.QuizQuestions.Add(new Domain.QuizQuestion
                            {
                                QuizId = vm.QuizId.Value,
                                QuestionId = existingQ.Id,
                                Order = nextOrder + 1
                            });
                            await _db.SaveChangesAsync();
                        }
                    }

                    row++;
                    continue; // go next row
                }

                // Not found -> create new
                var q = new McqQuestion
                {
                    Text = text,
                    Marks = marks,
                    Tag = string.IsNullOrWhiteSpace(tag) ? null : tag,
                    NormalizedText = norm // <<< store normalized text
                };
                _db.McqQuestions.Add(q);
                await _db.SaveChangesAsync(); // need q.Id


                // Create options (skip blanks)
                foreach (var (txt, isCorrect) in options)
                {
                    if (string.IsNullOrWhiteSpace(txt)) continue;
                    _db.McqOptions.Add(new McqOption
                    {
                        QuestionId = q.Id,
                        Text = txt,
                        IsCorrect = isCorrect
                    });
                }
                await _db.SaveChangesAsync();

                // If admin selected "Assign to quiz", link the existing question
                if (vm.AssignToQuiz && vm.QuizId.HasValue)
                {
                    bool linked = await _db.QuizQuestions
                        .AnyAsync(qq => qq.QuizId == vm.QuizId.Value && qq.QuestionId == existingQ.Id);

                    if (!linked)
                    {
                        nextOrderCounter++; // ✅ increment shared counter
                        _db.QuizQuestions.Add(new Domain.QuizQuestion
                        {
                            QuizId = vm.QuizId.Value,
                            QuestionId = existingQ.Id,
                            Order = nextOrderCounter
                        });
                        await _db.SaveChangesAsync();
                    }
                }


                vm.Inserted++;
                row++;
            }

            vm.FileName = file.FileName;
            if (vm.TotalRows == 0 && vm.Errors.Count == 0)
                vm.Errors.Add("No data rows found. Keep headers in row 1 and start data at row 2.");

            return View(vm);
        }
    

        // GET: /Admin/QuestionBank/Template
        [HttpGet]
        public IActionResult Template()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Questions");
            // Header
            ws.Cell(1, 1).Value = "Text";
            ws.Cell(1, 2).Value = "Marks";
            ws.Cell(1, 3).Value = "OptionA";
            ws.Cell(1, 4).Value = "OptionB";
            ws.Cell(1, 5).Value = "OptionC";
            ws.Cell(1, 6).Value = "OptionD";
            ws.Cell(1, 7).Value = "Correct (A-D)";
            ws.Cell(1, 8).Value = "Tag";

            // Example row
            ws.Cell(2, 1).Value = "What is the capital of France?";
            ws.Cell(2, 2).Value = 1;
            ws.Cell(2, 3).Value = "Paris";
            ws.Cell(2, 4).Value = "Berlin";
            ws.Cell(2, 5).Value = "Madrid";
            ws.Cell(2, 6).Value = "Rome";
            ws.Cell(2, 7).Value = "A";
            ws.Cell(2, 8).Value = "Geography";

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var bytes = ms.ToArray();
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "QuestionImportTemplate.xlsx");
        }

        private static string BuildNormalized(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Lowercase
            var s = input.ToLowerInvariant();

            // Replace CR/LF/Tabs with spaces
            s = s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

            // Collapse multiple spaces
            while (s.Contains("  ")) s = s.Replace("  ", " ");

            // Trim
            return s.Trim();
        }

    }

}
    
