using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Orion
{
    public sealed partial class MainWindowContent
    {
        private string _lastFormSubmitKey = "";
        private long _lastFormSubmitTick;
        private bool _showingSavePasswordDialog;

        private void OnAutofillInputFocus(NativeWebView webView, BrowserTab tab, string json)
        {
            if (!GetAutofillEnabled())
                return;

            string? url = tab.Url;
            if (string.IsNullOrEmpty(url))
                return;

            string? host = GetAuthority(url);
            if (host == null)
                return;

            if (OrionBackend.GetNeverSavePasswordItem(ViewModel.ActiveProfileId, host))
                return;

            List<PasswordEntry> entries = FindPasswordEntries(host);
            if (entries.Count == 0)
                return;

            try
            {
                using var doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                string type = root.TryGetProperty("type", out JsonElement t) ? t.GetString() ?? "" : "";
                if (type is not "username" and not "password")
                    return;

                ShowAutofillPicker(webView, host, entries);
            }
            catch
            {
                // Malformed JSON — ignore
            }
        }

        private void ShowAutofillPicker(NativeWebView webView, string site,
            List<PasswordEntry> entries)
        {
            AutofillPicker.Close();

            GeneralTransform transform = WebViewHost.TransformToVisual(this);
            Point hostOffset = transform.TransformPoint(new Point(0, 0));

            double offsetX = ActualWidth / 2;
            double offsetY = 0;

            AutofillPicker.CredentialSelected -= OnAutofillCredentialSelected;
            AutofillPicker.CredentialSelected += OnAutofillCredentialSelected;
            _autofillWebView = webView;

            AutofillPicker.Show(this, offsetX, offsetY, site, entries);
        }

        private NativeWebView? _autofillWebView;

        private void OnAutofillCredentialSelected(PasswordEntry entry)
        {
            if (_autofillWebView != null)
                FillCredentials(_autofillWebView, entry.Username, entry.Password);
            _autofillWebView = null;
        }

        private void OnAutofillFormSubmit(NativeWebView webView, BrowserTab tab, string json)
        {
            if (!GetSavePasswordEnabled())
                return;

            string? url = tab.Url;
            if (string.IsNullOrEmpty(url))
                return;

            string? host = GetAuthority(url);
            if (host == null)
                return;

            try
            {
                using var doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                string username = root.TryGetProperty("username", out JsonElement u) ? u.GetString() ?? "" : "";
                string password = root.TryGetProperty("password", out JsonElement p) ? p.GetString() ?? "" : "";

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    return;

                string key = $"{host}\n{username}\n{password}";
                long now = Stopwatch.GetTimestamp();
                if (key == _lastFormSubmitKey
                    && Stopwatch.GetElapsedTime(_lastFormSubmitTick, now).TotalSeconds < 2)
                {
                    return;
                }
                _lastFormSubmitKey = key;
                _lastFormSubmitTick = now;

                string schemeAndAuthority = GetSchemeAndAuthority(url) ?? $"https://{host}";
                _ = PromptAndSavePassword(schemeAndAuthority, host, username, password);
            }
            catch
            {
                // Malformed JSON — ignore
            }
        }

        private async Task PromptAndSavePassword(string schemeAndAuthority, string authority, string username, string password)
        {
            if (_showingSavePasswordDialog)
                return;

            long profileId = ViewModel.ActiveProfileId;

            if (OrionBackend.GetNeverSavePasswordItem(profileId, authority))
                return;

            long[] ids = OrionBackend.GetPasswordItemIds(profileId, authority: authority);
            List<PasswordEntry> existing = ids.Length > 0
                ? OrionBackend.GetPasswordEntries(ids) : [];

            if (existing.Count > 0)
            {
                PasswordEntry entry = existing[0];
                if (entry.Password == password)
                    return;

                _showingSavePasswordDialog = true;
                try
                {
                    PopupDialogResult result = await ShowUpdatePasswordDialog();
                    if (result == PopupDialogResult.Primary)
                    {
                        entry.Password = password;
                        OrionBackend.UpdatePasswordEntry(entry);
                    }
                }
                finally
                {
                    _showingSavePasswordDialog = false;
                }
                return;
            }

            _showingSavePasswordDialog = true;
            try
            {
                PopupDialogResult result = await ShowSavePasswordDialog();
                if (result == PopupDialogResult.Primary)
                {
                    OrionBackend.CreatePasswordEntry(profileId, new PasswordEntry
                    {
                        Url = schemeAndAuthority,
                        Username = username,
                        Password = password,
                    });
                }
                else if (result == PopupDialogResult.Secondary)
                {
                    OrionBackend.CreateNeverSavePasswordItem(profileId, schemeAndAuthority);
                }
            }
            finally
            {
                _showingSavePasswordDialog = false;
            }
        }

        private Task<PopupDialogResult> ShowSavePasswordDialog()
        {
            var dialog = new PopupDialog
            {
                Title = "Save Password",
                DialogContent = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = "Would you like to save this password?\n\n" +
                           "You can view and remove saved passwords in Orion password settings.",
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                },
                PrimaryButtonText = "Save Password",
                SecondaryButtonText = "Never for This Website",
                CloseButtonText = "Not Now",
            };
            return dialog.ShowAsync(this);
        }

        private Task<PopupDialogResult> ShowUpdatePasswordDialog()
        {
            var dialog = new PopupDialog
            {
                Title = "Update Password",
                DialogContent = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = "Would you like to update the password for this website?\n\n" +
                           "You can view and remove saved passwords in Orion password settings.",
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                },
                PrimaryButtonText = "Update Password",
                CloseButtonText = "Do Not Update",
            };
            return dialog.ShowAsync(this);
        }

        private static void FillCredentials(NativeWebView webView, string username, string password)
        {
            string escapedUser = JsonEncodedText.Encode(username).ToString();
            string escapedPass = JsonEncodedText.Encode(password).ToString();
            webView.ExecuteJavaScript(
                $"window.dispatchEvent(new CustomEvent('Autofill:fill', " +
                $"{{ detail: {{ username: \"{escapedUser}\", password: \"{escapedPass}\" }} }}));");
        }

        private List<PasswordEntry> FindPasswordEntries(string host)
        {
            long profileId = ViewModel.ActiveProfileId;
            long[] ids = OrionBackend.GetPasswordItemIds(profileId, authority: host);
            if (ids.Length == 0)
                return [];

            return OrionBackend.GetPasswordEntries(ids);
        }

        private bool GetAutofillEnabled()
        {
            return OrionBackend.GetBoolSetting(ViewModel.ActiveProfileId,
                "settings::passwords::offer_to_autofill_passwords_from_orion_keychain",
                "settings::passwords", "offer_to_autofill_passwords_from_orion_keychain", true);
        }

        private bool GetSavePasswordEnabled()
        {
            return OrionBackend.GetBoolSetting(ViewModel.ActiveProfileId,
                "settings::passwords::offer_to_save_passwords_to_orion_keychain",
                "settings::passwords", "offer_to_save_passwords_to_orion_keychain", true);
        }

        private static string? GetSchemeAndAuthority(string url)
        {
            try
            {
                var uri = new Uri(url);
                string authority = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
                return $"{uri.Scheme}://{authority}";
            }
            catch
            {
                return null;
            }
        }

        private static string? GetAuthority(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            }
            catch
            {
                return null;
            }
        }
    }
}
