using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.ViewModels;

namespace Quiz_Application_College.Areas.Admin.Controllers.Mcq
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
    // Clean, fixed path => /Admin/MCQ/QuestionBank/...
    [Route("Admin/MCQ/QuestionBank")]
    public class McqQuestionBankController : Controller
    {
        private readonly ApplicationDbContext _db;
        public McqQuestionBankController(ApplicationDbContext db) => _db = db;

        // GET: /Admin/MCQ/QuestionBank  and /Admin/MCQ/QuestionBank/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var list = await _db.McqQuestions
                .Include(q => q.Options)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            return View("~/Areas/Admin/Views/Mcq/QuestionBank/Index.cshtml", list);
        }

        // GET: /Admin/MCQ/QuestionBank/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View("~/Areas/Admin/Views/Mcq/QuestionBank/Create.cshtml", new McqCreateVm());
        }

        // POST: /Admin/MCQ/QuestionBank/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(McqCreateVm vm)
        {
            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/Mcq/QuestionBank/Create.cshtml", vm);

            if (vm.Options == null || vm.Options.Count(o => !string.IsNullOrWhiteSpace(o)) < 2)
                ModelState.AddModelError("", "Provide at least two options.");

            if (vm.CorrectIndex is null || vm.CorrectIndex < 0 || vm.CorrectIndex > vm.Options.Length - 1)
                ModelState.AddModelError(nameof(vm.CorrectIndex), "Select the correct option.");

            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/Mcq/QuestionBank/Create.cshtml", vm);

            var q = new McqQuestion
            {
                Text = vm.Text.Trim(),
                Marks = vm.Marks,
                Tag = string.IsNullOrWhiteSpace(vm.Tag) ? null : vm.Tag.Trim(),
                NormalizedText = BuildNormalized(vm.Text),
                // manual questions have no import file – leave null
                SourceFileName = null
            };

            for (int i = 0; i < vm.Options.Length; i++)
            {
                var text = vm.Options[i]?.Trim();
                if (string.IsNullOrWhiteSpace(text)) continue;

                q.Options.Add(new McqOption
                {
                    Text = text,
                    IsCorrect = vm.CorrectIndex == i
                });
            }

            _db.McqQuestions.Add(q);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /Admin/MCQ/QuestionBank/Edit/{id}
        [HttpGet("Edit/{id:guid}")]
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
                    .OrderBy(o => o.Id)
                    .Select(o => new McqEditVm.OptionVm
                    {
                        Id = o.Id,
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList()
            };

            return View("~/Areas/Admin/Views/Mcq/QuestionBank/Edit.cshtml", vm);
        }

        // POST: /Admin/MCQ/QuestionBank/Edit/{id}
        [HttpPost("Edit/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, McqEditVm vm)
        {
            if (id != vm.Id) return BadRequest();
            if (!ModelState.IsValid)
                return View("~/Areas/Admin/Views/Mcq/QuestionBank/Edit.cshtml", vm);

            if (vm.Options == null || vm.Options.Count(o => o.IsCorrect) != 1)
            {
                ModelState.AddModelError("", "Please mark exactly one option as correct.");
                return View("~/Areas/Admin/Views/Mcq/QuestionBank/Edit.cshtml", vm);
            }

            var q = await _db.McqQuestions
                .Include(x => x.Options)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            q.Text = vm.Text;
            q.Marks = vm.Marks;
            q.NormalizedText = BuildNormalized(vm.Text);
            // keep SourceFileName as is

            var existing = q.Options.ToDictionary(o => o.Id, o => o);
            var seen = new HashSet<Guid>();

            foreach (var optVm in vm.Options)
            {
                if (optVm.Id.HasValue && existing.TryGetValue(optVm.Id.Value, out var opt))
                {
                    opt.Text = optVm.Text;
                    opt.IsCorrect = optVm.IsCorrect;
                    seen.Add(opt.Id);
                }
                else
                {
                    var newOpt = new McqOption
                    {
                        QuestionId = q.Id,
                        Text = optVm.Text,
                        IsCorrect = optVm.IsCorrect
                    };
                    _db.McqOptions.Add(newOpt);
                }
            }

            var toRemove = q.Options.Where(o => !seen.Contains(o.Id)).ToList();
            if (toRemove.Any())
                _db.McqOptions.RemoveRange(toRemove);

            await _db.SaveChangesAsync();

            // 🔁 Recalculate totals for all quizzes using this question
            var affectedQuizIds = await _db.QuizQuestions
                .Where(qq => qq.QuestionId == q.Id)
                .Select(qq => qq.QuizId)
                .Distinct()
                .ToListAsync();

            await RecalculateTotalsForQuizIdsAsync(affectedQuizIds);

            TempData["Info"] = "Question updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/MCQ/QuestionBank/Delete/{id}
        [HttpPost("Delete/{id:guid}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var q = await _db.McqQuestions
                .Include(x => x.Options)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (q == null) return NotFound();

            // quizzes that currently use this question
            var links = await _db.QuizQuestions
                .Where(qq => qq.QuestionId == id)
                .ToListAsync();
            var affectedQuizIds = links.Select(l => l.QuizId).Distinct().ToList();

            _db.QuizQuestions.RemoveRange(links);
            _db.McqOptions.RemoveRange(q.Options);
            _db.McqQuestions.Remove(q);

            await _db.SaveChangesAsync();

            // 🔁 recalc totals for affected quizzes (now without this question)
            await RecalculateTotalsForQuizIdsAsync(affectedQuizIds);

            TempData["Info"] = "Question deleted.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Admin/MCQ/QuestionBank/Import
        [HttpPost("Import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(QuestionImportVm vm, IFormFile file)
        {
            vm.Quizzes = await _db.Quizzes
                .OrderBy(q => q.Title)
                .Select(q => new SelectListItem { Value = q.Id.ToString(), Text = q.Title })
                .ToListAsync();

            if (file == null || file.Length == 0)
            {
                vm.Errors.Add("Please choose an .xlsx file.");
                return View("~/Areas/Admin/Views/Mcq/QuestionBank/Import.cshtml", vm);
            }
            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                vm.Errors.Add("Only .xlsx files are supported.");
                return View("~/Areas/Admin/Views/Mcq/QuestionBank/Import.cshtml", vm);
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;

            using var wb = new ClosedXML.Excel.XLWorkbook(ms);
            var ws = wb.Worksheets.First();

            int row = 2; // assume headers at row 1
            int nextOrderCounter = 0;

            if (vm.AssignToQuiz && vm.QuizId.HasValue)
            {
                nextOrderCounter = await _db.QuizQuestions
                    .Where(qq => qq.QuizId == vm.QuizId.Value)
                    .Select(qq => (int?)qq.Order).MaxAsync() ?? 0;
            }

            while (true)
            {
                var text = ws.Cell(row, 1).GetString().Trim();
                if (string.IsNullOrWhiteSpace(text)) break;

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

                var norm = BuildNormalized(text);

                var dup = await _db.McqQuestions.FirstOrDefaultAsync(x => x.NormalizedText == norm);
                McqQuestion qEntity;

                if (dup != null)
                {
                    vm.Skipped++;
                    vm.Errors.Add($"Row {row}: Duplicate skipped (already exists).");

                    qEntity = dup;
                    // update origin file name if not already set
                    if (string.IsNullOrEmpty(qEntity.SourceFileName))
                    {
                        qEntity.SourceFileName = file.FileName;
                        await _db.SaveChangesAsync();
                    }
                }
                else
                {
                    qEntity = new McqQuestion
                    {
                        Text = text,
                        Marks = marks,
                        Tag = string.IsNullOrWhiteSpace(tag) ? null : tag,
                        NormalizedText = norm,
                        SourceFileName = file.FileName      // 🔴 store Excel file name here
                    };
                    _db.McqQuestions.Add(qEntity);
                    await _db.SaveChangesAsync();

                    foreach (var (txt, isCorrect) in options)
                    {
                        if (string.IsNullOrWhiteSpace(txt)) continue;
                        _db.McqOptions.Add(new McqOption
                        {
                            QuestionId = qEntity.Id,
                            Text = txt,
                            IsCorrect = isCorrect
                        });
                    }
                    await _db.SaveChangesAsync();

                    vm.Inserted++;
                }

                if (vm.AssignToQuiz && vm.QuizId.HasValue)
                {
                    bool linked = await _db.QuizQuestions
                        .AnyAsync(qq => qq.QuizId == vm.QuizId.Value && qq.QuestionId == qEntity.Id);

                    if (!linked)
                    {
                        nextOrderCounter++;
                        _db.QuizQuestions.Add(new QuizQuestion
                        {
                            QuizId = vm.QuizId.Value,
                            QuestionId = qEntity.Id,
                            Order = nextOrderCounter
                        });
                        await _db.SaveChangesAsync();
                    }
                }

                row++;
            }

            vm.FileName = file.FileName;
            if (vm.TotalRows == 0 && vm.Errors.Count == 0)
                vm.Errors.Add("No data rows found. Keep headers in row 1 and start data at row 2.");

            return View("~/Areas/Admin/Views/Mcq/QuestionBank/Import.cshtml", vm);
        }

        // GET: /Admin/MCQ/QuestionBank/Template
        [HttpGet("Template")]
        public IActionResult Template()
        {
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("Questions");
            ws.Cell(1, 1).Value = "Text";
            ws.Cell(1, 2).Value = "Marks";
            ws.Cell(1, 3).Value = "OptionA";
            ws.Cell(1, 4).Value = "OptionB";
            ws.Cell(1, 5).Value = "OptionC";
            ws.Cell(1, 6).Value = "OptionD";
            ws.Cell(1, 7).Value = "Correct (A-D)";
            ws.Cell(1, 8).Value = "Tag";

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
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "QuestionImportTemplate.xlsx");
        }

        private static string BuildNormalized(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var s = input.ToLowerInvariant()
                .Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Trim();
        }

        // Recalculates total marks for quizzes containing any of the specified question IDs.
        private async Task RecalculateTotalsForQuizIdsAsync(IEnumerable<Guid> quizIds)
        {
            var ids = quizIds.Distinct().ToList();
            if (!ids.Any()) return;

            var quizzes = await _db.Quizzes
                .Where(q => ids.Contains(q.Id))
                .ToListAsync();

            foreach (var quiz in quizzes)
            {
                var questionIds = await _db.QuizQuestions
                    .Where(qq => qq.QuizId == quiz.Id)
                    .Select(qq => qq.QuestionId)
                    .ToListAsync();

                decimal total = 0m;
                if (questionIds.Any())
                {
                    total = await _db.McqQuestions
                        .Where(q => questionIds.Contains(q.Id))
                        .SumAsync(q => q.Marks);
                }

                quiz.TotalMarks = (int)Math.Round(total);
            }

            await _db.SaveChangesAsync();
        }

    }
}
