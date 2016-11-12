using System;
using System.Collections.Generic;
using System.Linq;
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
