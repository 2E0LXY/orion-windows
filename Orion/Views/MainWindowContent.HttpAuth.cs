using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Orion
{
    public sealed partial class MainWindowContent
    {
        /// <summary>
        /// Credentials dialog for HTTP auth challenges (401 from a server,
        /// 407 from a proxy), raised via NativeWebView.AuthCredentialsRequested.
        /// Cancel renders the site's 401 body, matching other browsers.
        /// v1 shows a plain dialog; integrating the password store (prefill +
        /// offer to save, keyed host:port like the form autofill) is a
        /// follow-up.
        /// </summary>
        private async void ShowHttpAuthDialog(
            string host, int port, bool isProxy, string realm, long requestId)
        {
            string message = (isProxy
                ? $"The proxy {host}:{port} requires a username and password."
                : $"The server {host}:{port} requires a username and password.")
                + (realm.Length > 0 ? $" The server says: “{realm}”." : "");

            var usernameBox = new TextBox { Header = "Username" };
            var passwordBox = new PasswordBox { Header = "Password" };
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(usernameBox);
            panel.Children.Add(passwordBox);

            var dialog = new PopupDialog
            {
                Title = isProxy ? "Proxy authentication required" : "Sign in",
                DialogContent = panel,
                PrimaryButtonText = "Sign in",
                CloseButtonText = "Cancel",
            };

            bool signIn = await dialog.ShowAsync(this) == PopupDialogResult.Primary;
            OrionBackend.RespondToAuthCredentials(requestId,
                signIn ? 1 : 0, usernameBox.Text, passwordBox.Password);
        }
    }
}
