using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.Web.Controllers
{
    public class ReportsController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}
