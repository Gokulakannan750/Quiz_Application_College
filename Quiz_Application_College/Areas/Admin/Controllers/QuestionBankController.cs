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
    }
}
