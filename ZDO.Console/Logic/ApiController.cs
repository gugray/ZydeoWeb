using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZDO.Console.Logic
{
    public class ApiController : Controller
    {
        public static object TaskLock = new object();
        public static Task LastTask = null;
        public static Task RunningTask = null;

        private Options opt;

        public ApiController(IOptions<Options> opt)
        {
            this.opt = opt.Value;
        }

        public IActionResult GetValues([FromQuery] string shortName)
        {
            SiteConfig sc = null;
            foreach (var si in opt.Sites)
                if (si.ShortName == shortName) { sc = si; break; }
            if (sc == null) return StatusCode(404, "No such site.");
            try
            {
                bool srvRunning = Helpers.IsSrvRunning(Path.Combine(sc.AppRoot, "service/service.pid"));
                var config = Helpers.GetAppConfig(sc.EtcRoot);

                SiteValues sv = new SiteValues
                {
                    Maintenance = "?",
                    Service = srvRunning ? "running" : "stopped",
                    DbModel = Helpers.GetDbModel(config),
                    AppVersion = Helpers.GetAppVer(sc.AppRoot),
                    LogLevel = "?",
                    AppLog = "?",
                    DbDump = "?",
                    QueryLog = "?",
                    DictExport = "?",
                };
                return new ObjectResult(sv);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Unexpected error\n" + ex.Message);
            }
        }

        public IActionResult GetStatus([FromQuery] bool clearStatus)
        {
            lock (TaskLock)
            {
                if (RunningTask != null)
                    return new ObjectResult(new Status { StatusClass = "working", StatusMsg = RunningTask.StatusMsg });
                if (LastTask != null)
                {
                    Status s = new Status
                    {
                        StatusClass = LastTask.Succeeded ? "success" : "fail",
                        StatusMsg = LastTask.StatusMsg,
                    };
                    if (clearStatus)
                        LastTask = null;
                    return new ObjectResult(s);
                }
                return new ObjectResult(new Status());
            }
        }

        public IActionResult Execute([FromForm] string shortName, [FromForm] string cmd)
        {
            lock (TaskLock)
            {
                if (RunningTask != null)
                    return StatusCode(404, "A task is already in progress.");
                RunningTask = new Task(shortName, cmd, opt);
                return new ObjectResult(new Status { StatusClass = "working", StatusMsg = RunningTask.StatusMsg });
            }
        }
    }
}
