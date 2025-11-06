using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Application_College.Data;
using Quiz_Application_College.Domain;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")]
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

        // GET: /Admin/StudentProfiles
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            return View("~/Areas/Admin/Views/StudentProfiles/Index.cshtml");
        }

        // GET: /Admin/StudentProfiles/Import
        [HttpGet("Import")]
        public IActionResult Import()
        {
            return View("~/Areas/Admin/Views/StudentProfiles/Import.cshtml");
        }

        // POST: /Admin/StudentProfiles/Import
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

            // Ensure Student role exists
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
                    if (headerRow == null)
                    {
                        errors.Add($"Sheet '{ws.Name}': No data.");
                        continue;
                    }

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
                    {
                        errors.Add($"Sheet '{ws.Name}': Missing required headers (Name, Email, RollNumber).");
                        continue;
                    }

                    foreach (var row in ws.RowsUsed().Skip(1))
                    {
                        rows++;
                        var name = row.Cell(colName).GetString().Trim();
                        var email = row.Cell(colEmail).GetString().Trim();
                        var roll = row.Cell(colRoll).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                        {
                            errors.Add($"Sheet '{ws.Name}' Row {row.RowNumber()}: Invalid email.");
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(roll))
                        {
                            errors.Add($"Sheet '{ws.Name}' Row {row.RowNumber()}: Missing roll number.");
                            continue;
                        }

                        // Find or create Identity user
                        var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                            u.Email != null && u.Email.ToLower() == email.ToLower());

                        if (user == null)
                        {
                            user = new IdentityUser
                            {
                                // Login can accept roll or email if your login logic supports both:
                                UserName = roll,
                                Email = email,
                                EmailConfirmed = true
                            };
                            var pwd = "Student@12345"; // TODO: change/reset policy
                            var createRes = await _userManager.CreateAsync(user, pwd);
                            if (!createRes.Succeeded)
                            {
                                errors.Add($"Sheet '{ws.Name}' Row {row.RowNumber()}: Create user failed ({string.Join("; ", createRes.Errors.Select(e => e.Description))}).");
                                continue;
                            }
                            await _userManager.AddToRoleAsync(user, "Student");
                            usersCreated++;
                        }
                        else
                        {
                            // Optionally align username to roll number
                            if (!string.Equals(user.UserName, roll, StringComparison.Ordinal))
                            {
                                user.UserName = roll;
                                await _userManager.UpdateAsync(user);
                            }
                        }

                        // Upsert StudentProfile
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

        // Optional: CSV/XLSX template
        [HttpGet("Template")]
        public FileContentResult Template()
        {
            var bytes = Encoding.UTF8.GetBytes("Name,Email,RollNumber\nJohn Doe,john@abc.edu,CS001\n");
            return File(bytes, "text/csv", "StudentImportTemplate.csv");
        }
    }
}
