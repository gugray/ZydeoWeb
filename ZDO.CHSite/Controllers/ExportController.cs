using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

using ZDO.CHSite.Logic;
using ZD.Common;
using ZD.LangUtils;
using ZDO.CHSite.Entities;

namespace ZDO.CHSite.Controllers
{
    public class ExportController : Controller
    {
        private readonly IConfiguration config;
        private readonly ILogger logger;
        private readonly SqlDict dict;
        private readonly Mutation mut;
        private static Thread thread = null;
        private static object lockObj = new object();

        public ExportController(IConfiguration config, ILogger<ExportController> logger, SqlDict dict)
        {
            this.config = config;
            mut = config["MUTATION"] == "HDD" ? Mutation.HDD : Mutation.CHD;
            this.logger = logger;
            this.dict = dict;
        }

        public IActionResult DownloadInfo()
        {
            string fileName = config["exportFileNameRaw"] + ".gz";
            string filePath = Path.Combine(config["exportFolder"], fileName);
            FileInfo fi = new FileInfo(filePath);
            DateTime dt = fi.LastWriteTimeUtc.ToLocalTime();
            string strDate = dt.Year + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00") + "T";
            strDate += dt.Hour.ToString("00") + ":" + dt.Minute.ToString("00") + ":" + dt.Second.ToString("00") + "Z";
            string sizeStr = "{0:#,0}";
            sizeStr = string.Format(sizeStr, fi.Length);
            DownloadInfo res = new DownloadInfo
            {
                FileName = fileName,
                Timestamp = strDate,
                Size = sizeStr,
            };
            return new ObjectResult(res);
        }

        public FileResult Download()
        {
            string fileName = config["exportFileNameRaw"] + ".gz";
            string filePath = Path.Combine(config["exportFolder"], fileName);
            FileInfo fi = new FileInfo(filePath);
            return PhysicalFile(fi.FullName, "application/gzip", fileName);
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
            string exportFileName = Path.Combine(config["exportFolder"], config["exportFileNameRaw"]);
            bool fileUpToDate = false;
            if (System.IO.File.Exists(exportFileName))
            {
                FileInfo fi = new FileInfo(exportFileName);
                DateTime dtFile = fi.LastWriteTimeUtc;
                DateTime dtData = SqlDict.GetLatestChangeUtc();
                fileUpToDate = dtFile > dtData;
            }
            if (fileUpToDate) return;

            using (FileStream fs = new FileStream(exportFileName, FileMode.Create, FileAccess.ReadWrite))
            using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
            using (SqlDict.Exporter exporter = new SqlDict.Exporter())
            {
                // #20 Dictionary export mixes \r\n and \n
                sw.NewLine = "\n";
                writePrologue(sw);
                EntryBlockWriter ebw = new EntryBlockWriter(sw);
                while (true)
                {
                    List<EntryVersion> history;
                    int entryId;
                    exporter.GetNext(out history, out entryId);
                    if (history == null) break;
                    //// DBG: reduced export
                    //DateTime limit = new DateTime(2016, 11, 1);
                    //if (history[history.Count - 1].Timestamp < limit) continue;
                    ebw.WriteBlock(entryId, history);
                }
            }
        }

        private void writePrologue(StreamWriter sw)
        {
            string strPrologue;
            Assembly a = typeof(ExportController).GetTypeInfo().Assembly;
            string fileName = "ZDO.CHSite.files.other.export-prologue-{mut}.txt";
            if (mut == Mutation.CHD) fileName = fileName.Replace("{mut}", "chd");
            else fileName = fileName.Replace("{mut}", "hdd");
            using (Stream s = a.GetManifestResourceStream(fileName))
            using (StreamReader sr = new StreamReader(s))
            {
                strPrologue = sr.ReadToEnd();
            }
            DateTime dt = DateTime.UtcNow;
            string strDate = dt.Year + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00") + "T";
            strDate += dt.Hour.ToString("00") + ":" + dt.Minute.ToString("00") + ":" + dt.Second.ToString("00") + "Z";
            strPrologue = string.Format(strPrologue, strDate);
            // Extra sanity check: we want this export to have only \n as line breaks
            // #20 Dictionary export mixes \r\n and \n
            strPrologue = strPrologue.Replace("\r\n", "\n");
            sw.WriteLine(strPrologue);
        }

