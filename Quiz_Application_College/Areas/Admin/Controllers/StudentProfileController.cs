using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    // support both plural and "students" paths
    [Route("Admin/StudentProfiles")]
    [Route("Admin/Students")]
    public class StudentProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public StudentProfileController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // --------------------------------------------------------------------
        // DASHBOARD TILE
        // GET: /Admin/StudentProfiles   (or /Admin/Students)
        // --------------------------------------------------------------------
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Areas/Admin/Views/StudentProfiles/Index.cshtml");
        }

        // --------------------------------------------------------------------
        // BROWSE (READ-ONLY): filter + show MCQ/Coding enrollments per student
        // GET: /Admin/StudentProfiles/Browse
        // --------------------------------------------------------------------
        [HttpGet("Browse")]
        public async Task<IActionResult> Browse([FromQuery] StudentBrowseVm vm)
        {
            // dropdowns
            var colleges = await _db.StudentProfiles
                .Select(p => p.College).Distinct().OrderBy(s => s).ToListAsync();
            vm.CollegeOptions = new SelectList(colleges);

            var deptQuery = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College))
                deptQuery = deptQuery.Where(p => p.College == vm.College);
            var depts = await deptQuery.Select(p => p.Department).Distinct().OrderBy(s => s).ToListAsync();
            vm.DepartmentOptions = new SelectList(depts);

            // base query
            var q = _db.StudentProfiles
                .Join(_db.Users, p => p.UserId, u => u.Id, (p, u) => new { p, u })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(vm.College))
                q = q.Where(x => x.p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department))
                q = q.Where(x => x.p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim().ToLower();
                q = q.Where(x =>
                    (x.p.Name != null && x.p.Name.ToLower().Contains(s)) ||
                    (x.p.RollNumber != null && x.p.RollNumber.ToLower().Contains(s)) ||
                    (x.u.Email != null && x.u.Email.ToLower().Contains(s)));
            }

            vm.Total = await q.CountAsync();

            // page
            var skip = (vm.Page - 1) * vm.PageSize;
            var pageRows = await q
                .OrderBy(x => x.p.College).ThenBy(x => x.p.Department).ThenBy(x => x.p.RollNumber)
                .Skip(skip).Take(vm.PageSize)
                .Select(x => new StudentBrowseVm.Row
                {
                    UserId = x.p.UserId,
                    College = x.p.College,
                    Department = x.p.Department,
                    RollNumber = x.p.RollNumber,
                    Name = x.p.Name,
                    Email = x.u.Email ?? "",
                    CreatedAt = x.p.CreatedAt
                })
                .ToListAsync();

            // enrollments for these users
            var userIds = pageRows.Select(r => r.UserId).Distinct().ToList();

            var enrollments = await _db.Enrollments
                .Include(e => e.Quiz)
                .Where(e => userIds.Contains(e.UserId) && e.Quiz != null)
                .Select(e => new { e.UserId, e.Quiz!.Title, e.Quiz!.Type })
                .ToListAsync();

            var grouped = enrollments
                .GroupBy(e => e.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Mcq = g.Where(x => x.Type == QuizType.Mcq)
                               .Select(x => x.Title).Distinct().ToList(),
                        Coding = g.Where(x => x.Type == QuizType.Coding)
                                 .Select(x => x.Title).Distinct().ToList()
                    });

            foreach (var row in pageRows)
            {
                if (grouped.TryGetValue(row.UserId, out var lists))
                {
                    row.McqQuizzes = lists.Mcq;
                    row.CodingQuizzes = lists.Coding;
                }
            }

            vm.Rows = pageRows;
            return View("~/Areas/Admin/Views/StudentProfiles/Browse.cshtml", vm);
        }

        // --------------------------------------------------------------------
        // IMPORT (GET)
        // --------------------------------------------------------------------
        [HttpGet("Import")]
        public IActionResult Import()
        {
            return View("~/Areas/Admin/Views/StudentProfiles/Import.cshtml");
        }

        // --------------------------------------------------------------------
        // IMPORT (POST): filename => College; sheet names => Departments;
        // headers: Name, Email, RollNumber
        // Creates/updates Identity users + StudentProfiles
        // --------------------------------------------------------------------
        [HttpPost("Import")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Err"] = "Please upload an Excel (.xlsx) file.";
                return RedirectToAction(nameof(Import));
            }

            var collegeName = Path.GetFileNameWithoutExtension(file.FileName)?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(collegeName))
            {
                TempData["Err"] = "Cannot derive college name from file name.";
                return RedirectToAction(nameof(Import));
            }

            if (!await _roleManager.RoleExistsAsync("Student"))
                await _roleManager.CreateAsync(new IdentityRole("Student"));

            int usersCreated = 0, profilesCreated = 0, profilesUpdated = 0, rows = 0;
            var errors = new List<string>();

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;

                using var wb = new XLWorkbook(ms);
                foreach (var ws in wb.Worksheets)
                {
                    var department = ws.Name?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(department)) continue;

                    var headerRow = ws.FirstRowUsed();
                    if (headerRow == null) { errors.Add($"Sheet '{ws.Name}': No data."); continue; }

                    var headers = headerRow.CellsUsed().ToDictionary(
                        c => c.GetString().Trim().ToLowerInvariant(),
                        c => c.Address.ColumnNumber);

                    int colName = headers.ContainsKey("name") ? headers["name"] : -1;
                    int colEmail = headers.ContainsKey("email") ? headers["email"]
                                 : headers.ContainsKey("email id") ? headers["email id"] : -1;
                    int colRoll = headers.ContainsKey("rollnumber") ? headers["rollnumber"]
                                : headers.ContainsKey("roll") ? headers["roll"]
                                : headers.ContainsKey("roll no") ? headers["roll no"] : -1;

                    if (colName == -1 || colEmail == -1 || colRoll == -1)
                    { errors.Add($"Sheet '{ws.Name}': Missing headers (Name, Email, RollNumber)."); continue; }

                    foreach (var row in ws.RowsUsed().Skip(1))
                    {
                        rows++;
                        var name = row.Cell(colName).GetString().Trim();
                        var email = row.Cell(colEmail).GetString().Trim();
                        var roll = row.Cell(colRoll).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                        { errors.Add($"Sheet '{ws.Name}' Row {row.RowNumber()}: Invalid email."); continue; }
                        if (string.IsNullOrWhiteSpace(roll))
                        { errors.Add($"Sheet '{ws.Name}' Row {row.RowNumber()}: Missing roll number."); continue; }

                        // user find/create
                        var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                            u.Email != null && u.Email.ToLower() == email.ToLower());

                        if (user == null)
                        {
                            user = new IdentityUser { UserName = roll, Email = email, EmailConfirmed = true };
                            var pwd = "Student@12345";
                            var createRes = await _userManager.CreateAsync(user, pwd);
                            if (!createRes.Succeeded)
                            { errors.Add($"Sheet '{ws.Name}' Row {row.RowNumber()}: {string.Join("; ", createRes.Errors.Select(e => e.Description))}"); continue; }

                            await _userManager.AddToRoleAsync(user, "Student");
                            usersCreated++;
                        }
                        else
                        {
                            if (!string.Equals(user.UserName, roll, StringComparison.Ordinal))
                            {
                                user.UserName = roll;
                                await _userManager.UpdateAsync(user);
                            }
                        }

                        // profile upsert
                        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                        if (profile == null)
                        {
                            _db.StudentProfiles.Add(new StudentProfile
                            {
                                UserId = user.Id,
                                College = collegeName,
                                Department = department,
                                RollNumber = roll,
                                Name = name
                            });
                            profilesCreated++;
                        }
                        else
                        {
                            profile.College = collegeName;
                            profile.Department = department;
                            profile.RollNumber = roll;
                            profile.Name = name;
                            profilesUpdated++;
                        }
                    }
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                errors.Add("Import failed: " + ex.Message);
            }

            TempData["Ok"] = $"Import complete. Rows: {rows}, Users created: {usersCreated}, Profiles added: {profilesCreated}, Profiles updated: {profilesUpdated}.";
            if (errors.Any()) TempData["Err"] = string.Join("<br/>", errors);

            return RedirectToAction(nameof(Import));
        }

        // --------------------------------------------------------------------
        // simple CSV template
        // GET: /Admin/StudentProfiles/Template
        // --------------------------------------------------------------------
        [HttpGet("Template")]
        public FileContentResult Template()
        {
            var bytes = Encoding.UTF8.GetBytes("Name,Email,RollNumber\nJohn Doe,john@abc.edu,CS001\n");
            return File(bytes, "text/csv", "StudentImportTemplate.csv");
        }
    }
}
