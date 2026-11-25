using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Orion
{
    /// <summary>Who asked for a notification. Mirrors orion::NotificationSource.</summary>
    public enum NotificationSource
    {
        Extension = 0,
        Web = 1,
        Browser = 2,
    }

    /// <summary>Mirrors orion::NotificationEventType (core/notification_provider.h).</summary>
    public enum NotificationEventType
    {
        Shown = 0,
        Clicked = 1,
        ButtonClicked = 2,
        Closed = 3,
    }

    public sealed class OrionNotificationButton
    {
        public string Title { get; set; } = "";
        public string IconPath { get; set; } = "";
    }

    public sealed class OrionNotificationItem
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// A desktop notification request. Field names mirror orion::NotificationRequest
    /// so the core's JSON (snake_case) deserializes straight into it; other callers
    /// (page Notification API, Orion's own messages) construct it directly.
    /// </summary>
    public sealed class OrionNotification
    {
        public NotificationSource Source { get; set; } = NotificationSource.Browser;
        /// <summary>Extension id for Extension, origin for Web, free-form for Browser.</summary>
        public string SourceId { get; set; } = "";
        /// <summary>Unique within (Source, SourceId); showing the same id again replaces the toast.</summary>
        public string NotificationId { get; set; } = "";
        /// <summary>"basic" | "image" | "list" | "progress" (WebExtensions template types).</summary>
        public string Type { get; set; } = "basic";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string ContextMessage { get; set; } = "";
        /// <summary>Absolute local file path or http(s) URL; empty for none.</summary>
        public string IconPath { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public List<OrionNotificationButton> Buttons { get; set; } = [];
        public List<OrionNotificationItem> Items { get; set; } = [];
        public int? Progress { get; set; }
        public int Priority { get; set; }
        /// <summary>Milliseconds since the Unix epoch.</summary>
        public double? EventTime { get; set; }
        public bool RequireInteraction { get; set; }
        public bool Silent { get; set; }
    }

    /// <summary>A user activation of a shown notification. ButtonIndex is -1 for a body click.</summary>
    public readonly record struct NotificationActivation(
        NotificationSource Source, string SourceId, string NotificationId, int ButtonIndex);

    /// <summary>
    /// Desktop notifications for the whole app, on top of the Windows App SDK
    /// AppNotificationManager (works unpackaged). One presenter serves every
    /// source: the core's WebExtensions runtime (browser.notifications, via the
    /// SetNotificationCallbacks / NotifyNotificationEvent exports), and any C#
    /// caller through <see cref="Show"/>.
    ///
    /// Activation routing: every toast carries the owning profile id in its
    /// arguments. Orion runs one process per profile and Windows may activate
    /// whichever process registered last, so an activation for another
    /// profile is forwarded to that profile's process over the SingleInstance
    /// pipe. Extension activations are reported to the core (which raises the
    /// browser.notifications events); other sources raise <see cref="Activated"/>.
    /// The Windows toast platform does not report dismissals without a click,
    /// so no Closed event is produced for those.
    /// </summary>
    public static class NotificationService
    {
        private const string Group = "orion";
        private const int MaxButtons = 5;

        private static readonly JsonSerializerOptions s_json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private static DispatcherQueue? s_dispatcher;
        private static long s_profileId;
        private static bool s_registered;

        /// <summary>Raised on the UI thread for activations of Web/Browser notifications.</summary>
        public static event Action<NotificationActivation>? Activated;

        /// <summary>
        /// Registers with the notification platform and connects the core's
        /// presenter callbacks. Call once on the UI thread after InitOrionCore.
        /// </summary>
        public static void Initialize(DispatcherQueue dispatcher, long profileId)
        {
            s_dispatcher = dispatcher;
            s_profileId = profileId;
            try
            {
                AppNotificationManager manager = AppNotificationManager.Default;
                manager.NotificationInvoked += OnNotificationInvoked;
                manager.Register();
                s_registered = true;
            }
            catch (Exception ex)
            {
                OrionBackend.LogWarning($"NotificationService: registration failed: {ex.Message}");
            }
            OrionBackend.SetupNotificationCallbacks(ShowFromCore, ClearFromCore);
        }

        /// <summary>
        /// Delivers the activation that launched this process, if Orion was
        /// started by a click on one of its toasts. Call after the windows exist.
        /// </summary>
        public static void HandleLaunchActivation()
        {
            try
            {
                AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
                if (args.Kind == ExtendedActivationKind.AppNotification
                    && args.Data is AppNotificationActivatedEventArgs notification)
                {
                    DispatchArguments(notification.Arguments);
                }
            }
            catch (Exception ex)
            {
                OrionBackend.LogWarning($"NotificationService: launch activation check failed: {ex.Message}");
            }
        }

        /// <summary>Shows (or replaces) a notification. UI thread.</summary>
        public static void Show(OrionNotification notification)
        {
            if (!s_registered)
            {
                OrionBackend.LogWarning("NotificationService: not registered; dropping notification");
                return;
            }
            try
            {
                AppNotificationManager.Default.Show(Build(notification));
            }
            catch (Exception ex)
            {
                OrionBackend.LogWarning($"NotificationService: show failed: {ex.Message}");
                return;
            }
            if (notification.Source == NotificationSource.Extension)
            {
                // After the caller's create() has resolved, not re-entrantly inside it.
                string sourceId = notification.SourceId, id = notification.NotificationId;
                _ = s_dispatcher?.TryEnqueue(() => OrionBackend.NotifyNotificationEvent(
                    NotificationEventType.Shown, NotificationSource.Extension, sourceId, id, -1, false));
            }
        }

        /// <summary>Removes a notification from the screen and Action Center.</summary>
        public static void Clear(NotificationSource source, string sourceId, string notificationId)
        {
            if (!s_registered)
                return;
            _ = RemoveAsync(Tag(source, sourceId, notificationId));
        }

        private static async Task RemoveAsync(string tag)
        {
            try
            {
                await AppNotificationManager.Default.RemoveByTagAndGroupAsync(tag, Group);
            }
            catch (Exception ex)
            {
                OrionBackend.LogWarning($"NotificationService: remove failed: {ex.Message}");
            }
        }

        // ───── Core presenter callbacks (UI thread) ─────────────────────────

        private static void ShowFromCore(string requestJson)
        {
            OrionNotification? notification = null;
            try
            {
                notification = JsonSerializer.Deserialize<OrionNotification>(requestJson, s_json);
            }
            catch (Exception ex)
            {
                OrionBackend.LogWarning($"NotificationService: bad request from core: {ex.Message}");
            }
            if (notification != null)
                Show(notification);
        }

        private static void ClearFromCore(int source, string sourceId, string notificationId)
            => Clear((NotificationSource)source, sourceId, notificationId);

        // ───── Toast construction ───────────────────────────────────────────

        private static AppNotification Build(OrionNotification n)
        {
            AppNotificationBuilder builder = new AppNotificationBuilder()
                .SetTag(Tag(n.Source, n.SourceId, n.NotificationId))
                .SetGroup(Group);
            AddIdentityArguments(builder, n);

            if (!string.IsNullOrEmpty(n.Title))
                _ = builder.AddText(n.Title);

            string body = n.Message;
            if (n.Type == "list" && n.Items.Count > 0)
            {
                IEnumerable<string> lines = n.Items.Select(i =>
                    string.IsNullOrEmpty(i.Title) ? i.Message : $"{i.Title}: {i.Message}");
                body = string.IsNullOrEmpty(body) ? string.Join("\n", lines) : body + "\n" + string.Join("\n", lines);
            }

            if (n.Type == "progress" && n.Progress.HasValue)
            {
                AppNotificationProgressBar bar = new AppNotificationProgressBar()
                    .SetValue(Math.Clamp(n.Progress.Value, 0, 100) / 100.0);
                if (!string.IsNullOrEmpty(body))
                    _ = bar.SetStatus(body);
                _ = builder.AddProgressBar(bar);
            }
            else if (!string.IsNullOrEmpty(body))
            {
                _ = builder.AddText(body);
            }

            if (!string.IsNullOrEmpty(n.ContextMessage))
                _ = builder.SetAttributionText(n.ContextMessage);
            if (TryUri(n.IconPath, out Uri? icon))
                _ = builder.SetAppLogoOverride(icon);
            if (n.Type == "image" && TryUri(n.ImagePath, out Uri? image))
                _ = builder.SetInlineImage(image);

            for (int i = 0; i < Math.Min(n.Buttons.Count, MaxButtons); i++)
            {
                OrionNotificationButton b = n.Buttons[i];
                var button = new AppNotificationButton(string.IsNullOrEmpty(b.Title) ? " " : b.Title);
                AddIdentityArguments(button, n);
                _ = button.AddArgument("button", i.ToString());
                if (TryUri(b.IconPath, out Uri? buttonIcon))
                    _ = button.SetIcon(buttonIcon);
                _ = builder.AddButton(button);
            }

            if (n.RequireInteraction)
            {
                _ = builder.SetDuration(AppNotificationDuration.Long);
                if (n.Buttons.Count > 0)
                    _ = builder.SetScenario(AppNotificationScenario.Reminder);
            }
            if (n.Silent)
                _ = builder.MuteAudio();
            if (n.EventTime is double eventTime && eventTime > 0)
                _ = builder.SetTimeStamp(DateTimeOffset.FromUnixTimeMilliseconds((long)eventTime).LocalDateTime);

            return builder.BuildNotification();
        }

        // The same identity travels on the toast body and on every button,
        // because a button's arguments replace the body's on activation.
        private static void AddIdentityArguments(AppNotificationBuilder builder, OrionNotification n)
        {
            _ = builder.AddArgument("profile", s_profileId.ToString())
                   .AddArgument("source", ((int)n.Source).ToString())
                   .AddArgument("sid", Uri.EscapeDataString(n.SourceId))
                   .AddArgument("id", Uri.EscapeDataString(n.NotificationId));
        }

        private static void AddIdentityArguments(AppNotificationButton button, OrionNotification n)
        {
            _ = button.AddArgument("profile", s_profileId.ToString())
                  .AddArgument("source", ((int)n.Source).ToString())
                  .AddArgument("sid", Uri.EscapeDataString(n.SourceId))
                  .AddArgument("id", Uri.EscapeDataString(n.NotificationId));
        }

        private static bool TryUri(string pathOrUrl, out Uri? uri)
        {
            uri = null;
            return !string.IsNullOrEmpty(pathOrUrl) && Uri.TryCreate(pathOrUrl, UriKind.Absolute, out uri);
        }

        /// <summary>Stable toast tag (max 64 chars) for a notification identity.</summary>
        private static string Tag(NotificationSource source, string sourceId, string notificationId)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{(int)source}|{sourceId}|{notificationId}"));
            return Convert.ToHexString(hash, 0, 16);
        }

        // ───── Activation ───────────────────────────────────────────────────

        // Any thread: the notification platform invokes on a background thread.
        private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
            => DispatchArguments(args.Arguments);

        private static void DispatchArguments(IDictionary<string, string> arguments)
        {
            if (!arguments.TryGetValue("profile", out string? profileText)
                || !long.TryParse(profileText, out long profileId))
            {
                return;
            }
            if (profileId != s_profileId)
            {
                SingleInstance.ForwardNotificationActivation(profileId, SerializeArguments(arguments));
                return;
            }
            var payload = new Dictionary<string, string>(arguments);
            _ = s_dispatcher?.TryEnqueue(() => Deliver(payload));
        }

        /// <summary>An activation forwarded from another profile's process. UI thread.</summary>
        internal static void HandleForwardedActivation(string payload)
            => Deliver(ParseArguments(payload));

        private static void Deliver(IDictionary<string, string> a)
        {
            if (!a.TryGetValue("source", out string? sourceText) || !int.TryParse(sourceText, out int sourceValue))
                return;
            var source = (NotificationSource)sourceValue;
            string sourceId = a.TryGetValue("sid", out string? sid) ? Uri.UnescapeDataString(sid) : "";
            string id = a.TryGetValue("id", out string? rawId) ? Uri.UnescapeDataString(rawId) : "";
            int button = a.TryGetValue("button", out string? b) && int.TryParse(b, out int index) ? index : -1;

            if (source == NotificationSource.Extension)
            {
                OrionBackend.NotifyNotificationEvent(
                    button >= 0 ? NotificationEventType.ButtonClicked : NotificationEventType.Clicked,
                    source, sourceId, id, button, false);
            }
            else
            {
                Activated?.Invoke(new NotificationActivation(source, sourceId, id, button));
            }
        }

        private static string SerializeArguments(IDictionary<string, string> a)
            => string.Join("&", a.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));

        private static Dictionary<string, string> ParseArguments(string payload)
        {
            var result = new Dictionary<string, string>();
            foreach (string pair in payload.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0)
                    continue;
                result[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
            return result;
        }
    }
}
