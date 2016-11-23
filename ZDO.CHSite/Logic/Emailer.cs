using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;

using System.Security.Cryptography.X509Certificates;

namespace ZDO.CHSite.Logic
{
    public class Emailer
    {
        private readonly string smtpUrl;
        private readonly int smtpPort;
        private readonly string smtpUser;
        private readonly string smtpPass;
        private readonly string smtpReplyTo;
        private readonly string smtpFrom;
        private readonly string smtpBCC;

        public Emailer(IConfiguration config)
        {
            smtpUrl = config["smtpUrl"];
            smtpPort = int.Parse(config["smtpPort"]);
            smtpUser = config["smtpUser"];
            smtpPass = config["smtpPass"];
            smtpReplyTo = config["smtpReplyTo"];
            smtpFrom = config["smtpFrom"];
            smtpBCC = config["smtpBCC"];
        }

        public void SendMail(string to, string senderFriendly, string subject, string msgHtml, bool bcc)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(senderFriendly, smtpFrom));
            msg.ReplyTo.Add(new MailboxAddress(null, smtpReplyTo));
            msg.To.Add(new MailboxAddress(null, to));
            if (bcc) msg.Bcc.Add(new MailboxAddress(null, smtpBCC));
            msg.Subject = subject;
            msg.Body = new TextPart("html") { Text = msgHtml };
            using (var client = new SmtpClient())
            {
                client.Connect(smtpUrl, smtpPort, true);
                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove("XOAUTH2");
                // Note: only needed if the SMTP server requires authentication
                client.Authenticate(smtpUser, smtpPass);
                client.Send(msg);
                client.Disconnect(true);
            }
        }
    }
}
