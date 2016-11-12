/*

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

        private static int indexLineCount;
        private static int freqLineCount;

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

        public IActionResult IndexHDD()
        {
            ThreadPool.QueueUserWorkItem(funIndexHDD);
            return StatusCode(200, "Indexing started.");
        }

        public IActionResult ImportFreq()
        {
            ThreadPool.QueueUserWorkItem(funImportFreq);
            return StatusCode(200, "Import started.");
        }

        public class ProgressRes
        {
            public string Progress;
            public bool Done;
        }

        public IActionResult GetFreqProgress()
        {
            string progress;
            if (freqLineCount > 0)
            {
                progress = "Working, {0} word imported.";
                progress = string.Format(progress, freqLineCount);
            }
            else
            {
                progress = "Done: {0} words.";
                progress = string.Format(progress, -freqLineCount);
            }
            ProgressRes res = new ProgressRes { Progress = progress, Done = freqLineCount < 0 };
            return new ObjectResult(res);
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
            ProgressRes res = new ProgressRes { Progress = progress, Done = indexLineCount < 0 };
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

        private void funImportFreq(object o)
        {
            freqLineCount = 0;
            string freqPath = "files/data/subtlex-ch.txt";
            using (SqlDict.Freq freq = new SqlDict.Freq())
            using (FileStream fs = new FileStream(freqPath, FileMode.Open, FileAccess.Read))
            using (StreamReader sr = new StreamReader(fs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length != 2) continue;
                    freq.StoreFreq(parts[0], int.Parse(parts[1]));
                    ++freqLineCount;
                }
                freq.CommitRest();
            }
            freqLineCount = -freqLineCount;
        }
    }
}

*/
