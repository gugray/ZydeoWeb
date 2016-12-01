using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using MySql.Data.MySqlClient;

namespace ZDO.CHSite.Logic 
{
    /// <summary>
    /// Users, authentication and sessions business logic.
    /// </summary>
    public class Auth
    {
        /// <summary>
        /// Result code returned by <see cref="CreateUser"/>.
        /// </summary>
        [Flags]
        public enum CreateUserResult
        {
            /// <summary>
            /// Registration cycle started.
            /// </summary>
            OK = 0,
            /// <summary>
            /// Email address already in use.
            /// </summary>
            EmailExists = 0x1,
            /// <summary>
            /// User name taken.
            /// </summary>
            UserNameExists = 0x2,
        }

        /// <summary>
        /// Types of actions confirmed by email cycle.
        /// </summary>
        public enum ConfirmedAction
        {
            /// <summary>
            /// Create new account.
            /// </summary>
            Register = 0,
            /// <summary>
            /// Reset lost password.
            /// </summary>
            PassReset = 1,
            /// <summary>
            /// Change existing account's email address.
            /// </summary>
            ChangeEmail = 2,
            /// <summary>
            /// Code cannot be verified (doesn't exist or expired).
            /// </summary>
            Bad = 9999,
        }

        /// <summary>
        /// Passive information about a user account. Excludes contribution # and anything password-related.
        /// </summary>
        public class UserInfo
        {
            public int UserId;
            public string UserName;
            public string Email;
            public DateTime Registered;
            public int ContribScore;
            public bool IsPlaceholder;
            public bool IsDeleted;
            public string About;
            public string Location;
            public int Perms;
            public bool IsLoggedIn;
        }

        /// <summary>
        /// Result code for frontend: email change request OK, cycle started.
        /// </summary>
        public const string mailChangeOK = "ok";
        /// <summary>
        /// Result code for frontend: incorrect password in request.
        /// </summary>
        public const string mailChangeBadPass = "badpass";
        /// <summary>
        /// Result code for frontend: requested email is taken.
        /// </summary>
        public const string mailChangeEmailInUse = "emailinuse";

        /// <summary>
        /// Information about a single active authentication session.
        /// </summary>
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
        private readonly string sessionFileName;
        private readonly string baseUrl;
        private readonly int sessionTimeoutMinutes;
        private readonly ManualResetEvent quitBusywork;
        private readonly Thread busyThread;
        private readonly Dictionary<string, SessionInfo> sessions = new Dictionary<string, SessionInfo>();

        public Auth(ILoggerFactory lf, IConfiguration config, Emailer emailer, PageProvider pageProvider)
        {
            if (lf != null) logger = lf.CreateLogger(GetType().FullName);
            else logger = new DummyLogger();
            this.emailer = emailer;
            this.pageProvider = pageProvider;
            sessionTimeoutMinutes = int.Parse(config["sessionTimeoutMinutes"]);
            baseUrl = config["baseUrl"];
            sessionFileName = config["sessionFileName"];
            loadPersistedSessions();

            quitBusywork = new ManualResetEvent(false);
            busyThread = new Thread(funBusywork);
            busyThread.Start();
        }

        /// <summary>
        /// Loads sessions persisted at last shutdown.
        /// </summary>
        private void loadPersistedSessions()
        {
            if (!File.Exists(sessionFileName)) return;
            try
            {
                using (FileStream fs = new FileStream(sessionFileName, FileMode.Open, FileAccess.Read))
                using (StreamReader sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] parts = line.Split('\t');
                        if (parts.Length != 4) continue;
                        SessionInfo si = new SessionInfo(int.Parse(parts[1]), parts[3]);
                        si.Expires = new DateTime(long.Parse(parts[2]), DateTimeKind.Utc);
                        sessions[parts[0]] = si;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(new EventId(), ex, "Error loading persisted sessions.");
            }
        }

        /// <summary>
        /// Persists current sessions, and kills background worder thread.
        /// </summary>
        public void Shutdown()
        {
            // Stop the busywork
            quitBusywork.Set();
            busyThread.Join(100);
            // Write current sessions to persistence file
            try
            {
                List<string> lines = new List<string>();
                lock (sessions)
                {
                    foreach (var x in sessions)
                    {
                        string line = x.Key + "\t" + x.Value.UserId + "\t" + x.Value.Expires.Ticks + "\t" + x.Value.UserName;
                        lines.Add(line);
                    }
                }
                using (FileStream fs = new FileStream(sessionFileName, FileMode.Create, FileAccess.Write))
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    foreach (string line in lines) sw.WriteLine(line);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(), ex, "Error while persisting sessions at shutdown.");
            }
        }

