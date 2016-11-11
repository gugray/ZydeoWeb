using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ZDO.Console.Logic
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class IndexController : Controller
    {
        private Options opt;

        public IndexController(IOptions<Options> opt)
        {
            this.opt = opt.Value;
        }

        /// <summary>
        /// Serves single-page app's page requests.
        /// </summary>
        /// <param name="paras">The entire relative URL.</param>
        public IActionResult Index(string paras)
        {
            string rel = null;
            foreach (var site in opt.Sites)
                if (site.ShortName == paras) { rel = paras; break; }
            if (rel == null)
                return RedirectPermanent("/" + opt.Sites[0].ShortName);
            IndexModel model = new IndexModel
            {
                Sites = opt.Sites,
                Rel = rel
            };
            return View("/Index.cshtml", model);
        }
    }
}

