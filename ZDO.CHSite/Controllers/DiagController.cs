using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;

namespace ZDO.CHSite.Controllers
{
    public class DiagController : Controller
    {
        private readonly string workingFolder;
        private readonly SqlDict dict;

        public DiagController(IConfiguration config, SqlDict dict)
        {
            workingFolder = config["workingFolder"];
            this.dict = dict;
        }

        public IActionResult RecreateDB()
        {
            DB.CreateTables();
            dict.ReloadIndex();
            return StatusCode(200, "Database tables created.");
        }

        private static int indexLineCount;

        public IActionResult IndexHDD()
        {
            ThreadPool.QueueUserWorkItem(funIndexHDD);
            return StatusCode(200, "Indexing started.");
        }

        public class IndexProgress
        {
            public string Progress;
            public bool Done;
        }

        public IActionResult GetIndexingProgress()
        {
            string progress;
            if (indexLineCount > 0)
            {
                progress = "Working, {0} lines processed.";
                progress = string.Format(progress, indexLineCount);
            }
            else
            {
                progress = "Done: {0} lines.";
                progress = string.Format(progress, -indexLineCount);
            }
            IndexProgress res = new IndexProgress { Progress = progress, Done = indexLineCount < 0 };
            return new ObjectResult(res);
        }

        private void funIndexHDD(object o)
        {
            indexLineCount = 0;
            string hddPath = "files/data/handedict.txt";
            using (SqlDict.BulkBuilder imp = dict.GetBulkBuilder(workingFolder, 0, "Importing stuff.", false))
            using (FileStream fs = new FileStream(hddPath, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#")) continue;
                    imp.AddEntry(line);
                    ++indexLineCount;
                }
                imp.CommitRest();
            }
            indexLineCount = -indexLineCount;
        }
    }
}
