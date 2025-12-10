using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Quiz_Application_College.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "IsAdmin")] // Only Admins can manage users
    [Route("Admin/Users")]
    public class UsersController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        // Allowed roles in this application
        private static readonly string[] AllowedRoles = new[] { "Admin", "Trainer" };

        public UsersController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: /Admin/Users
        // List all users with roles
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var list = new List<UserListItemVm>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var isAdmin = roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));
                var isTrainer = roles.Any(r => r.Equals("Trainer", StringComparison.OrdinalIgnoreCase));

                list.Add(new UserListItemVm
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    RolesDisplay = roles.Any()
                        ? string.Join(", ", roles)
                        : "(no role)",
                    IsAdmin = isAdmin,
                    IsTrainer = isTrainer
                });
            }

            return View("~/Areas/Admin/Views/Users/Index.cshtml", list);
        }

        // GET: /Admin/Users/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            var vm = new CreateUserVm
            {
                AllowedRoles = AllowedRoles.ToList()
            };

            return View("~/Areas/Admin/Views/Users/Create.cshtml", vm);
        }

        // POST: /Admin/Users/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserVm vm)
        {
            vm.AllowedRoles = AllowedRoles.ToList();

            if (!ModelState.IsValid)
            {
                return View("~/Areas/Admin/Views/Users/Create.cshtml", vm);
            }

            // 1) Create user
            var user = new IdentityUser
            {
                UserName = vm.UserName,
                Email = vm.Email,
                EmailConfirmed = true // optional
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return View("~/Areas/Admin/Views/Users/Create.cshtml", vm);
            }

            // 2) Assign role if selected
            if (!string.IsNullOrWhiteSpace(vm.SelectedRole))
            {
                var roleName = vm.SelectedRole;

                if (!AllowedRoles.Contains(roleName))
                {
                    ModelState.AddModelError(nameof(vm.SelectedRole), "Invalid role selected.");
                    return View("~/Areas/Admin/Views/Users/Create.cshtml", vm);
                }

                // Ensure role exists
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    var roleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
                    if (!roleResult.Succeeded)
                    {
                        foreach (var error in roleResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        return View("~/Areas/Admin/Views/Users/Create.cshtml", vm);
                    }
                }

                // Assign role to user
                var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (!addRoleResult.Succeeded)
                {
                    foreach (var error in addRoleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return View("~/Areas/Admin/Views/Users/Create.cshtml", vm);
                }
            }

            TempData["Ok"] = "User created successfully.";
            return RedirectToAction(nameof(Index));
        }    

        // POST: /Admin/Users/Delete/{id}
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    TempData["Error"] = error.Description;
                }

                return RedirectToAction(nameof(Index));
            }

            TempData["Ok"] = "User deleted.";
            return RedirectToAction(nameof(Index));
        }

        // VIEW MODELS

        public class UserListItemVm
        {
            public string Id { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string RolesDisplay { get; set; } = string.Empty;
            public bool IsAdmin { get; set; }
            public bool IsTrainer { get; set; }
        }

        public class CreateUserVm
        {
            [Required]
            [Display(Name = "User name")]
            public string UserName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [Display(Name = "Email address")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [StringLength(100, ErrorMessage = "{0} must be at least {2} characters long.", MinimumLength = 6)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Display(Name = "Role")]
            public string? SelectedRole { get; set; }

            public List<string> AllowedRoles { get; set; } = new();
        }
    }
}
