using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;

namespace Orion
{
    public sealed partial class TabGroupSwitcherPopup : UserControl
    {
        public event EventHandler<TabGroup>? GroupSelected;
        public event EventHandler? NewGroupRequested;
        public event EventHandler<GroupRenamedEventArgs>? GroupRenamed;

        /// <summary>
        /// Context flyout factory, forwarded to the inner TabGroupListPanel.
        /// </summary>
        public Func<TabGroup, MenuFlyout>? ContextFlyoutFactory
        {
            get => InnerGroupList.ContextFlyoutFactory;
            set => InnerGroupList.ContextFlyoutFactory = value;
        }

        public bool IsOpen
        {
            get => SwitcherPopup.IsOpen;
            set => SwitcherPopup.IsOpen = value;
        }

        public TabGroupSwitcherPopup() => InitializeComponent();

        public void SetDataSource(TabGroup? unsavedGroup, ObservableCollection<TabGroup> savedGroups)
        {
            InnerGroupList.UnsavedTabGroup = unsavedGroup;
            InnerGroupList.SavedTabGroups = savedGroups;
        }

        public void ShowBelow(FrameworkElement anchor, TabGroup? unsavedGroup, TabGroup? selectedGroup)
        {
            if (SwitcherPopup.IsOpen)
            {
                SwitcherPopup.IsOpen = false;
                return;
            }

            InnerGroupList.UnsavedTabGroup = unsavedGroup;
            InnerGroupList.SelectedGroup = selectedGroup;

            SwitcherPopup.XamlRoot = anchor.XamlRoot;

            // Position below the anchor, relative to this control's coordinate space
            GeneralTransform transform = anchor.TransformToVisual(this);
            Windows.Foundation.Point point = transform.TransformPoint(new Windows.Foundation.Point(0, anchor.ActualHeight));
            SwitcherPopup.HorizontalOffset = point.X;
            SwitcherPopup.VerticalOffset = point.Y + 4;

            SwitcherPopup.IsOpen = true;
        }

        public void Close() => SwitcherPopup.IsOpen = false;

        private void InnerGroupList_GroupSelected(object? sender, TabGroup e) => GroupSelected?.Invoke(this, e);

        private void InnerGroupList_GroupRenamed(object? sender, GroupRenamedEventArgs e) => GroupRenamed?.Invoke(this, e);

        private void InnerGroupList_NewGroupRequested(object? sender, EventArgs e) => NewGroupRequested?.Invoke(this, EventArgs.Empty);
    }
}
