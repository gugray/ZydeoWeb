using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using ZDO.CHSite.Entities;
using ZDO.CHSite.Logic;

namespace ZDO.CHSite.Controllers
{
    /// <summary>
    /// Endpoints for authentication and account management.
    /// </summary>
    public class AuthController : Controller
    {
        private static HttpClient hcl = new HttpClient();
        private readonly Auth auth;
        private readonly IConfiguration config;
        private readonly ILogger logger;

        public AuthController(Auth auth, IConfiguration config, ILogger<AuthController> logger)
        {
            this.auth = auth;
            this.config = config;
            this.logger = logger;
        }

        /// <summary>
        /// Verifies captcha response sent from frontend.
        /// </summary>
        private bool isCaptchaOk(string captcha)
        {
            bool captchaOk = false;
            try
            {
                logger.LogDebug("Verifying captcha");
                Dictionary<string, string> postParams = new Dictionary<string, string>();
                postParams["secret"] = config["captchaSecretKey"];
                postParams["response"] = captcha;
                var cres = hcl.PostAsync("https://www.google.com/recaptcha/api/siteverify", new FormUrlEncodedContent(postParams)).Result;
                string ccont = "<empty>";
                try { ccont = cres.Content.ReadAsStringAsync().Result; }
                catch {}
                logger.LogDebug("Siteverify response: {0}", ccont);
                if (cres.IsSuccessStatusCode)
                {
                    Regex reCheck = new Regex("\"success\": +([^,]+)");
                    Match m = reCheck.Match(ccont);
                    if (m.Success && m.Groups[1].Value == "true") captchaOk = true;
                    else logger.LogWarning("Captcha failed to verify; Siteverify response: {0}", ccont);
                }
                else logger.LogError("Siteverify returned status {0}; response: {1}", (int)cres.StatusCode, ccont);
            }
            catch (Exception ex)
            {
                logger.LogWarning(new EventId(), ex, "Siteverify API call failed.");
            }
            return captchaOk;
        }

        /// <summary>
        /// Starts cycle for new account creation.
        /// </summary>
        public IActionResult CreateUser([FromForm] string email, [FromForm] string userName, [FromForm] string pass,
            [FromForm] string captcha, [FromForm] string lang)
        {
            // Must have all fields
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(pass) ||
                string.IsNullOrEmpty(captcha) || string.IsNullOrEmpty(lang))
            {
                logger.LogWarning(new EventId(), "Missing request data in CreateUser.");
                return StatusCode(400, "Missing request data.");
            }
            // Verify email, user and pass criteria. If they fail here, that's an invalid request: client should have checked.
            bool dataValid = true;
            if (!auth.IsEmailValid(email)) dataValid = false;
            Regex reUsrName = new Regex(@"^[\._\-\p{L}\p{N}]+$");
            if (!reUsrName.Match(userName).Success) dataValid = false;
            if (userName.Length < 3) dataValid = false;
            if (!auth.IsPasswordValid(pass)) dataValid = false;
            if (!dataValid) return StatusCode(400, "Invalid data; check for validation criteria.");

            // Verify captcha
            if (!isCaptchaOk(captcha)) return StatusCode(400, "Captcha didn't verify.");

            // See if user can be created
            Auth.CreateUserResult cur = auth.CreateUser(lang, email, userName, pass);
            CreateUserResult res = new CreateUserResult
            {
                Success = (cur == 0),
                UserNameExists = (cur & Auth.CreateUserResult.UserNameExists) == Auth.CreateUserResult.UserNameExists,
                EmailExists = (cur & Auth.CreateUserResult.EmailExists) == Auth.CreateUserResult.EmailExists,
            };
            return new ObjectResult(res);
        }

