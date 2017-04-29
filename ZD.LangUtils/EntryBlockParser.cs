using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using ZD.Common;

namespace ZD.LangUtils
{
    public class EntryBlockParser
    {
        private readonly CedictParser parser = new CedictParser();
        private readonly StreamReader sr;

        public EntryBlockParser(StreamReader sr)
        {
            this.sr = sr;
        }

        /// <summary>
        /// Reads a block from stream with versioned entries.
        /// </summary>
        /// <param name="vers">Receives versions; oldest first.</param>
        /// <returns>Entry ID if succeeded, -1 if input is over.</returns>
        public int ReadBlock(List<EntryVersion> vers)
        {
            int id = -1;
            EntryVersion currVersion = null;
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                // DBG
                // TO-DO: remove; deal with markers
                line = line.Replace("|", "");

                // Still looking for block stating ID
                if (id == -1)
                {
                    // Skip all lines that are *not* an ID
                    if (!line.StartsWith("# ID-")) continue;
                    line = line.Substring(5);
                    id = EntryId.StringToId(line);
                }
                // We're inside block
                else
                {
                    // Not a comment: this is block's last line, with current entry
                    if (!line.StartsWith("#"))
                    {
                        currVersion.Entry = parser.ParseEntry(line, -1, null);
                        vers.Add(currVersion);
                        // Forward-propagate unchanged entries (null at this point)
                        for (int i = 1; i < vers.Count; ++i) if (vers[i].Entry == null) vers[i].Entry = vers[i - 1].Entry;
                        return id;
                    }
                    // Commented lines are either version declarations, or past forms of entries following a version declaration
                    // Entries are omitted if they didn't change from previous version
                    if (line.StartsWith("# Ver"))
                    {
                        if (currVersion != null) vers.Add(currVersion);
                        currVersion = parseVersion(line);
                    }
                    else
                    {
                        line = line.Substring(2);
                        currVersion.Entry = parser.ParseEntry(line, -1, null);
                        vers.Add(currVersion);
                        currVersion = null;
                    }
                }
            }
            // Forward-propagate unchanged entries (null at this point)
            for (int i = 1; i < vers.Count; ++i) if (vers[i].Entry == null) vers[i].Entry = vers[i - 1].Entry;
            // Done.
            return id;
        }

        private Regex reVer = new Regex(@"^# Ver ([^ ]+) ([^ ]+) Stat\-([^ ]+) (\d*>)(.+)$");
        private Regex reDate = new Regex(@"^([\d]{4})\-([\d]{2})\-([\d]{2})T([\d]{2}):([\d]{2}):([\d]{2})Z$");

        // Parses a version declaration from line within block.
        private EntryVersion parseVersion(string line)
        {
            // Resolve declaration overall
            Match m = reVer.Match(line);
            if (!m.Success) throw new Exception("Invalid version declaration in block.");
            string dateStr = m.Groups[1].Value;
            string user = m.Groups[2].Value;
            string statStr = m.Groups[3].Value;
            string cmtIntro = m.Groups[4].Value;
            string cmt = m.Groups[5].Value;
            cmt = unesc(cmt);
            // Resolve UTC timestamp
            m = reDate.Match(dateStr);
            if (!m.Success) throw new Exception("Invalid date format: " + dateStr);
            DateTime date = new DateTime(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value),
                int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value),
                DateTimeKind.Utc);
            // Resolve status string
            EntryStatus status;
            if (statStr == "New") status = EntryStatus.Neutral;
            else if (statStr == "Verif") status = EntryStatus.Approved;
            else if (statStr == "Flagged") status = EntryStatus.Flagged;
            else throw new Exception("Invalid entry status: " + statStr);
            // Comment intro: does it have bulk ID?
            int bulkId = -1;
            if (cmtIntro.Length > 1) bulkId = int.Parse(cmtIntro.Substring(0, cmtIntro.Length - 1));
            // Put it all together
            return new EntryVersion
            {
                Timestamp = date,
                User = user,
                Status = status,
                BulkRef = bulkId,
                Comment = cmt,
            };
        }

        private static readonly string pua = (char)0xe000 + "";

        private string unesc(string str)
        {
            if (!str.Contains(@"\")) return str;
            str = str.Replace(@"\\", pua);
            str = str.Replace(@"\n", "\n");
            str = str.Replace(pua, @"\");
            return str;
        }
    }
}

