using Microsoft.AspNetCore.Mvc;

namespace ZDO.ConcAlign.Controllers
{
    public class IndexController : Controller
    {
        public IActionResult Index(string paras)
        {
            return View("/Index.cshtml");
        }
    }
}

