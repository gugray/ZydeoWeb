using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using MySql.Data.MySqlClient;

namespace ZDO.CHSite.Logic
{
    public class Auth
    {
        [Flags]
        public enum CreateUserResult
        {
            OK = 0,
            EmailExists = 0x1,
            UserNameExists = 0x2,
        }

        private class SessionInfo
        {
            public readonly int UserId;
            public readonly string UserName;
            public DateTime Expires;
            public SessionInfo(int userId, string userName)
            {
                UserId = userId;
                UserName = userName;
                Expires = DateTime.MinValue;
            }
        }

        /// <summary>
        /// My own logger.
        /// </summary>
        private readonly ILogger logger;

        private readonly int sessionTimeoutMinutes;

        private readonly Dictionary<string, SessionInfo> sessions = new Dictionary<string, SessionInfo>();

        public Auth(ILoggerFactory lf, IConfiguration config)
        {
            if (lf != null) logger = lf.CreateLogger(GetType().FullName);
            else logger = new DummyLogger();
            sessionTimeoutMinutes = int.Parse(config["sessionTimeoutMinutes"]);
        }

        private static string retrieveToken(IHeaderDictionary hdr)
        {
            string hdrAuth = hdr["Authorization"].ToString(); ;
            if (hdrAuth != null)
            {
                string token = hdrAuth.Replace("Bearer", "").Trim();
                if (token.Length > 0) return token;
            }
            return null;
        }

        public void Logout(IHeaderDictionary hdrReq)
        {
            string token = retrieveToken(hdrReq);
            lock (sessions)
            {
                if (sessions.ContainsKey(token)) sessions.Remove(token);
            }
        }

        public void CheckSession(IHeaderDictionary hdrReq, out int userId, out string userName)
        {
            userId = -1; userName = null;
            string token = retrieveToken(hdrReq);
            if (token == null) return;
            lock (sessions)
            {
                if (!sessions.ContainsKey(token)) return;
                SessionInfo si = sessions[token];
                if (si.Expires < DateTime.UtcNow)
                {
                    sessions.Remove(token);
                    return;
                }
                si.Expires = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes);
                userId = si.UserId;
                userName = si.UserName;
            }
        }

        public string Login(string email, string pass)
        {
            string dbHash = null;
            string dbSalt = null;
            string userName = null;
            int userId = -1;
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmd = DB.GetCmd(conn, "SelUserByEmail"))
            {
                cmd.Parameters["@email"].Value = email.ToLowerInvariant().Trim();
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        // TO-DO: re-enable, soon as we got verification mails in place.
                        //if (rdr.GetInt16("status") != 0) continue;
                        dbHash = rdr.GetString("pass_hash");
                        dbSalt = rdr.GetString("pass_salt");
                        userName = rdr.GetString("user_name");
                        userId = rdr.GetInt32("id");
                    }
                }
            }
            // Email not in DB, or not active, verified etc.
            if (dbHash == null) return null;
            // Hash submitted password with user's very own salt. Verify.
            string reqHash = getHash(pass + dbSalt);
            if (reqHash != dbHash) return null;
            // Great. Token is a new random string. Add to sessions, return.
            string token = getRandomString();
            SessionInfo si = new SessionInfo(userId, userName);
            si.Expires = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes);
            lock (sessions)
            {
                sessions[token] = si;
            }
            return token;
        }

        private static string getHash(string text)
        {
            // SHA512 is disposable by inheritance.  
            using (var sha256 = SHA256.Create())
            {
                // Send a sample text to hash.  
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                // Get the hashed string.  
                return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
            }
        }

        private static string getRandomString()
        {
            using (var keyGenerator = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[128 / 8];
                keyGenerator.GetBytes(bytes);
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        public void TriggerPasswordReset(string email)
        {
            email = email.ToLowerInvariant().Trim();
            // TO-DO
        }

        public CreateUserResult CreateUser(string email, string userName, string pass)
        {
            email = email.ToLowerInvariant().Trim();
            // Salt password, hash
            // http://www.c-sharpcorner.com/article/hashing-passwords-in-net-core-with-tips/
            // https://crackstation.net/hashing-security.htm
            string salt = getRandomString();
            string hash = getHash(pass + salt);

            CreateUserResult res = CreateUserResult.OK;
            int count;
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand ins = DB.GetCmd(conn, "InsNewUser"))
            using (MySqlCommand sel1 = DB.GetCmd(conn, "SelUserByName"))
            using (MySqlCommand sel2 = DB.GetCmd(conn, "SelUserByEmail"))
            using (MySqlTransaction trans = conn.BeginTransaction())
            {
                ins.Parameters["@email"].Value = email;
                ins.Parameters["@user_name"].Value = userName;
                ins.Parameters["@pass_hash"].Value = hash;
                ins.Parameters["@pass_salt"].Value = salt;
                ins.Parameters["@registered"].Value = DateTime.UtcNow;
                ins.ExecuteNonQuery();
                count = 0;
                sel1.Parameters["@user_name"].Value = userName;
                using (var rdr = sel1.ExecuteReader()) { while (rdr.Read()) ++count; }
                if (count > 1) res |= CreateUserResult.UserNameExists;
                count = 0;
                sel2.Parameters["@email"].Value = email;
                using (var rdr = sel2.ExecuteReader()) { while (rdr.Read()) ++count; }
                if (count > 1) res |= CreateUserResult.EmailExists;
                if (res == 0) trans.Commit();
                else trans.Rollback();
            }
            return res;
        }
    }
}
