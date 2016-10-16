using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

using ZD.Common;

namespace ZDO.CHSite.Logic
{
    public partial class SqlDict
    {
        public class HeadAndTrg
        {
            public readonly string Head;
            public readonly string Trg;
            public HeadAndTrg(string head, string trg)
            {
                Head = head;
                Trg = trg;
            }
        }

        public static bool DoesHeadExist(string head)
        {
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "SelCountHead"))
            {
                cmd.Parameters["@hw"].Value = head;
                Int64 count = (Int64)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        public static List<HeadAndTrg> GetEntriesBySimp(string simp)
        {
            return null;
        }
    }
}