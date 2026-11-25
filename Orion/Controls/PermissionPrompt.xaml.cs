using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace Orion
{
    /// <summary>
    /// Consent prompt for media-capture permission requests, anchored below
    /// the host's toolbar like Chrome/Edge permission bubbles. HandleRequest
    /// answers immediately from the persisted per-website decision
    /// (MediaPermissions), then from the view's session decision ("this
    /// time" choices, kept until the tab leaves the site), and prompts
    /// otherwise; every request is resolved exactly once — a prompt
    /// dismissed without a choice (light dismiss, tab switch, navigation)
    /// denies for this time only.
    /// </summary>
    public sealed partial class PermissionPrompt : UserControl
    {
        private NativeWebView? _webView;
        private long _profileId;
        private string _website = "";
        private WebMediaPermission _types;
        private long _requestId;
        private bool _resolved;

        public bool IsOpen => PromptPopup.IsOpen;

        public PermissionPrompt() => InitializeComponent();

        /// <summary>
        /// Resolves or prompts for one media-access request. A request that
        /// arrives while another prompt is open is denied for this time only
        /// (sites re-ask on the next user interaction).
        /// </summary>
        public void HandleRequest(FrameworkElement anchor, NativeWebView webView, long profileId,
            string pageUrl, WebMediaPermission types, long requestId)
        {
            string website = MediaPermissions.WebsiteForUrl(pageUrl);

            MediaPermissions.Decision decision = MediaPermissions.Resolve(profileId, website, types);
            if (decision == MediaPermissions.Decision.Ask)
                decision = webView.ResolveSessionMediaDecision(website, types);
            if (decision != MediaPermissions.Decision.Ask)
            {
                OrionBackend.RespondToMediaAccessPermissionRequest(requestId,
                    decision == MediaPermissions.Decision.Allow);
                return;
            }

            if (PromptPopup.IsOpen)
            {
                OrionBackend.RespondToMediaAccessPermissionRequest(requestId, false);
                return;
            }

            _webView = webView;
            _profileId = profileId;
            _website = website;
            _types = types;
            _requestId = requestId;
            _resolved = false;

            HeaderText.Text = website.Length > 0 ? $"{website} wants to" : "This page wants to";
            BuildRequestRows(types);

            // Without a website to key on there is nothing to persist —
            // offer only the one-time choices.
            bool canPersist = website.Length > 0;
            AllowAlwaysButton.Visibility = canPersist ? Visibility.Visible : Visibility.Collapsed;
            NeverAllowButton.Visibility = canPersist ? Visibility.Visible : Visibility.Collapsed;
            DenyOnceButton.Visibility = canPersist ? Visibility.Collapsed : Visibility.Visible;

            PromptPopup.XamlRoot = anchor.XamlRoot;
            PromptPopup.PlacementTarget = anchor;
            PromptPopup.DesiredPlacement = PopupPlacementMode.BottomEdgeAlignedLeft;
            PromptPopup.IsOpen = true;
        }

        /// <summary>
        /// Dismisses (denies once) an open prompt when its page navigates to
        /// a different website; same-site URL changes keep it open.
        /// </summary>
        public void DismissForNavigation(string newUrl)
        {
            if (PromptPopup.IsOpen && MediaPermissions.WebsiteForUrl(newUrl) != _website)
                Close();
        }

        /// <summary>Dismisses (denies once) an open prompt.</summary>
        public void Close()
        {
            if (PromptPopup.IsOpen)
                PromptPopup.IsOpen = false;
        }

        private void BuildRequestRows(WebMediaPermission types)
        {
            RequestRows.Children.Clear();
            if ((types & WebMediaPermission.Camera) != 0)
                RequestRows.Children.Add(BuildRequestRow("", "Use your camera"));
            if ((types & WebMediaPermission.Microphone) != 0)
                RequestRows.Children.Add(BuildRequestRow("", "Use your microphone"));
            if ((types & WebMediaPermission.ScreenShare) != 0)
                RequestRows.Children.Add(BuildRequestRow("", "Share your screen"));
        }

        private static UIElement BuildRequestRow(string glyph, string text) => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children =
            {
                new FontIcon { Glyph = glyph, FontSize = 16 },
                new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center },
            }
        };

        private void ResolveRequest(bool allow, bool remember)
        {
            _resolved = true;
            OrionBackend.RespondToMediaAccessPermissionRequest(_requestId, allow);
            if (remember)
                MediaPermissions.Persist(_profileId, _website, _types, allow);
            else
                _webView!.RecordSessionMediaDecision(_website, _types, allow);
            if (PromptPopup.IsOpen)
                PromptPopup.IsOpen = false;
        }

        private void AllowAlwaysButton_Click(object sender, RoutedEventArgs e) => ResolveRequest(true, true);

        private void AllowOnceButton_Click(object sender, RoutedEventArgs e) => ResolveRequest(true, false);

        private void NeverAllowButton_Click(object sender, RoutedEventArgs e) => ResolveRequest(false, true);

        private void DenyOnceButton_Click(object sender, RoutedEventArgs e) => ResolveRequest(false, false);

        private void PromptPopup_Closed(object sender, object e)
        {
            // Closed without a button choice (light dismiss, Close(), window
            // teardown): deny for this time only, ask again next time.
            if (!_resolved)
            {
                _resolved = true;
                OrionBackend.RespondToMediaAccessPermissionRequest(_requestId, false);
            }
            _webView = null;
            RequestRows.Children.Clear();
        }
    }
}
