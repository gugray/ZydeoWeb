using System;
using System.Collections.Generic;
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

        public enum ConfirmedAction
        {
            Register = 0,
            PassReset = 1,
            ChangeEmail = 2,
            Bad = 9999,
        }

        public class UserInfo
        {
            public int UserId;
            public string UserName;
            public string Email;
            public DateTime Registered;
            public string About;
            public string Location;
        }

        public const string mailChangeOK = "ok";
        public const string mailChangeBadPass = "badpass";
        public const string mailChangeEmailInUse = "emailinuse";

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

        private readonly ILogger logger;
        private readonly Emailer emailer;
        private readonly PageProvider pageProvider;

        private readonly int sessionTimeoutMinutes;

        private readonly Dictionary<string, SessionInfo> sessions = new Dictionary<string, SessionInfo>();

        public Auth(ILoggerFactory lf, IConfiguration config, Emailer emailer, PageProvider pageProvider)
        {
            if (lf != null) logger = lf.CreateLogger(GetType().FullName);
            else logger = new DummyLogger();
            this.emailer = emailer;
            this.pageProvider = pageProvider;
            sessionTimeoutMinutes = int.Parse(config["sessionTimeoutMinutes"]);
        }

        // TO-DO: Busywork

        public void Shutdown()
        {
            // TO-DO
        }

        public bool IsPasswordValid(string pass)
        {
            return pass.Length >= 6;
        }

        public bool IsEmailValid(string email)
        {
            // http://emailregex.com/
            Regex reEmail = new Regex(@"^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$");
            return reEmail.Match(email).Success;
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

        public UserInfo GetUserInfo(int userId)
        {
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand sel = DB.GetCmd(conn, "SelUserById"))
            {
                UserInfo res = null;
                sel.Parameters["@id"].Value = userId;
                using (var rdr = sel.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        object[] vals = new object[16];
                        rdr.GetValues(vals);
                        res = new UserInfo
                        {
                            UserId = userId,
                            UserName = rdr.GetString("user_name"),
                            Email = vals[2] is DBNull ? "" : rdr.GetString(2),
                            Registered = new DateTime(rdr.GetDateTime("registered").Ticks, DateTimeKind.Utc),
                            About = vals[7] is DBNull ? "" : rdr.GetString(7),
                            Location = vals[8] is DBNull ? "" : rdr.GetString(8),
                        };
                    }
                }
                return res;
            }
        }

        public void CheckTokenCode(string code, out ConfirmedAction action, out string data, out int userId)
        {
            action = ConfirmedAction.Bad;
            data = null;
            userId = -1;
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand sel = DB.GetCmd(conn, "SelConfToken"))
            {
                ConfirmedAction dbAction = ConfirmedAction.Bad;
                int dbUserId = -1;
                string dbData = null;
                DateTime dbExpiry = DateTime.MinValue;
                sel.Parameters["@code"].Value = code;
                using (var rdr = sel.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        dbAction = (ConfirmedAction)rdr.GetInt32("action");
                        dbUserId = rdr.GetInt32("user_id");
                        dbExpiry = new DateTime(rdr.GetDateTime("expiry").Ticks, DateTimeKind.Utc);
                        dbData = rdr.GetString("data");
                    }
                }
                // Token not found?
                if (dbUserId == -1) return;
                // Token expired?
                if (dbExpiry < DateTime.UtcNow) return;
                // Yay we're good
                action = dbAction;
                data = dbData;
                userId = dbUserId;
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

        public bool ChangePassword(int userId, string oldPass, string newPass)
        {
            string dbHash = null;
            string dbSalt = null;
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmdSel = DB.GetCmd(conn, "SelUserById"))
            using (MySqlCommand cmdUpd = DB.GetCmd(conn, "UpdatePassword"))
            {
                cmdSel.Parameters["@id"].Value = userId;
                using (var rdr = cmdSel.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        dbHash = rdr.GetString("pass_hash");
                        dbSalt = rdr.GetString("pass_salt");
                    }
                }
                if (dbHash == null || dbSalt == null) throw new Exception("User ID not found, or user deleted.");
                // Provided old password correct?
                string oldHash = getHash(oldPass + dbSalt);
                if (oldHash != dbHash) return false;
                // Store new salt and hash
                string newSalt = getRandomString();
                string newHash = getHash(newPass + newSalt);
                cmdUpd.Parameters["@id"].Value = userId;
                cmdUpd.Parameters["@new_pass_salt"].Value = newSalt;
                cmdUpd.Parameters["@new_pass_hash"].Value = newHash;
                cmdUpd.ExecuteNonQuery();
                return true;
            }
        }

        public void ChangeInfo(int userId, string location, string about)
        {
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmdSel = DB.GetCmd(conn, "SelUserById"))
            using (MySqlCommand cmdUpd = DB.GetCmd(conn, "UpdateUserInfo"))
            {
                cmdSel.Parameters["@id"].Value = userId;
                bool found = false;
                using (var rdr = cmdSel.ExecuteReader())
                {
                    while (rdr.Read()) found = true;
                }
                if (!found) throw new Exception("User ID not found, or user deleted.");
                cmdUpd.Parameters["@id"].Value = userId;
                cmdUpd.Parameters["@new_location"].Value = location;
                cmdUpd.Parameters["@new_about"].Value = about;
                cmdUpd.ExecuteNonQuery();
            }
        }

        public string TriggerChangeEmail(int userId, string pass, string newEmail, string lang)
        {
            newEmail = newEmail.ToLowerInvariant().Trim();
            string dbHash = null;
            string dbSalt = null;
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmdSelById = DB.GetCmd(conn, "SelUserById"))
            using (MySqlCommand cmdSelByEmail = DB.GetCmd(conn, "SelUserByEmail"))
            {
                cmdSelById.Parameters["@id"].Value = userId;
                using (var rdr = cmdSelById.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        dbHash = rdr.GetString("pass_hash");
                        dbSalt = rdr.GetString("pass_salt");
                    }
                }
                if (dbHash == null || dbSalt == null) throw new Exception("User ID not found, or user deleted.");
                // Provided old password correct?
                string hash = getHash(pass + dbSalt);
                if (hash != dbHash) return mailChangeBadPass;
                // Is email in use?
                bool mailInUse = false;
                cmdSelByEmail.Parameters["@email"].Value = newEmail;
                using (var rdr = cmdSelByEmail.ExecuteReader())
                {
                    while (rdr.Read()) mailInUse = true;
                }
                if (mailInUse) return mailChangeEmailInUse;
                // File for verification
                DateTime expiry = DateTime.UtcNow;
                expiry = expiry.AddMinutes(60);
                string code = fileConfToken(conn, userId, expiry, newEmail, (int)ConfirmedAction.ChangeEmail);
                string msg = pageProvider.GetPage(lang, "?/mail.mailchangeconfirm", false).Html;
                msg = string.Format(msg, lang, code);
                emailer.SendMail(newEmail,
                    TextProvider.Instance.GetString(lang, "emails.senderNameHDD"),
                    TextProvider.Instance.GetString(lang, "emails.mailChangeConfirm"),
                    msg, true);
                // We're good.
                return mailChangeOK;
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
                        if (rdr.GetInt16("status") != 0) continue;
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
            string token = null;
            lock (sessions)
            {
                token = getRandomString();
                // Make totally sure token is unique
                while (sessions.ContainsKey(token)) token = getRandomString();
                SessionInfo si = new SessionInfo(userId, userName);
                si.Expires = DateTime.UtcNow.AddMinutes(sessionTimeoutMinutes);
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

        public void TriggerPasswordReset(string email, string lang)
        {
            email = email.ToLowerInvariant().Trim();

            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmdSelByEmail = DB.GetCmd(conn, "SelUserByEmail"))
            {
                // Can we find user by email?
                int userId = -1;
                cmdSelByEmail.Parameters["@email"].Value = email;
                using (var rdr = cmdSelByEmail.ExecuteReader())
                {
                    while (rdr.Read()) userId = rdr.GetInt32("id");
                }
                // No user with this email: silently return
                if (userId == -1) return;
                // File for verification
                DateTime expiry = DateTime.UtcNow;
                expiry = expiry.AddMinutes(60);
                string code = fileConfToken(conn, userId, expiry, null, (int)ConfirmedAction.PassReset);
                string msg = pageProvider.GetPage(lang, "?/mail.passreset", false).Html;
                msg = string.Format(msg, lang, code);
                emailer.SendMail(email,
                    TextProvider.Instance.GetString(lang, "emails.senderNameHDD"),
                    TextProvider.Instance.GetString(lang, "emails.passreset"),
                    msg, true);
            }
        }

        public bool ResetPassword(string pass, string code)
        {
            // OK, token still valid, and is action correct?
            ConfirmedAction action;
            string data;
            int userId;
            CheckTokenCode(code, out action, out data, out userId);
            if (userId < 0 || action != ConfirmedAction.PassReset) return false;
            // Change password
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand cmdUpd = DB.GetCmd(conn, "UpdatePassword"))
            using (MySqlCommand del = DB.GetCmd(conn, "DelConfToken"))
            {
                // Store new salt and hash
                string newSalt = getRandomString();
                string newHash = getHash(pass + newSalt);
                cmdUpd.Parameters["@id"].Value = userId;
                cmdUpd.Parameters["@new_pass_salt"].Value = newSalt;
                cmdUpd.Parameters["@new_pass_hash"].Value = newHash;
                cmdUpd.ExecuteNonQuery();
                // Destroy token
                del.Parameters["@code"].Value = code;
                del.ExecuteNonQuery();
            }
            // We're good.
            return true;
        }

        private string fileConfToken(MySqlConnection conn, int userId, DateTime expiry, string data, int action)
        {
            string code = null;
            using (MySqlCommand ins = DB.GetCmd(conn, "InsConfToken"))
            using (MySqlCommand sel = DB.GetCmd(conn, "SelConfToken"))
            using (MySqlCommand upd = DB.GetCmd(conn, "UpdConfTokenData"))
            {
                ins.Parameters["@user_id"].Value = userId;
                ins.Parameters["@expiry"].Value = expiry;
                ins.Parameters["@action"].Value = action;
                while (true)
                {
                    // Get new code
                    code = getRandomString();
                    // Insert (or do nothing - code is unique key)
                    ins.Parameters["@code"].Value = code;
                    ins.ExecuteNonQuery();
                    // Retrieve
                    string dataBack = null;
                    sel.Parameters["@code"].Value = code;
                    using (var rdr = sel.ExecuteReader())
                    {
                        while (rdr.Read()) dataBack = rdr.GetString("data");
                    }
                    // We got a unique new record if data is same as code
                    if (dataBack == code) break;
                }
                // Now update data in shiny new record
                upd.Parameters["@code"].Value = code;
                upd.Parameters["@data"].Value = data == null ? "" : data;
                upd.ExecuteNonQuery();
                return code;
            }
        }

        public bool ConfirmCreateUser(string code, int userId)
        {
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand sel = DB.GetCmd(conn, "SelUserById"))
            using (MySqlCommand upd = DB.GetCmd(conn, "UpdUserStatus"))
            using (MySqlCommand del = DB.GetCmd(conn, "DelConfToken"))
            {
                // Find user
                int status = -1;
                sel.Parameters["@id"].Value = userId;
                using (var rdr = sel.ExecuteReader())
                {
                    while (rdr.Read()) status = rdr.GetInt32("status");
                }
                // Only confirm if currently pending (and, erhm, found)
                if (status != 1) return false;
                // Set status to 0
                upd.Parameters["@id"].Value = userId;
                upd.Parameters["@status"].Value = 0;
                upd.ExecuteNonQuery();
                // Destroy token
                del.Parameters["@code"].Value = code;
                del.ExecuteNonQuery();
            }
            return true;
        }

        public bool ConfirmChangeEmail(string code, int userId, string newEmail)
        {
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand selUsr = DB.GetCmd(conn, "SelUserById"))
            using (MySqlCommand selEmail = DB.GetCmd(conn, "SelUserByEmail"))
            using (MySqlCommand upd = DB.GetCmd(conn, "UpdUserEmail"))
            using (MySqlCommand del = DB.GetCmd(conn, "DelConfToken"))
            {
                // Find user
                int status = -1;
                selUsr.Parameters["@id"].Value = userId;
                using (var rdr = selUsr.ExecuteReader())
                {
                    while (rdr.Read()) status = rdr.GetInt32("status");
                }
                // Only confirm if currently active (and, erhm, found)
                if (status != 0) return false;
                // Verify email uniqueness, once again (may have been registered since change was requested)
                bool mailInUse = false;
                selEmail.Parameters["@email"].Value = newEmail;
                using (var rdr = selEmail.ExecuteReader())
                {
                    while (rdr.Read()) mailInUse = true;
                }
                if (mailInUse) return false;
                // Update email
                upd.Parameters["@id"].Value = userId;
                upd.Parameters["@email"].Value = newEmail;
                upd.ExecuteNonQuery();
                // Destroy token
                del.Parameters["@code"].Value = code;
                del.ExecuteNonQuery();
                // Log out user if currently logged in
                lock (sessions)
                {
                    List<string> toRem = new List<string>();
                    foreach (var x in sessions) if (x.Value.UserId == userId) toRem.Add(x.Key);
                    foreach (var token in toRem) sessions.Remove(token);
                }
            }
            return true;
        }

        public CreateUserResult CreateUser(string lang, string email, string userName, string pass)
        {
            email = email.ToLowerInvariant().Trim();
            // Salt password, hash
            // http://www.c-sharpcorner.com/article/hashing-passwords-in-net-core-with-tips/
            // https://crackstation.net/hashing-security.htm
            string salt = getRandomString();
            string hash = getHash(pass + salt);

            CreateUserResult res = CreateUserResult.OK;
            int count;
            int userId = -1;
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
                userId = (int)ins.LastInsertedId;
                count = 0;
                sel1.Parameters["@user_name"].Value = userName;
                using (var rdr = sel1.ExecuteReader()) { while (rdr.Read()) ++count; }
                if (count > 1) res |= CreateUserResult.UserNameExists;
                count = 0;
                sel2.Parameters["@email"].Value = email;
                using (var rdr = sel2.ExecuteReader()) { while (rdr.Read()) ++count;  }
                if (count > 1) res |= CreateUserResult.EmailExists;
                if (res == 0) trans.Commit();
                else trans.Rollback();
            }
            if (res != CreateUserResult.OK) return res;
            // User created: store confirmation token; send email
            using (MySqlConnection conn = DB.GetConn())
            {
                DateTime expiry = DateTime.UtcNow;
                expiry = expiry.AddMinutes(60);
                string code = fileConfToken(conn, userId, expiry, null, (int)ConfirmedAction.Register);
                string msg = pageProvider.GetPage(lang, "?/mail.regconfirm", false).Html;
                msg = string.Format(msg, lang, code);
                emailer.SendMail(email,
                    TextProvider.Instance.GetString(lang, "emails.senderNameHDD"),
                    TextProvider.Instance.GetString(lang, "emails.regConfirm"),
                    msg, true);
            }
            return res;
        }
    }
}
