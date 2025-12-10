using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Trainer")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ReportsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ------------------------------------------------------------
        // INDEX – filter students and show MCQ / Coding marks per student
        // ------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Index(string college, string department, string search)
        {
            var vm = await BuildViewModelAsync(college, department, search);
            return View(vm);
        }

        // ------------------------------------------------------------
        // DOWNLOAD – same filters, Excel with two sheets: MCQ / Coding
        // ------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Download(string college, string department, string search)
        {
            var vm = await BuildViewModelAsync(college, department, search);

            using var wb = new XLWorkbook();

            // ---------- MCQ sheet ----------
            var mcqSheet = wb.AddWorksheet("MCQ");
            mcqSheet.Cell(1, 1).Value = "Name";
            mcqSheet.Cell(1, 2).Value = "Register No";
            mcqSheet.Cell(1, 3).Value = "Email";
            mcqSheet.Cell(1, 4).Value = "College";
            mcqSheet.Cell(1, 5).Value = "Department";
            mcqSheet.Cell(1, 6).Value = "Marks";

            var r = 2;
            foreach (var row in vm.McqRows)
            {
                mcqSheet.Cell(r, 1).Value = row.StudentName;
                mcqSheet.Cell(r, 2).Value = row.RegisterNumber;
                mcqSheet.Cell(r, 3).Value = row.Email;
                mcqSheet.Cell(r, 4).Value = row.College;
                mcqSheet.Cell(r, 5).Value = row.Department;
                mcqSheet.Cell(r, 6).Value = row.TotalMarks;
                r++;
            }
            mcqSheet.Columns().AdjustToContents();

            // ---------- Coding sheet ----------
            var codingSheet = wb.AddWorksheet("Coding");
            codingSheet.Cell(1, 1).Value = "Name";
            codingSheet.Cell(1, 2).Value = "Register No";
            codingSheet.Cell(1, 3).Value = "Email";
            codingSheet.Cell(1, 4).Value = "College";
            codingSheet.Cell(1, 5).Value = "Department";
            codingSheet.Cell(1, 6).Value = "Marks";

            r = 2;
            foreach (var row in vm.CodingRows)
            {
                codingSheet.Cell(r, 1).Value = row.StudentName;
                codingSheet.Cell(r, 2).Value = row.RegisterNumber;
                codingSheet.Cell(r, 3).Value = row.Email;
                codingSheet.Cell(r, 4).Value = row.College;
                codingSheet.Cell(r, 5).Value = row.Department;
                codingSheet.Cell(r, 6).Value = row.TotalMarks;
                r++;
            }
            codingSheet.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            return File(
                ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "StudentReports.xlsx");
        }

        // ------------------------------------------------------------
        // Build view model once (used by Index + Download)
        // ------------------------------------------------------------
        private async Task<ReportsIndexVm> BuildViewModelAsync(string college, string department, string search)
        {
            college ??= string.Empty;
            department ??= string.Empty;
            search ??= string.Empty;

            // 1) Base student query with filters
            var studentsQuery = _db.StudentProfiles.AsQueryable();

            if (!string.IsNullOrEmpty(college))
            {
                studentsQuery = studentsQuery.Where(s => s.College == college);
            }

            if (!string.IsNullOrEmpty(department))
            {
                studentsQuery = studentsQuery.Where(s => s.Department == department);
            }

            if (!string.IsNullOrEmpty(search))
            {
                var sTerm = search.Trim();
                studentsQuery = studentsQuery.Where(s =>
                    s.Name.Contains(sTerm) ||
                    s.RollNumber.Contains(sTerm) ||
                    s.Email.Contains(sTerm));
            }

            var students = await studentsQuery
                .OrderBy(s => s.Name)
                .ToListAsync();

            // 2) Dropdown sources
            var allColleges = await _db.StudentProfiles
                .Select(s => s.College)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            var allDepartments = await _db.StudentProfiles
                .Select(s => s.Department)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            var vm = new ReportsIndexVm
            {
                SelectedCollege = college,
                SelectedDepartment = department,
                Search = search,
                CollegeOptions = new List<SelectListItem>(),
                DepartmentOptions = new List<SelectListItem>()
            };

            // College dropdown
            vm.CollegeOptions.Add(new SelectListItem
            {
                Value = "",
                Text = "All",
                Selected = string.IsNullOrEmpty(college)
            });
            foreach (var c in allColleges)
            {
                vm.CollegeOptions.Add(new SelectListItem
                {
                    Value = c,
                    Text = c,
                    Selected = c == college
                });
            }

            // Department dropdown
            vm.DepartmentOptions.Add(new SelectListItem
            {
                Value = "",
                Text = "All",
                Selected = string.IsNullOrEmpty(department)
            });
            foreach (var d in allDepartments)
            {
                vm.DepartmentOptions.Add(new SelectListItem
                {
                    Value = d,
                    Text = d,
                    Selected = d == department
                });
            }

            if (!students.Any())
            {
                vm.McqRows = new List<StudentReportRowVm>();
                vm.CodingRows = new List<StudentReportRowVm>();
                return vm;
            }

            // 3) Load attempts with quiz type
            var allAttempts = await (from a in _db.Attempts
                                     join q in _db.Quizzes on a.QuizId equals q.Id
                                     select new
                                     {
                                         a.UserId,
                                         a.Score,
                                         q.Type
                                     }).ToListAsync();

            var attemptsByUser = allAttempts
                .GroupBy(a => a.UserId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var mcqRows = new List<StudentReportRowVm>();
            var codingRows = new List<StudentReportRowVm>();

            foreach (var s in students)
            {
                var key = "SP:" + s.Id.ToString();

                if (!attemptsByUser.TryGetValue(key, out var listForStudent))
                {
                    continue; // no attempts for this student
                }

                var mcqList = listForStudent.Where(x => x.Type == QuizType.Mcq).ToList();
                var codingList = listForStudent.Where(x => x.Type == QuizType.Coding).ToList();

                // MCQ – one row per attempt
                foreach (var a in mcqList)
                {
                    var marks = (decimal?)a.Score ?? 0m;

                    mcqRows.Add(new StudentReportRowVm
                    {
                        StudentName = s.Name,
                        RegisterNumber = s.RollNumber,
                        Email = s.Email,
                        College = s.College,
                        Department = s.Department,
                        TotalMarks = marks   // marks for THIS attempt
                    });
                }

                // Coding – one row per attempt
                foreach (var a in codingList)
                {
                    var marks = (decimal?)a.Score ?? 0m;

                    codingRows.Add(new StudentReportRowVm
                    {
                        StudentName = s.Name,
                        RegisterNumber = s.RollNumber,
                        Email = s.Email,
                        College = s.College,
                        Department = s.Department,
                        TotalMarks = marks   // marks for THIS attempt
                    });
                }
            }

            vm.McqRows = mcqRows
                .OrderByDescending(r => r.TotalMarks)
                .ThenBy(r => r.StudentName)
                .ToList();

            vm.CodingRows = codingRows
                .OrderByDescending(r => r.TotalMarks)
                .ThenBy(r => r.StudentName)
                .ToList();

            return vm;
        }

        // ------------------------------------------------------------
        // View models
        // ------------------------------------------------------------
        public class ReportsIndexVm
        {
            public string SelectedCollege { get; set; }
            public string SelectedDepartment { get; set; }
            public string Search { get; set; }

            public List<SelectListItem> CollegeOptions { get; set; } = new();
            public List<SelectListItem> DepartmentOptions { get; set; } = new();

            public List<StudentReportRowVm> McqRows { get; set; } = new();
            public List<StudentReportRowVm> CodingRows { get; set; } = new();
        }

        public class StudentReportRowVm
        {
            public string StudentName { get; set; }
            public string RegisterNumber { get; set; }
            public string Email { get; set; }
            public string College { get; set; }
            public string Department { get; set; }

            // Sum of scores for that student in that category (MCQ / Coding)
            public decimal TotalMarks { get; set; }
        }
    }
}
