using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;
using Quiz_Application_College.Services.Security;
using Quiz_Application_College.ViewModels;
using System.Text;

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
            // Dropdowns
            var colleges = await _db.StudentProfiles
                .Select(p => p.College).Distinct().OrderBy(s => s).ToListAsync();
            vm.CollegeOptions = new SelectList(colleges);

            var deptQuery = _db.StudentProfiles.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.College))
                deptQuery = deptQuery.Where(p => p.College == vm.College);
            var depts = await deptQuery.Select(p => p.Department).Distinct().OrderBy(s => s).ToListAsync();
            vm.DepartmentOptions = new SelectList(depts);

            // Base query (NO AspNetUsers join)
            var q = _db.StudentProfiles.AsQueryable();

            if (!string.IsNullOrWhiteSpace(vm.College))
                q = q.Where(p => p.College == vm.College);
            if (!string.IsNullOrWhiteSpace(vm.Department))
                q = q.Where(p => p.Department == vm.Department);
            if (!string.IsNullOrWhiteSpace(vm.Search))
            {
                var s = vm.Search.Trim().ToLower();
                q = q.Where(p =>
                    (!string.IsNullOrEmpty(p.Name) && p.Name.ToLower().Contains(s)) ||
                    (!string.IsNullOrEmpty(p.RollNumber) && p.RollNumber.ToLower().Contains(s)) ||
                    (!string.IsNullOrEmpty(p.Email) && p.Email.ToLower().Contains(s)));
            }

            vm.Total = await q.CountAsync();

            // Page
            var skip = (vm.Page - 1) * vm.PageSize;
            var pageRows = await q
                .OrderBy(p => p.College).ThenBy(p => p.Department).ThenBy(p => p.RollNumber)
                .Skip(skip).Take(vm.PageSize)
                .Select(p => new StudentBrowseVm.Row
                {
                    // keep UserId if you still have it on StudentProfile (used to match Enrollment.UserId)
                    UserId = p.UserId ?? string.Empty,
                    College = p.College,
                    Department = p.Department,
                    RollNumber = p.RollNumber,
                    Name = p.Name,
                    Email = p.Email,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            // Build a map: UserId -> Email (from current page)
            var userIdToEmail = pageRows
                .Where(r => !string.IsNullOrWhiteSpace(r.UserId))
                .GroupBy(r => r.UserId)
                .ToDictionary(g => g.Key!, g => g.Select(x => x.Email ?? string.Empty).FirstOrDefault() ?? string.Empty);

            // Collect UserIds from current page to look up enrollments
            var userIds = userIdToEmail.Keys.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();

            // Pull enrollments by UserId and group them per user
            var enrollments = await _db.Enrollments
                .Include(e => e.Quiz)
                .Where(e => userIds.Contains(e.UserId) && e.Quiz != null)
                .Select(e => new { e.UserId, e.Quiz!.Title, e.Quiz!.Type })
                .ToListAsync();

            // Group by UserId → then attach to rows (split MCQ vs Coding)
            var groupedByUser = enrollments
                .GroupBy(e => e.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        Mcq = g.Where(x => x.Type == QuizType.Mcq).Select(x => x.Title).Distinct().ToList(),
                        Coding = g.Where(x => x.Type == QuizType.Coding).Select(x => x.Title).Distinct().ToList()
                    });

            foreach (var row in pageRows)
            {
                if (!string.IsNullOrWhiteSpace(row.UserId) && groupedByUser.TryGetValue(row.UserId, out var lists))
                {
                    row.McqQuizzes = lists.Mcq;
                    row.CodingQuizzes = lists.Coding;
                }
                else
                {
                    row.McqQuizzes = new List<string>();
                    row.CodingQuizzes = new List<string>();
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

            int profilesCreated = 0, profilesUpdated = 0, rows = 0;
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

                        try
                        {
                            // Upsert by Email in StudentProfiles only
                            var profile = await _db.StudentProfiles
                                .FirstOrDefaultAsync(p => p.Email.ToLower() == email.ToLower());

                            if (profile == null)
                            {
                                var saltBytes = StudentPasswordHasher.NewSalt();
                                var saltB64 = Convert.ToBase64String(saltBytes);
                                var hashB64 = StudentPasswordHasher.Hash(roll, saltBytes);

                                profile = new StudentProfile
                                {
                                    Id = Guid.NewGuid(),
                                    Email = email,
                                    Name = name,
                                    RollNumber = roll,
                                    College = collegeName,
                                    Department = department,
                                    PasswordSalt = saltB64,
                                    PasswordHash = hashB64,
                                    IsActive = true,
                                    CreatedAt = DateTimeOffset.UtcNow
                                };

                                _db.StudentProfiles.Add(profile);
                                profilesCreated++;
                            }
                            else
                            {
                                profile.Email = email;
                                profile.Name = name;
                                profile.RollNumber = roll;
                                profile.College = collegeName;
                                profile.Department = department;
                                profile.CreatedAt = DateTimeOffset.UtcNow;

                                var saltBytes = StudentPasswordHasher.NewSalt();
                                profile.PasswordSalt = Convert.ToBase64String(saltBytes);
                                profile.PasswordHash = StudentPasswordHasher.Hash(roll, saltBytes);
                                profile.IsActive = true;

                                profilesUpdated++;
                            }

                            await _db.SaveChangesAsync();
                        }
                        catch (DbUpdateException ex)
                        {
                            var baseMsg = ex.GetBaseException()?.Message ?? ex.Message;
                            errors.Add($"Sheet '{ws.Name}' Row {row.RowNumber()}: DB save failed → {baseMsg}");
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Sheet '{ws.Name}' Row {row.RowNumber()}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add("Import failed: " + ex.Message);
            }

            TempData["Ok"] = $"Import complete. Rows: {rows}, Profiles added: {profilesCreated}, Profiles updated: {profilesUpdated}.";
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
