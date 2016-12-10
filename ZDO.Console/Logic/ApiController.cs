using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
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
                string logLevel, appLogInfo, queryLogInfo;
                getLogInfo(config, out logLevel, out appLogInfo, out queryLogInfo);

                SiteValues sv = new SiteValues
                {
                    Maintenance = "?",
                    Service = srvRunning ? "running" : "stopped",
                    DbModel = Helpers.GetDbModel(config),
                    AppVersion = Helpers.GetAppVer(sc.AppRoot),
                    LogLevel = logLevel,
                    AppLog = appLogInfo,
                    DbDump = getDumpInfo(sc),
                    QueryLog = queryLogInfo,
                    DictExport = "?",
                };
                return new ObjectResult(sv);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Unexpected error\n" + ex.Message);
            }
        }

        private void getLogInfo(IConfiguration cfg, out string logLevel, out string appLogInfo, out string queryLogInfo)
        {
            logLevel = cfg["logLevel"];
            FileInfo fi = new FileInfo(cfg["logFileName"]);
            appLogInfo = Helpers.GetDTString(fi.LastWriteTimeUtc.ToLocalTime()) + " • " + Helpers.GetSizeString((int)fi.Length);
            fi = new FileInfo(cfg["queryLogFileName"]);
            queryLogInfo = Helpers.GetDTString(fi.LastWriteTimeUtc.ToLocalTime()) + " • " + Helpers.GetSizeString((int)fi.Length);
        }

        private string getDumpInfo(SiteConfig sc)
        {
            string expFolder = Path.Combine(sc.EtcRoot, "backups");
            string latestFullName;
            DateTime latestDT;
            int latestSize;
            Helpers.FindLatestBackup(expFolder, out latestFullName, out latestDT, out latestSize);

            if (latestFullName == null) return "n/a";
            string res = Helpers.GetDTString(latestDT);
            res += " • ";
            res += Helpers.GetSizeString(latestSize);
            return res;
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
