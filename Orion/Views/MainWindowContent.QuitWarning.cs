using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace Orion
{
    public sealed partial class MainWindowContent
    {
        /// <summary>
        /// Quit entry point gated on settings::general::show_warning_before_quitting,
        /// mirroring orion-linux's quit action + QuitOrionDialog: a confirmation
        /// dialog showing open tab-group/tab counts, whose wording depends on
        /// whether the session is restored on next launch (app_opens_with),
        /// with an inline checkbox that can turn the warning off for good.
        /// </summary>
        /// <param name="quitAllProfiles">
        /// True for explicit Quit Orion (Ctrl+Q, menu): quits every profile's
        /// process. False for X-closing the last window: only this profile's
        /// process exits — other profiles' windows stay open.
        /// </param>
        internal async void RequestExitAll(bool quitAllProfiles)
        {
            long profileId = ViewModel.ActiveProfileId;
            bool warn = OrionBackend.GetBoolSetting(profileId,
                "settings::general::show_warning_before_quitting", "settings::general",
                "show_warning_before_quitting", true);
            if (!warn)
            {
                Exit(quitAllProfiles);
                return;
            }

            int groupCount = 0;
            int tabCount = 0;
            foreach (MainWindow window in MainWindow.AllWindows)
            {
                foreach (TabGroupViewModel vm in window.MainWindowContent.ViewModel.TabGroups)
                {
                    groupCount++;
                    tabCount += vm.Model.FlattenedTabs.Count();
                }
            }

            var appOpensWith = (AppOpensWith)int.Parse(OrionBackend.GetAppSetting(
                "settings::general::app_opens_with",
                ((int)AppOpensWith.AllNonPrivateWindowsFromLastSession).ToString()));
            bool restoresOnReopen = appOpensWith is AppOpensWith.AllWindowsFromLastSession
                or AppOpensWith.AllNonPrivateWindowsFromLastSession;

            string groupsWord = groupCount == 1 ? "tab group" : "tab groups";
            string tabsWord = tabCount == 1 ? "tab" : "tabs";
            string body = restoresOnReopen
                ? $"You have {groupCount} open {groupsWord} containing {tabCount} {tabsWord} that will be restored when Orion reopens."
                : $"You have {groupCount} open {groupsWord} containing {tabCount} {tabsWord} that will be closed.";
            if (quitAllProfiles)
                body += "\n\nWindows of all profiles will be closed.";

            var warnCheckBox = new CheckBox
            {
                Content = "Show warning before quitting",
                IsChecked = true,
            };
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(warnCheckBox);

            var dialog = new PopupDialog
            {
                Title = "Are you sure you want to quit Orion?",
                DialogContent = panel,
                PrimaryButtonText = "Quit",
                CloseButtonText = "Cancel",
            };
            if (await dialog.ShowAsync(this) != PopupDialogResult.Primary)
                return;

            if (warnCheckBox.IsChecked != true)
            {
                OrionBackend.SetBoolSetting(profileId,
                    "settings::general::show_warning_before_quitting", "settings::general",
                    "show_warning_before_quitting", false, true);
            }
            Exit(quitAllProfiles);
        }

        /// <summary>
        /// Quit Orion means the whole app: one process per profile, so ask
        /// every other running profile's process to quit (they skip their own
        /// confirmation — the user already answered here), then exit this one.
        /// Records which profiles were open so the next cold start can reopen
        /// all of them, not just the default profile (Program.Main consumes
        /// the list). Skipped when the startup setting doesn't restore
        /// sessions — reopening fresh windows for every profile would be noise.
        /// </summary>
        private static void Exit(bool quitAllProfiles)
        {
            if (quitAllProfiles)
            {
                long currentId = OrionBackend.GetCurrentProfileId();
                var openProfileIds = new List<long> { currentId };  // focused profile first
                foreach (Profile profile in OrionBackend.GetProfiles())
                {
                    if (profile.Id != currentId && SingleInstance.SendQuit(profile.Id))
                        openProfileIds.Add(profile.Id);
                }

                var appOpensWith = (AppOpensWith)int.Parse(OrionBackend.GetAppSetting(
                    "settings::general::app_opens_with",
                    ((int)AppOpensWith.AllNonPrivateWindowsFromLastSession).ToString()));
                bool restoresOnReopen = appOpensWith is AppOpensWith.AllWindowsFromLastSession
                    or AppOpensWith.AllNonPrivateWindowsFromLastSession;
                OrionBackend.SetAppSetting("settings::general::reopen_profiles",
                    restoresOnReopen ? string.Join(',', openProfileIds) : "");
            }
            MainWindow.ExitAll();
        }
    }
}
