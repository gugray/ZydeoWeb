using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace ZDO.Console.Logic
{
    public class Task
    {
        private readonly string shortName;
        private readonly Options opt;
        private readonly SiteConfig sconf;
        public string StatusMsg { get; private set; }
        public bool Succeeded = false;

        public Task(string shortName, string cmd, Options opt)
        {
            this.shortName = shortName;
            this.opt = opt;
            foreach (var x in opt.Sites)
                if (x.ShortName == shortName) sconf = x;
            StatusMsg = "Starting task [" + cmd + "]";
            ThreadPool.QueueUserWorkItem(fun, cmd);
        }

        private void fun(object o)
        {
            string cmd = (string)o;
            try
            {
                if (cmd == "startService") funService(true);
                else if (cmd == "stopService") funService(false);
                else if (cmd == "deployApp") funDeployApp();
                else if (cmd == "resetDB") funRecreateDB();
                else if (cmd == "importFreq") funImportFreq();
                else if (cmd == "importDict") funImportDict();
                else if (cmd == "backupDB") funBackupDB();
                else if (cmd == "dumpCleanup") funDumpCleanup();
                else if (cmd == "fetchLatestBackup") funFetchBackup();
                else if (cmd == "fetchAppLog") funFetchLog(true);
                else if (cmd == "fetchQueryLog") funFetchLog(false);
                else
                {
                    string cmdStr = cmd == null ? "null" : cmd;
                    throw new Exception("Unrecognized command: " + cmdStr);
                }
            }
            catch (Exception ex)
            {
                StatusMsg = "Unexpected error\n" + ex.ToString();
            }
            finally
            {
                lock (ApiController.TaskLock)
                {
                    ApiController.LastTask = this;
                    ApiController.RunningTask = null;
                }
            }
        }
        
        private void msgOutErr(string stdout, string stderr, string err)
        {
            if (!string.IsNullOrEmpty(err)) StatusMsg += "\nError:\n" + err;
            if (stdout != "") StatusMsg += "\nStdout:\n" + stdout;
            if (stderr != "") StatusMsg += "\nStderr:\n" + stderr;
        }

        private void funService(bool start)
        {
            if (start) StatusMsg = "Starting service: " + shortName;
            else StatusMsg = "Stopping service: " + shortName;
            string stdout, stderr;
            string err = Helpers.Exec(sconf.SrvScript, start ? "start" : "stop", out stdout, out stderr);
            Succeeded = err == null;
            StatusMsg = "Finished.";
            msgOutErr(stdout, stderr, err);
        }

        private void funFetchLog(bool appLog)
        {
            string stdout, stderr, err;
            StatusMsg = "Preparing to fetch log...";

            var cfg = Helpers.GetAppConfig(sconf.EtcRoot);
            string src = appLog ? cfg["logFileName"] : cfg["queryLogFileName"];
            string fn = Path.GetFileName(src);
            string trg = Path.Combine(opt.WarehousePath, fn);

            StatusMsg = "Copying " + fn + " ...";
            string args = src + " " + trg;
            err = Helpers.Exec("cp", args, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to copy file.";
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = "Compressing in warehouse...";
            err = Helpers.Exec("gzip", trg, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to compress in warehouse.";
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = appLog ? "App log is in warehouse folder." : "Query log is in warehouse folder.";
            Succeeded = true;
        }

        private void funFetchBackup()
        {
            string stdout, stderr, err;
            StatusMsg = "Preparing to fetch latest backup...";

            string expFolder = Path.Combine(sconf.EtcRoot, "backups");
            string latestFullName;
            DateTime latestDT;
            int latestSize;
            Helpers.FindLatestBackup(expFolder, out latestFullName, out latestDT, out latestSize);
            if (latestFullName == null)
            {
                StatusMsg = "No backup found.";
                return;
            }
            StatusMsg = "Copying...";
            string args = latestFullName + " " + opt.WarehousePath;
            err = Helpers.Exec("cp", args, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to copy file.";
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = "Latest backup is in warehouse folder.";
            Succeeded = true;
        }

        private void funDumpCleanup()
        {
            string stdout, stderr, err;
            StatusMsg = "Preparing to remove excess backup files...";

            // Enumerate dump files
            string expFolder = Path.Combine(sconf.EtcRoot, "backups");
            DirectoryInfo di = new DirectoryInfo(expFolder);
            var fis = di.EnumerateFiles();
            List<FileInfo> lst = new List<FileInfo>();
            Regex reDump = new Regex(Helpers.BupRegex);
            foreach (var fi in fis)
            {
                if (!reDump.Match(fi.Name).Success) continue;
                lst.Add(fi);
            }
            lst.Sort((x, y) => x.LastWriteTimeUtc.CompareTo(y.LastWriteTimeUtc));
            // Decide what to keep
            HashSet<string> toKeep = new HashSet<string>();
            if (lst.Count <= 7) foreach (var x in lst) toKeep.Add(x.FullName);
            else
            {
                // Keep first file from each month
                int lastMonthFirst = 100101;
                foreach (var x in lst)
                {
                    int currMonth = x.LastWriteTimeUtc.Year * 100 + x.LastWriteTimeUtc.Month;
                    if (currMonth > lastMonthFirst)
                    {
                        lastMonthFirst = currMonth;
                        toKeep.Add(x.FullName);
                    }
                }
                // Keep latest 7 files
                for (int i = 0; i != 7 && lst.Count - i - 1 >= 0; ++i) toKeep.Add(lst[lst.Count - i - 1].FullName);
            }

            // Delete all non-keepers
            StatusMsg = "Removing excess files...";
            foreach (var x in lst)
            {
                if (toKeep.Contains(x.FullName)) continue;
                err = Helpers.Exec("rm", x.FullName, out stdout, out stderr);
                if (err != null)
                {
                    StatusMsg = "Failed to remove file: " + x.FullName;
                    msgOutErr(stdout, stderr, err);
                    return;
                }
            }

            // Donez.
            StatusMsg = "Done removing excess DB dumps.";
            Succeeded = true;
        }

        private void funBackupDB()
        {
            string stdout, stderr, err;
            StatusMsg = "Preparing to back up database...";

            // Instance's DB connection: well, usr/pass/db name
            // We're assuming localhost otherwise: mysqldump command
            var cfg = Helpers.GetAppConfig(sconf.EtcRoot);
            string args = cfg["dbDatabase"] + " --single-transaction --user=" + cfg["dbUserID"] + " --password=" + cfg["dbPass"];
            DateTime dt = DateTime.UtcNow;
            string dumpName = "db-dump-" + dt.Year + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00") + "T";
            dumpName += dt.Hour.ToString("00") + "-" + dt.Minute.ToString("00") + "-" + dt.Second.ToString("00") + "Z";
            dumpName += ".sql";
            string dumpDir = Path.Combine(sconf.EtcRoot, "backups");
            dumpName = Path.Combine(dumpDir, dumpName);
            args += " --result-file=" + dumpName;

            StatusMsg = "Performing backup...";
            err = Helpers.Exec("mysqldump", args, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to back up database";
                msgOutErr(stdout, stderr, err);
                return;
            }

            StatusMsg = "Compressing dump...";
            err = Helpers.Exec("gzip", dumpName, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to back up database";
                msgOutErr(stdout, stderr, err);
                return;
            }

            StatusMsg = "Copying to db-dump.sql.gz...";
            args = dumpName + ".gz " + Path.Combine(dumpDir, "db-dump.sql.gz");
            err = Helpers.Exec("cp", args, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to back up database";
                msgOutErr(stdout, stderr, err);
                return;
            }

            StatusMsg = "Backup complete.";
            Succeeded = true;
        }

        private void funImportDict()
        {
            string stdout, stderr, err;
            StatusMsg = "Preparing to import dictionary...";
            if (Helpers.IsSrvRunning(Path.Combine(sconf.AppRoot, "service/service.pid")))
            {
                StatusMsg = "Aborted because service is running.";
                return;
            }

            string dictFile = Path.Combine(opt.WarehousePath, "handedict.txt");
            if (!File.Exists(dictFile))
            {
                StatusMsg = "Aborted because dict file does not exist: " + dictFile;
                return;
            }

            string appDll = Path.Combine(sconf.AppRoot, "app/ZDO.CHSite.dll");
            Dictionary<string, string> env = new Dictionary<string, string>();
            env["MUTATION"] = sconf.Mutation;
            if (sconf.StagingOf != "") env["ASPNETCORE_ENVIRONMENT"] = "Staging";
            else env["ASPNETCORE_ENVIRONMENT"] = "Production";
            string args = appDll + " --task import-dict " + dictFile + " " + opt.WarehousePath;

            StatusMsg = "Importing dictionary...";
            err = Helpers.Exec("dotnet", args, out stdout, out stderr, env);
            if (err != null)
            {
                StatusMsg = "Failed to import dictionary.";
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = "Dictionary imported successfully.";
            Succeeded = true;
        }

        private void funImportFreq()
        {
            string stdout, stderr, err;
            StatusMsg = "Preparing to import word frequencies...";
            if (Helpers.IsSrvRunning(Path.Combine(sconf.AppRoot, "service/service.pid")))
            {
                StatusMsg = "Aborted because service is running.";
                return;
            }

            string freqFile = Path.Combine(opt.WarehousePath, "subtlex-ch.txt");
            if (!File.Exists(freqFile))
            {
                StatusMsg = "Aborted because freq file does not exist: " + freqFile;
                return;
            }

            string appDll = Path.Combine(sconf.AppRoot, "app/ZDO.CHSite.dll");
            Dictionary<string, string> env = new Dictionary<string, string>();
            env["MUTATION"] = sconf.Mutation;
            if (sconf.StagingOf != "") env["ASPNETCORE_ENVIRONMENT"] = "Staging";
            else env["ASPNETCORE_ENVIRONMENT"] = "Production";

            StatusMsg = "Importing word frequencies...";
            err = Helpers.Exec("dotnet", appDll + " --task import-freq " + freqFile, out stdout, out stderr, env);
            if (err != null)
            {
                StatusMsg = "Failed to import word frequencies.";
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = "Word frequencies imported successfully.";
            Succeeded = true;
        }

        private void funRecreateDB()
        {
            string stdout, stderr, err;
            StatusMsg = "Preparing to recreate DB...";
            if (Helpers.IsSrvRunning(Path.Combine(sconf.AppRoot, "service/service.pid")))
            {
                StatusMsg = "Aborted because service is running.";
                return;
            }

            string appDll = Path.Combine(sconf.AppRoot, "app/ZDO.CHSite.dll");
            Dictionary<string, string> env = new Dictionary<string, string>();
            env["MUTATION"] = sconf.Mutation;
            if (sconf.StagingOf != "") env["ASPNETCORE_ENVIRONMENT"] = "Staging";
            else env["ASPNETCORE_ENVIRONMENT"] = "Production";
            err = Helpers.Exec("dotnet", appDll + " --task recreate-db", out stdout, out stderr, env);
            if (err != null)
            {
                StatusMsg = "Failed to recreate DB.";
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = "DB reset successfully.";
            Succeeded = true;
        }

        private void funDeployApp()
        {
            Helpers.ExecWorkingDir = opt.WarehousePath;
            string stdout, stderr, err;
            StatusMsg = "Preparing to deploy app...";
            if (Helpers.IsSrvRunning(Path.Combine(sconf.AppRoot, "service/service.pid")))
            {
                StatusMsg = "Aborted because service is running.";
                return;
            }
            string tarFile = Path.Combine(opt.WarehousePath, "chsite.tar.gz");
            if (!File.Exists(tarFile))
            {
                StatusMsg = "Aborted because app archive does not exist: " + tarFile;
                return;
            }
            string appDir = Path.Combine(sconf.AppRoot, "app");
            if (!appDir.StartsWith("/opt/zdo"))
            {
                StatusMsg = "Aborting because app directory looks odd: " + appDir;
                return;
            }
            if (Directory.Exists(appDir))
            {
                err = Helpers.Exec("rm", "-rf " + appDir, out stdout, out stderr);
                if (err != null)
                {
                    StatusMsg = "Failed to remove old app directory: " + appDir;
                    msgOutErr(stdout, stderr, err);
                    return;
                }
            }
            err = Helpers.Exec("mkdir", appDir, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to create new app directory: " + appDir;
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = "Copying archive to app directory...";
            err = Helpers.Exec("cp", tarFile + " " + appDir, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to copy archive to app directory";
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = "Extracting archive...";
            err = Helpers.Exec("tar", " -xvzf " + Path.Combine(appDir, "chsite.tar.gz") + " -C " + appDir, out stdout, out stderr);
            if (err != null)
            {
                StatusMsg = "Failed to extract archive";
                msgOutErr(stdout, stderr, err);
                return;
            }
            StatusMsg = "Application deployed successfully.";
            Succeeded = true;
        }
    }
}
