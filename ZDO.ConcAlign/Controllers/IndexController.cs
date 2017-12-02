using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
