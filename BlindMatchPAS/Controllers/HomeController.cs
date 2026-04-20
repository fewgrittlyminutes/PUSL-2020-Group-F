using Microsoft.AspNetCore.Mvc;

namespace BlindMatchPAS.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.IsInRole("Student"))
                return RedirectToAction("Dashboard", "Student");
            if (User.IsInRole("Supervisor"))
                return RedirectToAction("Dashboard", "Supervisor");
            if (User.IsInRole("ModuleLeader"))
                return RedirectToAction("Dashboard", "ModuleLeader");
            if (User.IsInRole("SystemAdmin"))
                return RedirectToAction("Dashboard", "Admin");

            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
