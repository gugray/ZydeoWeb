using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace ZDO.CHSite.Controllers
{
    public class FilesController : Controller
    {
        private readonly string downloadsFolder;
        private readonly string baseHost;

        public FilesController(IConfiguration config)
        {
            downloadsFolder = config["downloadsFolder"];
            Uri baseUri = new Uri(config["baseUrl"]);
            baseHost = baseUri.Host;
        }

        public IActionResult Get(string name)
        {
            string filePath = Path.Combine(downloadsFolder, name);
            // If requested file doesn't exist, redirect to elegant 404
            if (!System.IO.File.Exists(filePath))
            {
                string redirTo = "/en/404";
                // If link was clicked within this site, try and figure out UI language
                // But don't sweat it.
                try
                {
                    string strReferer = Request.Headers["Referer"];
                    Uri uriReferer = new Uri(strReferer);
                    if (uriReferer.Host == baseHost || uriReferer.Host == "localhost")
                        redirTo = uriReferer.LocalPath.Substring(0, 4) + "404";
                }
                catch { }
                return Redirect(redirTo);
            }
            FileInfo fi = new FileInfo(filePath);
            return PhysicalFile(fi.FullName, "application/octet-stream", name);
        }
    }
}
