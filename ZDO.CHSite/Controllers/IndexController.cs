using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Countries;
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
        /// ZDO mutation.
        /// </summary>
        private readonly Mutation mut;
        /// <summary>
        /// Site's base URL.
        /// </summary>
        private readonly string baseUrl;
        /// <summary>
        /// Dynamic page controller.
        /// </summary>
        private readonly DynpageController dpc;
        /// <summary>
        /// Google Analytics code.
        /// </summary>
        private readonly string gaCode;
        /// <summary>
        /// Site key for Google recaptchas.
        /// </summary>
        private readonly string captchaSiteKey;

        /// <summary>
        /// Ctor: infuse dependencies.
        /// </summary>
        public IndexController(PageProvider pageProvider, IConfiguration config, 
            ILoggerFactory loggerFactory, Auth auth, SqlDict dict)
        {
            mut = config["MUTATION"] == "HDD" ? Mutation.HDD : Mutation.CHD;
            baseUrl = config["baseUrl"];
            dpc = new DynpageController(pageProvider, config, loggerFactory, auth, dict, null, null, null, null);
            gaCode = config["gaCode"];
            captchaSiteKey = config["captchaSiteKey"];
        }

        private static void getLangRel(string str, out string lang, out string rel)
        {
            if (str == "en" || str == "de" || str == "hu")
            {
                lang = str;
                rel = "";
                return;
            }
            if (str.StartsWith("en/") || str.StartsWith("de/") || str.StartsWith("hu/"))
            {
                lang = str.Substring(0, 2);
                rel = str.Substring(3);
                return;
            }
            lang = rel = null;
        }

        /// <summary>
        /// Serves single-page app's page requests.
        /// </summary>
        /// <param name="paras">The entire relative URL.</param>
        public IActionResult Index(string paras)
        {
            string fullRel = paras == null ? "" : paras;
            string lang, rel;
            getLangRel(fullRel, out lang, out rel);
            if (lang == null)
            {
                // TO-DO: Check language cookie here
                string redirTo = mut == Mutation.CHD ? "hu" : "de";
                return RedirectPermanent("/" + redirTo);
            }
            // Infuse requested page right away
            var pr = dpc.GetPageResult(lang, rel, false, false);
            IndexModel model = new IndexModel(
                mut, baseUrl, lang, pr.RelNorm, pr,
                gaCode, AppVersion.VerStr, captchaSiteKey);
            return View("/Index.cshtml", model);
        }
    }
}

