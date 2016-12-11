using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;
using ZDO.CHSite.Renderers;
using ZD.Common;
using ZD.LangUtils;

namespace ZDO.CHSite.Controllers
{
    public class ExportController : Controller
    {
        private readonly string exportFileName;
        private readonly string compressedFileName;
        private readonly ILogger logger;
        private static Thread thread = null;
        private static object lockObj = new object();

        public ExportController(IConfiguration config, ILoggerFactory loggerFactory)
        {
            string expDir = config["exportFolder"];
            exportFileName = Path.Combine(expDir, config["exportFileNameRaw"]);
            compressedFileName = Path.Combine(expDir, config["exportFileNameCompressed"]);
            logger = loggerFactory.CreateLogger("ExportController");
        }

        public IActionResult Go()
        {
            // This request mustn't come through proxy, and must come from localhost
            string xfwd = HttpContext.Request.Headers["X-Real-IP"];
            if (xfwd != null) return StatusCode(401, "Caller IP not authorized to trigger export.");
            if (!IPAddress.IsLoopback(HttpContext.Connection.RemoteIpAddress)) return StatusCode(401, "Caller IP not authorized to trigger export.");
            // Export already in progress?
            lock (lockObj)
            {
                if (thread != null) return StatusCode(400, "Export already in progress.");
                thread = new Thread(exportFun);
                thread.Start();
            }
            // Good
            return new ObjectResult("started");
        }

        private void doExport()
        {
            using (FileStream fs = new FileStream(exportFileName, FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
            using (SqlDict.Exporter exporter = new SqlDict.Exporter())
            {
                writePrologue(sw);
                EntryBlockWriter ebw = new EntryBlockWriter(sw);
                while (true)
                {
                    List<EntryVersion> history;
                    int entryId;
                    exporter.GetNext(out history, out entryId);
                    if (history == null) break;
                    // DBG: reduced export
                    DateTime limit = new DateTime(2016, 11, 1);
                    if (history[history.Count - 1].Timestamp < limit) continue;
                    ebw.WriteBlock(entryId, history);
                }
            }
        }

        private void writePrologue(StreamWriter sw)
        {
            // TO-DO: from embedded resource in files/other
            sw.WriteLine("# HanDeDict");
            sw.WriteLine("# " + DateTime.UtcNow.ToString());
        }

        private void exportFun()
        {
            try { doExport(); }
            catch (Exception ex) { logger.LogError(new EventId(), "Dictionary export failed.", ex); }
            finally
            {
                lock (lockObj)
                {
                    thread = null;
                }
            }
        }
    }
}
