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
        // INDEX – main reports page
        // ------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Index(
            string selectedCollege,
            string selectedDepartment,
            string search,
            bool onlySuspicious = false)
        {
            var vm = await BuildViewModelAsync(selectedCollege, selectedDepartment, search, onlySuspicious);
            return View(vm);
        }

        // ------------------------------------------------------------
        // DOWNLOAD – Excel export (MCQ + Coding)
        // ------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Download(
            string selectedCollege,
            string selectedDepartment,
            string search,
            bool onlySuspicious = false)
        {
            var vm = await BuildViewModelAsync(selectedCollege, selectedDepartment, search, onlySuspicious);

            using var wb = new XLWorkbook();

            // ---------- MCQ sheet ----------
            var mcqSheet = wb.AddWorksheet("MCQ");
            mcqSheet.Cell(1, 1).Value = "Name";
            mcqSheet.Cell(1, 2).Value = "Register No";
            mcqSheet.Cell(1, 3).Value = "College";
            mcqSheet.Cell(1, 4).Value = "Department";
            mcqSheet.Cell(1, 5).Value = "Quiz";
            mcqSheet.Cell(1, 6).Value = "Marks";

            var r = 2;
            foreach (var row in vm.McqRows)
            {
                mcqSheet.Cell(r, 1).Value = row.StudentName;
                mcqSheet.Cell(r, 2).Value = row.RegisterNumber;
                mcqSheet.Cell(r, 3).Value = row.College;
                mcqSheet.Cell(r, 4).Value = row.Department;
                mcqSheet.Cell(r, 5).Value = row.QuizTitle;
                mcqSheet.Cell(r, 6).Value = row.TotalMarks;
                r++;
            }
            mcqSheet.Columns().AdjustToContents();

            // ---------- Coding sheet ----------
            var codingSheet = wb.AddWorksheet("Coding");
            codingSheet.Cell(1, 1).Value = "Name";
            codingSheet.Cell(1, 2).Value = "Register No";
            codingSheet.Cell(1, 3).Value = "College";
            codingSheet.Cell(1, 4).Value = "Department";
            codingSheet.Cell(1, 5).Value = "Quiz";
            codingSheet.Cell(1, 6).Value = "Marks";

            r = 2;
            foreach (var row in vm.CodingRows)
            {
                codingSheet.Cell(r, 1).Value = row.StudentName;
                codingSheet.Cell(r, 2).Value = row.RegisterNumber;
                codingSheet.Cell(r, 3).Value = row.College;
                codingSheet.Cell(r, 4).Value = row.Department;
                codingSheet.Cell(r, 5).Value = row.QuizTitle;
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
        // STUDENT ATTEMPTS – detail page for a single student
        // ------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> StudentAttempts(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            // StudentProfile.Id is stored as string in the rows
            var student = await _db.StudentProfiles
                .FirstOrDefaultAsync(s => s.Id.ToString() == id);

            if (student == null)
                return NotFound();

            var userId = "SP:" + student.Id.ToString();

            var attempts = await (from a in _db.Attempts
                                  join q in _db.Quizzes on a.QuizId equals q.Id
                                  where a.UserId == userId
                                  orderby a.StartedAt descending
                                  select new { Attempt = a, Quiz = q })
                                  .ToListAsync();

            var vm = new StudentAttemptDetailVm
            {
                StudentProfileId = student.Id.ToString(),
                StudentName = student.Name,
                RegisterNumber = student.RollNumber,
                College = student.College,
                Department = student.Department,
                Attempts = new List<AttemptDetailRowVm>()
            };

            foreach (var x in attempts)
            {
                var a = x.Attempt;
                var q = x.Quiz;

                var score = (decimal?)a.Score ?? 0m;

                var ipChanged =
                    !string.IsNullOrWhiteSpace(a.StartIpAddress) &&
                    !string.IsNullOrWhiteSpace(a.SubmitIpAddress) &&
                    !string.Equals(
                        a.StartIpAddress,
                        a.SubmitIpAddress,
                        StringComparison.OrdinalIgnoreCase);

                var uaChanged =
                    !string.IsNullOrWhiteSpace(a.StartUserAgent) &&
                    !string.IsNullOrWhiteSpace(a.SubmitUserAgent) &&
                    !string.Equals(
                        a.StartUserAgent,
                        a.SubmitUserAgent,
                        StringComparison.OrdinalIgnoreCase);

                vm.Attempts.Add(new AttemptDetailRowVm
                {
                    QuizTitle = q.Title,
                    QuizType = q.Type == QuizType.Mcq ? "MCQ" :
                               q.Type == QuizType.Coding ? "Coding" :
                               q.Type.ToString(),
                    Score = score,
                    StartedAt = a.StartedAt,
                    SubmittedAt = a.SubmittedAt,
                    StartIpAddress = a.StartIpAddress,
                    SubmitIpAddress = a.SubmitIpAddress,
                    IpChanged = ipChanged,
                    UserAgentChanged = uaChanged
                });
            }

            return View(vm);
        }

        // ------------------------------------------------------------
        // Build view model once (used by Index + Download)
        // ------------------------------------------------------------
        private async Task<ReportsIndexVm> BuildViewModelAsync(
            string college,
            string department,
            string search,
            bool onlySuspicious)
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
                OnlySuspicious = onlySuspicious,
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

            // 3) Load attempts with quiz type + title + audit data
            var allAttempts = await (from a in _db.Attempts
                                     join q in _db.Quizzes on a.QuizId equals q.Id
                                     select new
                                     {
                                         a.UserId,
                                         a.Score,
                                         q.Type,
                                         q.Title,
                                         a.StartIpAddress,
                                         a.SubmitIpAddress,
                                         a.StartUserAgent,
                                         a.SubmitUserAgent
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

                    var ipChanged =
                        !string.IsNullOrWhiteSpace(a.StartIpAddress) &&
                        !string.IsNullOrWhiteSpace(a.SubmitIpAddress) &&
                        !string.Equals(
                            a.StartIpAddress,
                            a.SubmitIpAddress,
                            StringComparison.OrdinalIgnoreCase);

                    var uaChanged =
                        !string.IsNullOrWhiteSpace(a.StartUserAgent) &&
                        !string.IsNullOrWhiteSpace(a.SubmitUserAgent) &&
                        !string.Equals(
                            a.StartUserAgent,
                            a.SubmitUserAgent,
                            StringComparison.OrdinalIgnoreCase);

                    mcqRows.Add(new StudentReportRowVm
                    {
                        StudentProfileId = s.Id.ToString(),
                        StudentName = s.Name,
                        RegisterNumber = s.RollNumber,
                        College = s.College,
                        Department = s.Department,
                        QuizTitle = a.Title,
                        TotalMarks = marks,
                        IpChanged = ipChanged,
                        UserAgentChanged = uaChanged
                    });
                }

                // Coding – one row per attempt
                foreach (var a in codingList)
                {
                    var marks = (decimal?)a.Score ?? 0m;

                    var ipChanged =
                        !string.IsNullOrWhiteSpace(a.StartIpAddress) &&
                        !string.IsNullOrWhiteSpace(a.SubmitIpAddress) &&
                        !string.Equals(
                            a.StartIpAddress,
                            a.SubmitIpAddress,
                            StringComparison.OrdinalIgnoreCase);

                    var uaChanged =
                        !string.IsNullOrWhiteSpace(a.StartUserAgent) &&
                        !string.IsNullOrWhiteSpace(a.SubmitUserAgent) &&
                        !string.Equals(
                            a.StartUserAgent,
                            a.SubmitUserAgent,
                            StringComparison.OrdinalIgnoreCase);

                    codingRows.Add(new StudentReportRowVm
                    {
                        StudentProfileId = s.Id.ToString(),
                        StudentName = s.Name,
                        RegisterNumber = s.RollNumber,
                        College = s.College,
                        Department = s.Department,
                        QuizTitle = a.Title,
                        TotalMarks = marks,
                        IpChanged = ipChanged,
                        UserAgentChanged = uaChanged
                    });
                }
            }

            // 4) If "OnlySuspicious" is on, keep only rows where IP or UA changed
            if (onlySuspicious)
            {
                mcqRows = mcqRows
                    .Where(r => r.IpChanged || r.UserAgentChanged)
                    .ToList();

                codingRows = codingRows
                    .Where(r => r.IpChanged || r.UserAgentChanged)
                    .ToList();
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

            public bool OnlySuspicious { get; set; }

            public List<SelectListItem> CollegeOptions { get; set; } = new();
            public List<SelectListItem> DepartmentOptions { get; set; } = new();

            public List<StudentReportRowVm> McqRows { get; set; } = new();
            public List<StudentReportRowVm> CodingRows { get; set; } = new();
        }

        public class StudentReportRowVm
        {
            public string StudentProfileId { get; set; }   // for detail link

            public string StudentName { get; set; }
            public string RegisterNumber { get; set; }
            public string College { get; set; }
            public string Department { get; set; }

            public string QuizTitle { get; set; }
            public decimal TotalMarks { get; set; }

            public bool IpChanged { get; set; }
            public bool UserAgentChanged { get; set; }
        }

        // Detail page VMs
        public class StudentAttemptDetailVm
        {
            public string StudentProfileId { get; set; }
            public string StudentName { get; set; }
            public string RegisterNumber { get; set; }
            public string College { get; set; }
            public string Department { get; set; }

            public List<AttemptDetailRowVm> Attempts { get; set; } = new();
        }

        public class AttemptDetailRowVm
        {
            public string QuizTitle { get; set; }
            public string QuizType { get; set; } // MCQ / Coding
            public decimal Score { get; set; }

            // Match the Attempt entity types (DateTimeOffset)
            public DateTimeOffset StartedAt { get; set; }
            public DateTimeOffset? SubmittedAt { get; set; }

            public string StartIpAddress { get; set; }
            public string SubmitIpAddress { get; set; }

            public bool IpChanged { get; set; }
            public bool UserAgentChanged { get; set; }
        }

    }
}
