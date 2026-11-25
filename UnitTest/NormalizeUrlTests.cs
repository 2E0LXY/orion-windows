using System;
using Xunit;

namespace Orion
{
    public class NormalizeUrlTests
    {
        [Theory]
        [InlineData("http://example.com")]
        [InlineData("https://kagi.com/stats")]
        [InlineData("about:blank")]
        [InlineData("orion://settings")]
        public void AlreadyHasScheme_ReturnsUnchanged(string input) => Assert.Equal(input, MainWindowViewModel.NormalizeUrl(input));

        [Theory]
        [InlineData("example.com", "https://example.com")]
        [InlineData("kagi.com/search", "https://kagi.com/search")]
        [InlineData("192.168.1.1", "https://192.168.1.1")]
        public void LooksLikeUrl_PrefixesHttps(string input, string expected) => Assert.Equal(expected, MainWindowViewModel.NormalizeUrl(input));

        [Theory]
        [InlineData("hello world")]
        [InlineData("python tutorial")]
        public void SearchQuery_DefaultsToKagi(string query)
        {
            string result = MainWindowViewModel.NormalizeUrl(query);
            Assert.StartsWith("https://kagi.com/search?q=", result);
            Assert.Contains(Uri.EscapeDataString(query), result);
        }

        [Theory]
        [InlineData("hello world", "https://www.google.com/search?q=%s", "https://www.google.com/search?q=hello%20world")]
        [InlineData("rust lang", "https://duckduckgo.com/?q=%s", "https://duckduckgo.com/?q=rust%20lang")]
        public void SearchQuery_UsesProvidedTemplate(string query, string template, string expected) => Assert.Equal(expected, MainWindowViewModel.NormalizeUrl(query, template));
    }
}
