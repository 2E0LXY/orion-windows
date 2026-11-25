using System.Collections.Generic;
using System.Linq;

namespace Orion
{
    public sealed partial class MainWindowContent
    {
        private bool _tabOverviewWired;

        /// <summary>
        /// Toggles the tab overview popup. The popup renders above the web
        /// HWND, so nothing is hidden — the active tab stays visible (and
        /// capturable) behind the scrim, and closing needs no restore logic.
        /// </summary>
        private void ToggleTabOverview()
        {
            if (TabOverviewContent.IsOpen)
            {
                TabOverviewContent.Close();
                return;
            }

            if (!_tabOverviewWired)
            {
                _tabOverviewWired = true;
                TabOverviewContent.TabActivated += OnOverviewTabActivated;
                TabOverviewContent.TabCloseRequested += CloseTab;
            }

            // Refresh the active tab's thumbnail; its content stays visible
            // under the popup, so both capture paths work.
            if (ViewModel.ActiveTab is { } active)
                RequestTabThumbnail(active);

            TabOverviewContent.Open(BuildOverviewGroups());
        }

        /// <summary>All tab groups with their live tab collections.</summary>
        private List<TabOverviewGroup> BuildOverviewGroups() =>
            [.. ViewModel.TabGroups.Select(vm => new TabOverviewGroup(
                vm.Model.DisplayName, vm.Model, vm.Model.FlattenedTabCollection))];

        /// <summary>
        /// Activates a tile's tab; a tab from another group switches the
        /// group first (mirrors the retired list overview's behavior).
        /// </summary>
        private void OnOverviewTabActivated(BrowserTab tab)
        {
            TabGroupViewModel? groupVm = ViewModel.TabGroups.FirstOrDefault(
                g => g.Model.FlattenedTabs.Contains(tab));
            if (groupVm != null && groupVm != ViewModel.ActiveGroup)
                ViewModel.SelectTabGroup(groupVm.Model);
            ViewModel.SwitchToTab(tab);
        }
    }
}