        private void doShellStuff()
        {
            string stdout, stderr, err;
            // Working folder - just for safety's sake
            ShellHelper.ExecWorkingDir = config["workingFolder"];
            // GZIP our export
            string exportFileName = Path.Combine(config["exportFolder"], config["exportFileNameRaw"]);
            err = ShellHelper.Exec("gzip", "-k -f " + exportFileName, out stdout, out stderr);
            if (err != null)
            {
                string msg = "Failed to gzip export: {0}\nSTDOUT: {1}\nSTDERR: {2}";
                msg = string.Format(msg, err, stdout, stderr);
                logger.LogError(msg);
                return;
            }
            // If we have Dropbox uploader specified, upload
            string dbuploader = config["dropboxUploader"];
            if (!string.IsNullOrEmpty(dbuploader))
            {
                string dropName = config["dropboxFolder"] + "/" + config["exportFileNameRaw"] + ".gz";
                string uploaderConfig = config["dropboxUploaderConfig"];
                err = ShellHelper.Exec(dbuploader, "-f " + uploaderConfig + " upload " +
                    exportFileName + ".gz " + dropName, out stdout, out stderr);
                if (err != null)
                {
                    string msg = "Failed to upload to Dropbox: {0}\nSTDOUT: {1}\nSTDERR: {2}";
                    msg = string.Format(msg, err, stdout, stderr);
                    logger.LogError(msg);
                }
            }
            // If we have a Git folder specified, copy export there; stage-commit-push
            string gitCloneFolder = config["gitCloneFolder"];
            if (!string.IsNullOrEmpty(gitCloneFolder))
            {
                while (true) // Sorry for the GOTO & thanks for all the fish
                {
                    // Copy
                    err = ShellHelper.Exec("cp", exportFileName + " " + gitCloneFolder, out stdout, out stderr);
                    if (err != null) { logger.LogError("Failed to copy export to Git clone folder: " + err); break; }
                    // For all the Git stuff, working folder must be Git clone
                    ShellHelper.ExecWorkingDir = gitCloneFolder;
                    // Stage
                    err = ShellHelper.Exec("git", "add .", out stdout, out stderr);
                    if (err != null)
                    {
                        string msg = "Git add failed: {0}\nSTDOUT: {1}\nSTDERR: {2}";
                        msg = string.Format(msg, err, stdout, stderr);
                        logger.LogError(msg);
                        break;
                    }
                    // Commit with message
                    string commitMessage = config["gitCommitMessage"];
                    err = ShellHelper.Exec("git", "commit -m \"" + commitMessage + "\"", out stdout, out stderr);
                    if (err != null)
                    {
                        string msg = "Git commit failed: {0}\nSTDOUT: {1}\nSTDERR: {2}";
                        msg = string.Format(msg, err, stdout, stderr);
                        logger.LogError(msg);
                        break;
                    }
                    // Push
                    err = ShellHelper.Exec("git", "push", out stdout, out stderr);
                    if (err != null)
                    {
                        string msg = "Git push failed: {0}\nSTDOUT: {1}\nSTDERR: {2}";
                        msg = string.Format(msg, err, stdout, stderr);
                        logger.LogError(msg);
                        break;
                    }
                    // Done.
                    break;
                }
            }
        }

        private void exportFun()
        {
            try
            {
                doExport();
                doShellStuff();
            }
            catch (Exception ex) { logger.LogError(new EventId(), ex, "Dictionary export failed."); }
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
