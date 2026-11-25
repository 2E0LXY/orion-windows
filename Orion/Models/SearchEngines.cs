using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Orion
{
    public sealed record SearchEngineInfo(string Name, string UrlTemplate, string SuggestionUrlTemplate);

    /// <summary>
    /// Display name plus search/suggestion URL templates per SearchEngine
    /// (the enum is generated from the core module's settings_dto.h). The
    /// templates mirror orion-linux/app/src/util/search_engines.cpp — the core
    /// exposes them only through a GTK-dependent function, and has no display
    /// names, so this table is the Windows-side source for all three.
    /// %s is the query placeholder.
    /// </summary>
    public static class SearchEngines
    {
        public const string DefaultUrlTemplate = "https://kagi.com/search?q=%s";

        private static readonly (SearchEngine Engine, SearchEngineInfo Info)[] Table =
        [
            (SearchEngine.kagi, new("Kagi",
                DefaultUrlTemplate,
                "https://kagi.com/suggest?q=%s")),
            (SearchEngine.google, new("Google",
                "https://www.google.com/search?q=%s",
                "https://suggestqueries.google.com/complete/search?client=firefox&q=%s")),
            (SearchEngine.bing, new("Bing",
                "https://www.bing.com/search?q=%s",
                "https://api.bing.com/osjson.aspx?query=%s")),
            (SearchEngine.yandex, new("Yandex",
                "https://yandex.com/search/?text=%s",
                "https://suggest.yandex.com/suggest-ya.cgi?part=%s")),
            (SearchEngine.duckduckgo, new("DuckDuckGo",
                "https://duckduckgo.com/?q=%s",
                "https://duckduckgo.com/ac/?q=%s&type=list")),
            (SearchEngine.startpage, new("Startpage",
                "https://www.startpage.com/sp/search?q=%s",
                "https://ac.startpage.com/ac/sp_suggest.php?query=%s")),
            (SearchEngine.brave, new("Brave",
                "https://search.brave.com/search?q=%s",
                "https://search.brave.com/api/suggest?q=%s")),
            (SearchEngine.ecosia, new("Ecosia",
                "https://www.ecosia.org/search?q=%s",
                "https://ac.ecosia.org/autocomplete?q=%s")),
            (SearchEngine.yahoo, new("Yahoo",
                "https://search.yahoo.com/search?p=%s",
                "https://search.yahoo.com/sugg/gossip/gossip-us-ura/?output=fxjson&command=%s")),
            (SearchEngine.baidu, new("Baidu",
                "https://www.baidu.com/s?wd=%s",
                "http://suggestion.baidu.com/su?wd=%s&json=1")),
            (SearchEngine.qwant, new("Qwant",
                "https://www.qwant.com/?q=%s",
                "https://api.qwant.com/api/suggest/v1/web?count=10&q=%s")),
            (SearchEngine.mojeek, new("Mojeek",
                "https://www.mojeek.com/search?q=%s",
                "https://www.mojeek.com/suggest?q=%s")),
            (SearchEngine.you_com, new("You.com",
                "https://you.com/search?q=%s",
                "https://you.com/api/suggest?q=%s")),
        ];

        /// <summary>All engines in presentation order (table order).</summary>
        public static readonly SearchEngine[] All = [.. Table.Select(t => t.Engine)];

        public static readonly FrozenDictionary<SearchEngine, SearchEngineInfo> ByEngine =
            Table.ToFrozenDictionary(t => t.Engine, t => t.Info);

        /// <summary>Falls back to Kagi for non-engine values (not_set, none).</summary>
        public static SearchEngineInfo Get(SearchEngine engine) =>
            ByEngine.GetValueOrDefault(engine, ByEngine[SearchEngine.kagi]);
    }
}
