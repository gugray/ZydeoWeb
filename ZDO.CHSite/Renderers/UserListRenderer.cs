using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;

using ZDO.CHSite.Logic;

namespace ZDO.CHSite.Renderers
{
    public class UserListRenderer
    {
        public static void Render(StringBuilder sb, string lang, List<Auth.UserInfo> users, bool isMobile)
        {
            sb.AppendLine("<div class='content userlist'>");

            // Full screen: two columns
            if (!isMobile)
            {
                int half = (users.Count + 1) / 2;
                for (int i = 0; i < half; ++i)
                {
                    // Left
                    renderUser(sb, lang, users[i], "left");
                    // Right, if present
                    if (half + i < users.Count) renderUser(sb, lang, users[half + i], "right");
                    // Separator
                    sb.AppendLine("<div class='itemSep' />");
                }
            }
            // Mobile: no left and right
            else
            {
                for (int i = 0; i != users.Count; ++i)
                {
                    renderUser(sb, lang, users[i], "");
                    sb.AppendLine("<div class='itemSep' />");
                }
            }

            sb.AppendLine("</div>"); // <div class='content userlist'>
        }

        private static void renderUser(StringBuilder sb, string lang, Auth.UserInfo user, string extraClass)
        {
            sb.AppendLine("<div class='user " + extraClass + "'>");

            sb.Append("<div class='line1'>");
            sb.Append("<span class='userName");
            if (user.IsLoggedIn) sb.Append(" online");
            sb.Append("'>");
            sb.Append(HtmlEncoder.Default.Encode(user.UserName));
            sb.Append("</span>");
            if (user.ContribScore > 0)
            {
                sb.Append(" &bull; ");
                sb.Append(getContribCountStr(user.ContribScore, lang));
                sb.Append(" <i class='fa fa-trophy'></i>");
            }
            sb.AppendLine("</div>"); // <div class='line1'>

            sb.Append("<div class='line2'>");
            bool needBreak = false;
            if (!user.IsPlaceholder)
            {
                sb.Append(HtmlEncoder.Default.Encode(TextProvider.Instance.GetString(lang, "userList.registered")));
                sb.Append(" ");
                sb.Append(Utils.ChinesDateStr(user.Registered));
                needBreak = true;
            }
            if (!string.IsNullOrEmpty(user.Location))
            {
                if (needBreak) sb.Append("<br/>");
                needBreak = false;
                sb.Append("<span class='userInfo'><b>");
                sb.Append(HtmlEncoder.Default.Encode(TextProvider.Instance.GetString(lang, "userList.location")));
                sb.Append("</b> ");
                sb.Append(HtmlEncoder.Default.Encode(user.Location));
                sb.Append("</span>");
            }
            if (!string.IsNullOrEmpty(user.About))
            {
                if (needBreak) sb.Append("<br/>");
                else sb.Append(" ");
                needBreak = false;
                sb.Append("<span class='userInfo'><b>");
                sb.Append(HtmlEncoder.Default.Encode(TextProvider.Instance.GetString(lang, "userList.about")));
                sb.Append("</b> ");
                sb.Append(HtmlEncoder.Default.Encode(user.About));
                sb.Append("</span>");
            }
            sb.AppendLine("</div>"); // <div class='line2'>

            sb.AppendLine("</div>"); // <div class='user...
        }

        private static string getContribCountStr(int count, string lang)
        {
            if (count < 10000) return count.ToString();
            int k = count / 1000;
            int frac = (count - k * 1000) / 100;
            if (lang == "de" || lang == "hu") return k.ToString() + "," + frac.ToString() + "k";
            else return k.ToString() + "." + frac.ToString() + "k";
        }
    }
}
