using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using Xunit;

namespace Orion
{
    /// <summary>
    /// Tests for the fullscreen-related MainWindowViewModel behavior:
    /// the non-persisting sidebar setter used by fullscreen auto-hide, and
    /// the chrome visibility computed properties driven by IsChromeHiddenForFullscreen.
    /// Shares a collection with MainWindowViewModelTests: constructing the VM
    /// resets static shared state (_sharedSavedGroupsByProfile), so classes
    /// doing that must not run in parallel with each other.
    /// </summary>
    [Collection("MainWindowViewModel shared state")]
    public class FullscreenViewModelTests : IDisposable
    {
        private static Profile TestProfile => new() { Id = 1, Name = "Test", IsDefault = true };

        private const string ShowSidebarKey = "settings::appearance::show_sidebar";

        // Per-test recording of SetBoolSetting calls; isolated from parallel
        // test classes via the stub's AsyncLocal sink.
        private readonly List<(long ProfileId, string Key, bool Value)> _boolWrites = [];

        public FullscreenViewModelTests()
        {
            MainWindowViewModel.ResetSharedState();
            OrionBackend.ResetStubState();
            OrionBackend.BoolSettingWriteSink.Value = _boolWrites;
        }

        public void Dispose()
        {
            OrionBackend.BoolSettingWriteSink.Value = null;
            MainWindowViewModel.ResetSharedState();
            OrionBackend.ResetStubState();
        }

        [Fact]
        public void ShowSidebar_UserSet_PersistsSetting()
        {
            var vm = new MainWindowViewModel(TestProfile)
            {
                ShowSidebar = true
            };

            Assert.Contains(_boolWrites, w => w.Key == ShowSidebarKey && w.Value);
        }

        [Fact]
        public void SetShowSidebarTransient_AppliesValueWithoutPersisting()
        {
            var vm = new MainWindowViewModel(TestProfile);

            vm.SetShowSidebarTransient(true);

            Assert.True(vm.ShowSidebar);
            Assert.DoesNotContain(_boolWrites, w => w.Key == ShowSidebarKey);
        }

        [Fact]
        public void SetShowSidebarTransient_EqualWriteBack_DoesNotPersist()
        {
            // Simulates the TwoWay IsPaneOpen binding writing the same value back
            // after a transient set: the equal value must not slip into persistence.
            var vm = new MainWindowViewModel(TestProfile);

            vm.SetShowSidebarTransient(true);
            vm.ShowSidebar = true;

            Assert.DoesNotContain(_boolWrites, w => w.Key == ShowSidebarKey);
        }

        [Fact]
        public void ShowSidebar_RealChangeAfterTransientSet_PersistsAgain()
        {
            var vm = new MainWindowViewModel(TestProfile);

            vm.SetShowSidebarTransient(true);
            vm.ShowSidebar = false;

            Assert.Contains(_boolWrites, w => w.Key == ShowSidebarKey && !w.Value);
        }

        [Fact]
        public void TitleBarVisibility_FollowsChromeHidden()
        {
            var vm = new MainWindowViewModel(TestProfile);

            Assert.Equal(Visibility.Visible, vm.TitleBarVisibility);

            vm.IsChromeHiddenForFullscreen = true;
            Assert.Equal(Visibility.Collapsed, vm.TitleBarVisibility);

            vm.IsChromeHiddenForFullscreen = false;
            Assert.Equal(Visibility.Visible, vm.TitleBarVisibility);
        }

        [Fact]
        public void HorizontalTabsVisibility_CollapsedWhenChromeHidden_EvenInStandardLayout()
        {
            var vm = new MainWindowViewModel(TestProfile)
            {
                TabLayout = TabLayout.standard,
                IsChromeHiddenForFullscreen = true
            };
            Assert.Equal(Visibility.Collapsed, vm.HorizontalTabsVisibility);

            vm.IsChromeHiddenForFullscreen = false;
            Assert.Equal(Visibility.Visible, vm.HorizontalTabsVisibility);
        }

        [Fact]
        public void BookmarkBarVisibility_CollapsedWhenChromeHidden()
        {
            var vm = new MainWindowViewModel(TestProfile)
            {
                ShowBookmarkBar = true,
                IsChromeHiddenForFullscreen = true
            };
            Assert.Equal(Visibility.Collapsed, vm.BookmarkBarVisibility);

            vm.IsChromeHiddenForFullscreen = false;
            Assert.Equal(Visibility.Visible, vm.BookmarkBarVisibility);
        }

        [Fact]
        public void CompactTabsVisibility_UnaffectedByChromeHidden()
        {
            // The compact tab strip lives inside the title bar, which collapses
            // as a whole, so its own visibility must not change.
            var vm = new MainWindowViewModel(TestProfile)
            {
                TabLayout = TabLayout.compact,
                IsChromeHiddenForFullscreen = true
            };

            Assert.Equal(Visibility.Visible, vm.CompactTabsVisibility);
        }

        [Fact]
        public void IsChromeHiddenForFullscreen_RaisesVisibilityChangeNotifications()
        {
            var vm = new MainWindowViewModel(TestProfile);
            var raised = new List<string>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            vm.IsChromeHiddenForFullscreen = true;

            Assert.Contains(nameof(MainWindowViewModel.TitleBarVisibility), raised);
            Assert.Contains(nameof(MainWindowViewModel.HorizontalTabsVisibility), raised);
            Assert.Contains(nameof(MainWindowViewModel.BookmarkBarVisibility), raised);
        }
    }
}