        /// <summary>
        /// Authenticates with email and password, starts session if successful.
        /// </summary>
        public IActionResult Login([FromForm] string email, [FromForm] string pass)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass)) return new ObjectResult(null);
            return new ObjectResult(auth.Login(email, pass));
        }

        /// <summary>
        /// Ends current session (auth token in header).
        /// </summary>
        public IActionResult Logout()
        {
            auth.Logout(HttpContext.Request.Headers);
            return new ObjectResult(null);
        }

        /// <summary>
        /// Changes user's public info (auth token in header).
        /// </summary>
        public IActionResult ChangeInfo([FromForm] string location, [FromForm] string about)
        {
            if (location == null) location = "";
            if (about == null) about = "";
            // Must come from authenticated user
            int userId; string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return StatusCode(401, "Authentication token missing, invalid or expired.");
            // Store changes
            auth.ChangeInfo(userId, location, about);
            return new ObjectResult(true);
        }

        /// <summary>
        /// Triggers email change cycle (auth token in header).
        /// </summary>
        public IActionResult ChangeEmail([FromForm] string pass, [FromForm] string newEmail, [FromForm] string lang)
        {
            // Must have all fields
            if (string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(newEmail) || string.IsNullOrEmpty(lang))
            {
                logger.LogWarning(new EventId(), "Missing request data in ChangeEmail.");
                return StatusCode(400, "Missing request data.");
            }
            // Must come from authenticated user
            int userId; string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return StatusCode(401, "Authentication token missing, invalid or expired.");
            // Validate new email
            if (!auth.IsEmailValid(newEmail)) return StatusCode(400, "Invalid data; validate before request.");
            // Trigger mail change sequence, with password verification
            return new ObjectResult(auth.TriggerChangeEmail(userId, pass, newEmail, lang));
        }

        /// <summary>
        /// Changes account's password (auth token in header).
        /// </summary>
        public IActionResult ChangePassword([FromForm] string oldPass, [FromForm] string newPass)
        {
            // Must have all fields
            if (string.IsNullOrEmpty(oldPass) || string.IsNullOrEmpty(newPass))
            {
                logger.LogWarning(new EventId(), "Missing request data in ChangePassword.");
                return StatusCode(400, "Missing request data.");
            }
            // Must come from authenticated user
            int userId; string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId < 0) return StatusCode(401, "Authentication token missing, invalid or expired.");
            // Validate new password
            if (!auth.IsPasswordValid(newPass)) return StatusCode(400, "Invalid data; validate before request.");
            // Change password, with verification
            if (auth.ChangePassword(userId, oldPass, newPass)) return new ObjectResult(true);
            else return new ObjectResult(false);
        }

        /// <summary>
        /// Triggers password reset cycle for account (auth token in header).
        /// </summary>
        public IActionResult ForgotPassword([FromForm] string email, [FromForm] string captcha, [FromForm] string lang)
        {
            // Must have all fields
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(captcha))
            {
                logger.LogWarning(new EventId(), "Missing request data in ForgotPassword.");
                return StatusCode(400, "Missing request data.");
            }
            // Verify captcha
            if (!isCaptchaOk(captcha)) return StatusCode(400, "Captcha didn't verify.");
            // Trigger password reset cycle
            auth.TriggerPasswordReset(email, lang);
            // We always say it worked.
            return new ObjectResult(true);
        }

        /// <summary>
        /// Sets account's new password based on confirmation code.
        /// </summary>
        public IActionResult ResetPassword([FromForm] string pass, [FromForm] string code)
        {
            // Must have all fields
            if (string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(code))
            {
                logger.LogWarning(new EventId(), "Missing request data in ResetPassword.");
                return StatusCode(400, "Missing request data.");
            }
            // Must NOT come from authenticated user
            int userId; string userName;
            auth.CheckSession(HttpContext.Request.Headers, out userId, out userName);
            if (userId != -1) return StatusCode(401, "Request must not contain authentication token.");
            // Validate new password
            if (!auth.IsPasswordValid(pass)) return StatusCode(400, "Invalid data; validate before request.");
            // Change password, with verification
            return new ObjectResult(auth.ResetPassword(pass, code));

        }
    }
}
