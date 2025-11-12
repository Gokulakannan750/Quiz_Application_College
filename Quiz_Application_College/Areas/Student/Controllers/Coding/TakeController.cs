using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Application_College.Areas.Student.Controllers.Coding
{
    [Area("Student")]
    [Authorize(AuthenticationSchemes = "StudentCookie")]
    [Route("Student/Coding/Take")]
    public class TakeController : Controller
    {
        [HttpGet("")]
        public IActionResult Index(Guid quizId)
        {
            // TEMP view — you will replace with the real coding editor page
            return Content($"Coding quiz started. QuizId = {quizId}. (TODO: render coding editor)");
        }
    }
}
