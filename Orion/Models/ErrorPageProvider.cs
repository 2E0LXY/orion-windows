using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

namespace Orion
{
    public static class ErrorPageProvider
    {
        private static readonly Dictionary<ErrorPageType, string> _templates = [];
        private static string _logoDataUri = string.Empty;

        private static readonly Dictionary<ErrorPageType, string> _fileNames = new()
        {
            { ErrorPageType.Unknown, "error_unknown.html" },
            { ErrorPageType.NetworkError, "error_network.html" },
            { ErrorPageType.DnsError, "error_dns.html" },
            { ErrorPageType.Timeout, "error_timeout.html" },
            { ErrorPageType.SslError, "error_ssl.html" },
            { ErrorPageType.ConnectionRefused, "error_connection_refused.html" },
        };

        public static void Initialize()
        {
            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

            string logoPath = Path.Combine(baseDir, "Assets", "orion-logo-64.png");
            if (File.Exists(logoPath))
                _logoDataUri = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(logoPath));

            string pagesDir = Path.Combine(baseDir, "InternalPages");
            foreach ((ErrorPageType type, string fileName) in _fileNames)
            {
                string filePath = Path.Combine(pagesDir, fileName);
                if (File.Exists(filePath))
                    _templates[type] = File.ReadAllText(filePath);
            }
        }

        public static string GetErrorPage(ErrorPageType type, string failingUrl, string description)
        {
            if (!_templates.TryGetValue(type, out string? template))
                _ = _templates.TryGetValue(ErrorPageType.Unknown, out template);

            return template == null
                ? $"<html><body><h1>Error</h1><p>{WebUtility.HtmlEncode(description)}</p></body></html>"
                : template
                .Replace("{{LOGO}}", _logoDataUri)
                .Replace("{{URL}}", WebUtility.HtmlEncode(failingUrl))
                .Replace("{{DESCRIPTION}}", WebUtility.HtmlEncode(description))
                .Replace("{{ERROR_TYPE}}", type.ToString());
        }
    }
}
