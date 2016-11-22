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
    public class AuthController : Controller
    {
        private static HttpClient hcl = new HttpClient();
        private readonly Auth auth;
        private readonly IConfiguration config;

        public AuthController(Auth auth, IConfiguration config)
        {
            this.auth = auth;
            this.config = config;
        }

        private bool isCaptchaOk(string captcha)
        {
            bool captchaOk = false;
            try
            {
                Dictionary<string, string> postParams = new Dictionary<string, string>();
                postParams["secret"] = config["captchaSecretKey"];
                postParams["response"] = captcha;
                var cres = hcl.PostAsync("https://www.google.com/recaptcha/api/siteverify", new FormUrlEncodedContent(postParams)).Result;
                if (cres.IsSuccessStatusCode)
                {
                    string ccont = cres.Content.ReadAsStringAsync().Result;
                    Regex reCheck = new Regex("\"success\": +([^,]+)");
                    Match m = reCheck.Match(ccont);
                    if (m.Success && m.Groups[1].Value == "true") captchaOk = true;
                }
            }
            catch (Exception ex)
            {
                // TO-DO: Log warning
            }
            return captchaOk;
        }

        public IActionResult CreateUser([FromForm] string email, [FromForm] string userName, [FromForm] string pass,
            [FromForm] string captcha)
        {
            // Must have all fields
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(captcha))
            {
                // TO-DO: log warning
                return StatusCode(400, "Missing request data.");
            }
            // Verify email, user and pass criteria. If they fail here, that's an invalid request: client should have checked.
            bool dataValid = true;
            // http://emailregex.com/
            Regex reEmail = new Regex(@"^[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$");
            if (!reEmail.Match(email).Success) dataValid = false;
            Regex reUsrName = new Regex(@"^[\._\-\p{L}\p{N}]+$");
            if (!reUsrName.Match(userName).Success) dataValid = false;
            if (userName.Length < 3) dataValid = false;
            if (pass.Length < 6) dataValid = false;
            if (!dataValid) return StatusCode(400, "Invalid data; check for validation criteria.");

            // Verify captcha
            if (!isCaptchaOk(captcha)) return StatusCode(400, "Captcha didn't verify.");

            // See if user can be created
            Auth.CreateUserResult cur = auth.CreateUser(email, userName, pass);
            CreateUserResult res = new CreateUserResult
            {
                Success = (cur == 0),
                UserNameExists = (cur & Auth.CreateUserResult.UserNameExists) == Auth.CreateUserResult.UserNameExists,
                EmailExists = (cur & Auth.CreateUserResult.EmailExists) == Auth.CreateUserResult.EmailExists,
            };
            return new ObjectResult(res);
        }

        public IActionResult Login([FromForm] string email, [FromForm] string pass)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass)) return new ObjectResult(null);
            return new ObjectResult(auth.Login(email, pass));
        }

        public IActionResult Logout()
        {
            auth.Logout(HttpContext.Request.Headers);
            return new ObjectResult(null);
        }

        public IActionResult ForgotPassword([FromForm] string email, [FromForm] string captcha)
        {
            // Must have all fields
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(captcha))
            {
                // TO-DO: log warning
                return StatusCode(400, "Missing request data.");
            }
            // Verify captcha
            if (!isCaptchaOk(captcha)) return StatusCode(400, "Captcha didn't verify.");
            // Trigger password reset cycle
            auth.TriggerPasswordReset(email);
            // We always say it worked.
            return new ObjectResult(true);
        }
    }
}
