using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Quiz_Application_College.Models;

namespace Quiz_Application_College.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }
        public IActionResult Index()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin"))
                    return RedirectToAction("Index", "Home", new { area = "Admin" });

                if (User.IsInRole("Student"))
                    return RedirectToAction("Index", "Home", new { area = "Student" });
            }

            // Your public landing page (or redirect to /Identity/Account/Login)
            return View();
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