        /// <summary>
        /// Cleanup thread: purges expired sessions and old confirmation codes.
        /// </summary>
        private void funBusywork()
        {
            while (true)
            {
                // Busywork every 5 minutes
                // Quit when event is set: shutdown
                if (quitBusywork.WaitOne(1000 * 60 * 5)) break;
                // OK, do our job
                try
                {
                    // Purge expired sessions from memory
                    lock (sessions)
                    {
                        var dtNow = DateTime.UtcNow;
                        List<string> toRem = new List<string>();
                        foreach (var x in sessions) if (x.Value.Expires < dtNow) toRem.Add(x.Key);
                        foreach (string key in toRem) sessions.Remove(key);
                    }
                    // Delete confirmation tokens that are at least a week out of date from DB
                    using (MySqlConnection conn = DB.GetConn())
                    using (MySqlCommand cmd = DB.GetCmd(conn, "DelOldConfTokens"))
                    {
                        DateTime dtBefore = DateTime.UtcNow;
                        dtBefore.Subtract(new TimeSpan(7, 0, 0, 0));
                        cmd.Parameters["@expiry_before"].Value = dtBefore;
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(new EventId(), ex, "Error in busywork cleanup thread.");
                }
            }
        }

        /// <summary>
        /// Verifies if string is a valid (strong enough) password.
        /// </summary>
        public bool IsPasswordValid(string pass)
        {
            return pass.Length >= 6;
        }

        /// <summary>
        /// Verifies if strings looks like a valid email address (regex-based).
        /// </summary>
        public bool IsEmailValid(string email)
        {
            // http://emailregex.com/
            Regex reEmail = new Regex(@"^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$");
            return reEmail.Match(email).Success;
        }

        /// <summary>
        /// Retrieves auth token from HTTP request headers. Null if none found.
        /// </summary>
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

        /// <summary>
        /// <para>Checks if there's a live session for auth token. If yes, extends session's expiry as a side effect.</para>
        /// <para>User ID is -1 if there's not live session.</para>
        /// </summary>
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

        /// <summary>
        /// Decides if a user is authorized to approve entries (regardless of existing session).
        /// </summary>
        public bool CanApprove(int userId)
        {
            // TO-DO
            return false;
        }

        /// <summary>
        /// Ends the session identified by the auth token in HTTP headers, if found.
        /// </summary>
        public void Logout(IHeaderDictionary hdrReq)
        {
            string token = retrieveToken(hdrReq);
            lock (sessions)
            {
                if (sessions.ContainsKey(token)) sessions.Remove(token);
            }
        }

        /// <summary>
        /// Retrieves all existing users (including deleted ones).
        /// </summary>
        public List<UserInfo> GetAllUsers()
        {
            HashSet<int> loggedIn = new HashSet<int>();
            foreach (var x in sessions)
            {
                if (x.Value.Expires < DateTime.UtcNow) continue;
                loggedIn.Add(x.Value.UserId);
            }
            List<UserInfo> res = new List<UserInfo>();
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand sel = DB.GetCmd(conn, "SelAllUsers"))
            {
                using (var rdr = sel.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        object[] vals = new object[16];
                        rdr.GetValues(vals);
                        byte status = rdr.GetByte("status");
                        var usr = new UserInfo
                        {
                            UserId = rdr.GetInt32("id"),
                            UserName = rdr.GetString("user_name"),
                            Email = vals[2] is DBNull ? "" : rdr.GetString(2),
                            Registered = new DateTime(rdr.GetDateTime("registered").Ticks, DateTimeKind.Utc),
                            About = vals[7] is DBNull ? "" : rdr.GetString(7),
                            Location = vals[8] is DBNull ? "" : rdr.GetString(8),
                            IsDeleted = status == 2,
                            IsPlaceholder = status == 3,
                            ContribScore = rdr.GetInt32("contrib_score"),
                            Perms = rdr.GetInt32("perms"),
                        };
                        if (loggedIn.Contains(usr.UserId)) usr.IsLoggedIn = true;
                        res.Add(usr);
                    }
                }
            }
            return res;
        }

        /// <summary>
        /// Retrieves passive information about a user account.
        /// </summary>
        public UserInfo GetUserInfo(int userId)
        {
            UserInfo res = null;
            using (MySqlConnection conn = DB.GetConn())
            using (MySqlCommand sel = DB.GetCmd(conn, "SelUserById"))
            {
                sel.Parameters["@id"].Value = userId;
                using (var rdr = sel.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        object[] vals = new object[16];
                        rdr.GetValues(vals);
                        byte status = rdr.GetByte("status");
                        res = new UserInfo
                        {
                            UserId = userId,
                            UserName = rdr.GetString("user_name"),
                            Email = vals[2] is DBNull ? "" : rdr.GetString(2),
                            Registered = new DateTime(rdr.GetDateTime("registered").Ticks, DateTimeKind.Utc),
                            About = vals[7] is DBNull ? "" : rdr.GetString(7),
                            Location = vals[8] is DBNull ? "" : rdr.GetString(8),
                            IsDeleted = status == 2,
                            IsPlaceholder = status == 3,
                            ContribScore = rdr.GetInt32("contrib_score"),
                            Perms = rdr.GetInt32("perms"),
                        };
                    }
                }
            }
            foreach (var x in sessions)
            {
                if (x.Value.UserId != userId) continue;
                if (x.Value.Expires < DateTime.UtcNow) continue;
                res.IsLoggedIn = true;
                break;
            }
            return res;
        }

        /// <summary>
        /// Interprets confirmation code. Action is <see cref="ConfirmedAction.Bad"/> if code doesn't exist, or has expired.
        /// </summary>
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

        /// <summary>
        /// Changes a user's password if old password is correct. Returns false with no change otherwise.
        /// </summary>
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

        /// <summary>
        /// Changes a user's public information.
        /// </summary>
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

        /// <summary>
        /// Triggers email change cycle. No effect is pass is wrong or email is taken.
        /// </summary>
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
                string url = baseUrl + lang + "/user/confirm/" + code;
                msg = string.Format(msg, url);
                emailer.SendMail(newEmail,
                    TextProvider.Instance.GetString(lang, "emails.senderNameHDD"),
                    TextProvider.Instance.GetString(lang, "emails.mailChangeConfirm"),
                    msg, true);
                // We're good.
                return mailChangeOK;
            }
        }

        /// <summary>
        /// <para>Authenticates user. Creates session and returns auth token if credentials are OK.</para>
        /// <para>Returns null if authentication is unsuccessful.</para>
        /// </summary>
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

        /// <summary>
        /// Gets a cryptographic hash (SHA256) of the provided string.
        /// </summary>
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

        /// <summary>
        /// Creates a 128-bit cryptographic random number, as hexa string.
        /// </summary>
        private static string getRandomString()
        {
            using (var keyGenerator = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[128 / 8];
                keyGenerator.GetBytes(bytes);
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// <para>Triggers password reset cycle for user identified by email.</para>
        /// <para>Silently does nothing if email does not identify an existing user.</para>
        /// </summary>
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
                string url = baseUrl + lang + "/user/confirm/" + code;
                msg = string.Format(msg, url);
                emailer.SendMail(email,
                    TextProvider.Instance.GetString(lang, "emails.senderNameHDD"),
                    TextProvider.Instance.GetString(lang, "emails.passreset"),
                    msg, true);
            }
        }

        /// <summary>
        /// Sets new password of user identified by confirmation code. Returns false if code is wrong.
        /// </summary>
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

        /// <summary>
        /// Creates and stores a confirmation code for an email-confirmed action.
        /// </summary>
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

        /// <summary>
        /// Confirms new user with provided code. User's status goes from 1 to 0 if successful.
        /// </summary>
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

        /// <summary>
        /// Confirms account's new email with provided confirmation code.
        /// </summary>
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

        /// <summary>
        /// Creates pending new user and starts confirmation cycle.
        /// </summary>
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
                string url = baseUrl + lang + "/user/confirm/" + code;
                msg = string.Format(msg, url);
                emailer.SendMail(email,
                    TextProvider.Instance.GetString(lang, "emails.senderNameHDD"),
                    TextProvider.Instance.GetString(lang, "emails.regConfirm"),
                    msg, true);
            }
            return res;
        }
    }
}
