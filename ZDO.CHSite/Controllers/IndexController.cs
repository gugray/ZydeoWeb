using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;

namespace ZDO.CHSite.Controllers
{
    /// <summary>
    /// Serves page of single-page app (we only have one page).
    /// </summary>
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class IndexController : Controller
    {
        /// <summary>
        /// Dynamic page controller.
        /// </summary>
        private readonly DynpageController dpc;
        /// <summary>
        /// Google Analytics code.
        /// </summary>
        private readonly string gaCode;
        /// <summary>
        /// Ctor: infuse dependencies.
        /// </summary>

        public IndexController(PageProvider pageProvider, SqlDict dict, IConfiguration config)
        {
            dpc = new DynpageController(pageProvider, dict, config);
            gaCode = config["gaCode"];
        }

        /// <summary>
        /// Serves single-page app's page requests.
        /// </summary>
        /// <param name="paras">The entire relative URL.</param>
        public IActionResult Index(string paras)
        {
            var pr = dpc.GetPageResult("hu", paras);
            IndexModel model = new IndexModel("hu", pr.RelNorm, pr, gaCode);
            return View("/Index.cshtml", model);
        }
    }
}

