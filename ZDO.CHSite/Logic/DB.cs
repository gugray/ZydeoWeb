using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

using ZD.Common;

namespace ZDO.CHSite.Logic
{
    public class DB
    {
        /// <summary>
        /// One pre-processed script from DB.Scripts.txt.
        /// </summary>
        private class Command
        {
            /// <summary>
            /// SQL command.
            /// </summary>
            public string Sql;
            /// <summary>
            /// The command's parameters and their types.
            /// </summary>
            public Dictionary<string, MySqlDbType> Params = new Dictionary<string, MySqlDbType>();
        }

        /// <summary>
        /// Logger.
        /// </summary>
        private static ILogger logger;

        /// <summary>
        /// Pre-processed scripts.
        /// </summary>
        private static readonly Dictionary<string, Command> cmdDict = new Dictionary<string, Command>();

        /// <summary>
        /// Connection string for new connections.
        /// </summary>
        private static string connectionString;

        /// <summary>
        /// Pre-processes scripts from DB.Scripts.txt.
        /// Builds and stores connection string from site config.
        /// </summary>
        public static void Init(string server, uint port, string db, string user, string pass, ILogger logger)
        {
            DB.logger = logger;
            // Build connection string. Comes from Private.config
            MySqlConnectionStringBuilder csb = new MySqlConnectionStringBuilder();
            csb.Server = server;
            csb.Port = port;
            csb.Database = db;
            csb.UserID = user;
            csb.Password = pass;
            csb.Pooling = true;
            csb.IgnorePrepare = false;
            csb.CharacterSet = "utf8";
            csb.SslMode = MySqlSslMode.None; // SSL currently not supported in .NET Core library
            connectionString = csb.GetConnectionString(true);

            // Parse embedded resource with scipts.
            Command cmd = null;
            string cmdName = null;
            Assembly a = typeof(DB).GetTypeInfo().Assembly;
            string fileName = "ZDO.CHSite.Logic.DB.Scripts.txt";
            using (Stream s = a.GetManifestResourceStream(fileName))
            using (StreamReader sr = new StreamReader(s))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("# Script"))
                    {
                        cmdName = line.Substring(9);
                        cmd = new Command();
                    }
                    else if (line.StartsWith("# @"))
                    {
                        string[] parts = line.Substring(2).Split(' ');
                        MySqlDbType dbType;
                        if (parts[1] == "BLOB") dbType = MySqlDbType.Blob;
                        else if (parts[1] == "TINYINT") dbType = MySqlDbType.Byte;
                        else if (parts[1] == "VARCHAR") dbType = MySqlDbType.VarChar;
                        else if (parts[1] == "DATETIME") dbType = MySqlDbType.DateTime;
                        else if (parts[1] == "INT") dbType = MySqlDbType.Int32;
                        else if (parts[1] == "BIGINT") dbType = MySqlDbType.Int64;
                        else throw new Exception("Forgotten field type: " + parts[1]);
                        cmd.Params[parts[0]] = dbType;
                    }
                    else if (line.StartsWith("# End"))
                    {
                        cmdDict[cmdName] = cmd;
                        cmd = null;
                        cmdName = null;
                    }
                    else if (!line.StartsWith("#"))
                    {
                        if (cmd != null) cmd.Sql += line + "\r\n";
                    }
                }
            }
        }

        /// <summary>
        /// Opens a DB connection.
        /// </summary>
        /// <returns></returns>
        public static MySqlConnection GetConn()
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            try { conn.Open(); }
            catch { conn.Dispose(); throw; }
            return conn;
        }

        /// <summary>
        /// Gets a command by instantiating a script from DB.Scripts.txt.
        /// </summary>
        public static MySqlCommand GetCmd(MySqlConnection conn, string cmdName)
        {
            Command cmd = cmdDict[cmdName];
            MySqlCommand res = new MySqlCommand(cmd.Sql, conn);
            foreach (var x in cmd.Params) res.Parameters.Add(x.Key, x.Value);
            if (res.Parameters.Count != 0) res.Prepare();
            return res;
        } 

        /// <summary>
        /// Verifies DB model so it matches current application version; throws if there's a mismatch.
        /// </summary>
        public static void VerifyVersion(string appVersion)
        {
            string dbModel = "n/a";
            using (var conn = GetConn())
            using (var cmd = new MySqlCommand("SELECT value FROM sys_params WHERE xkey='db_model';", conn))
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read()) dbModel= rdr.GetString(0);
            }
            if (dbModel != appVersion)
            {
                throw new Exception("DB model is " + dbModel + "; it does not match app version, which is " + appVersion);
            }
        } 

        /// <summary>
        /// Creates tables in the DB.
        /// </summary>
        public static void CreateTables()
        {
            MySqlConnection conn = null;
            MySqlCommand cmd = null;
            try
            {
                conn = GetConn();
                cmd = GetCmd(conn, "CreateDB");
                cmd.ExecuteNonQuery();
                cmd.Dispose(); cmd = null;
            }
            finally
            {
                if (cmd != null) cmd.Dispose();
                if (conn != null)
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }
    }
}