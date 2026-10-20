using Microsoft.AspNetCore.Mvc;
using RaceDay.Models;
using System.Diagnostics;

namespace RaceDay.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult AdminDashboard()
        {
            return View();
        }

        public IActionResult OrganiserDashboard()
        {
            return View();
        }
        public IActionResult Index()
        {
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
