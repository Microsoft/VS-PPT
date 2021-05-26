using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Microsoft.VisualStudio.Editor.PeekF1
{
    public static class HelpUrlBuilder
    {
        public const string HelpUrlPrefix = "https://msdn.microsoft.com/query/dev14.query?appId=Dev14IDEF1&l=";
        public const string HelpUrlSuffix = "&rd=true#content";

        public static string Build(Dictionary<string, string[]> attributes)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StringBuilder sb = new StringBuilder();
            sb.Append(HelpUrlPrefix);

            uint lcid;
            IUIHostLocale loc = (IUIHostLocale)ServiceProvider.GlobalProvider.GetService(typeof(IUIHostLocale));
            if (loc != null && ErrorHandler.Succeeded(loc.GetUILocale(out lcid)))
            {
                var cultureInfo = new CultureInfo((int)lcid);
                sb.Append(cultureInfo.Name);
            }
            else
            {
                sb.Append("EN-US");
            }

            if (attributes.ContainsKey("keyword"))
            {
                foreach (var keyword in attributes["keyword"])
                {
                    AppendUrlParameter(sb, "&k=k({0})", keyword);
                }
            }

            if (attributes.ContainsKey("TargetFrameworkMoniker"))
            {
                AppendUrlParameter(sb, ";k(TargetFrameworkMoniker-{0})", attributes["TargetFrameworkMoniker"][0]);
            }

            if (attributes.ContainsKey("DevLang"))
            {
                AppendUrlParameter(sb, ";k(DevLang-{0})", attributes["DevLang"][0]);
            }

            sb.Append(HelpUrlSuffix);
            return sb.ToString();
        }

        private static void AppendUrlParameter(StringBuilder buffer, string format, string value)
        {
            if (value != null)
            {
                buffer.AppendFormat(format, Uri.EscapeDataString(value));
            }
        }
    }
}
