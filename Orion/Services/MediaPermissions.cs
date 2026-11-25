using System;

namespace Orion
{
    /// <summary>
    /// Per-website media-capture permission decisions, stored in the profile
    /// database with the same keys and values orion-linux's permission handlers
    /// use: settings::website::specific::&lt;website&gt;::&lt;key&gt; rows holding
    /// 1=ask, 2=deny, 3=allow, falling back to the profile's all-websites
    /// default (ask) when no row exists.
    /// </summary>
    public static class MediaPermissions
    {
        public enum Decision { Ask, Deny, Allow }

        private const long NotSetValue = 0;
        private const long AskValue = 1;
        private const long DenyValue = 2;
        private const long AllowValue = 3;

        private static readonly (WebMediaPermission Type, string KeyName)[] s_settingKeys =
        [
            (WebMediaPermission.Camera, "camera_default_setting"),
            (WebMediaPermission.Microphone, "microphone_default_setting"),
            (WebMediaPermission.ScreenShare, "screen_sharing_default_setting"),
        ];

        /// <summary>
        /// The persistence key for a page URL: its authority (host[:port]),
        /// or "" when the URL has none (persistence is skipped for those).
        /// </summary>
        public static string WebsiteForUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Authority : "";

        /// <summary>
        /// Combined persisted decision for every requested type: Deny when any
        /// type is denied, Allow when every type is allowed, Ask otherwise.
        /// </summary>
        public static Decision Resolve(long profileId, string website, WebMediaPermission types)
        {
            if (website.Length == 0)
                return Decision.Ask;

            bool sawRequestedType = false;
            bool allAllowed = true;
            foreach ((WebMediaPermission type, string keyName) in s_settingKeys)
            {
                if ((types & type) == 0)
                    continue;
                sawRequestedType = true;

                string raw = OrionBackend.GetSettingForWebsite(profileId, website, keyName, AskValue.ToString());
                long value = long.TryParse(raw, out long parsed) ? parsed : AskValue;
                if (value == DenyValue)
                    return Decision.Deny;
                if (value != AllowValue)
                    allAllowed = false;
            }
            return sawRequestedType && allAllowed ? Decision.Allow : Decision.Ask;
        }

        /// <summary>
        /// Persists allow/deny for every requested type. No-op when the
        /// website is empty (nothing meaningful to key on).
        /// </summary>
        public static void Persist(long profileId, string website, WebMediaPermission types, bool allow)
        {
            if (website.Length == 0)
                return;

            foreach ((WebMediaPermission type, string keyName) in s_settingKeys)
            {
                if ((types & type) != 0)
                {
                    OrionBackend.SetSettingForWebsite(profileId, website, keyName,
                        allow ? AllowValue : DenyValue, NotSetValue);
                }
            }
        }
    }
}
