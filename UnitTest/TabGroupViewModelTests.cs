using Xunit;

namespace Orion
{
    public class TabGroupViewModelTests
    {
        private static (TabGroupViewModel Vm, BrowserTab[] Tabs) MakeGroup(int tabCount)
        {
            var group = new TabGroup();
            for (int i = 0; i < tabCount; i++)
                group.AddTab(new BrowserTab { Id = i + 1, Title = $"Tab {i + 1}" });
            return (new TabGroupViewModel(group), [.. group.Tabs]);
        }

        [Fact]
        public void RemoveTab_ReturnToLastActive_ActivatesPreviouslyActiveTab()
        {
            (TabGroupViewModel vm, BrowserTab[] tabs) = MakeGroup(3);
            vm.ActiveTab = tabs[0];
            vm.ActiveTab = tabs[2];

            vm.RemoveTab(tabs[2], returnToLastActive: true);

            Assert.Equal(tabs[0], vm.ActiveTab);
        }

        [Fact]
        public void RemoveTab_NoReturnToLastActive_ActivatesNextTab()
        {
            (TabGroupViewModel vm, BrowserTab[] tabs) = MakeGroup(3);
            vm.ActiveTab = tabs[0];
            vm.ActiveTab = tabs[1];

            vm.RemoveTab(tabs[1], returnToLastActive: false);

            Assert.Equal(tabs[2], vm.ActiveTab);
        }

        [Fact]
        public void RemoveTab_ReturnToLastActive_FallsBackToNextTabWithoutPreviousTab()
        {
            (TabGroupViewModel vm, BrowserTab[] tabs) = MakeGroup(3);
            vm.ActiveTab = tabs[1];

            vm.RemoveTab(tabs[1], returnToLastActive: true);

            Assert.Equal(tabs[2], vm.ActiveTab);
        }

        [Fact]
        public void RemoveTab_ReturnToLastActive_FallsBackToNeighborWhenPreviousTabGone()
        {
            (TabGroupViewModel vm, BrowserTab[] tabs) = MakeGroup(4);
            vm.ActiveTab = tabs[0];
            vm.ActiveTab = tabs[1];
            vm.ActiveTab = tabs[3];

            vm.Model.RemoveTab(tabs[1]); // close others / move to group bypass the view model

            vm.RemoveTab(tabs[3], returnToLastActive: true);

            Assert.Equal(tabs[2], vm.ActiveTab);
        }

        [Fact]
        public void RemoveTab_BackgroundTab_KeepsActiveTab()
        {
            (TabGroupViewModel vm, BrowserTab[] tabs) = MakeGroup(3);
            vm.ActiveTab = tabs[0];
            vm.ActiveTab = tabs[2];

            vm.RemoveTab(tabs[1], returnToLastActive: true);

            Assert.Equal(tabs[2], vm.ActiveTab);
        }

        [Fact]
        public void RemoveTab_ReturnToLastActive_SecondConsecutiveCloseFallsBackToNeighbor()
        {
            (TabGroupViewModel vm, BrowserTab[] tabs) = MakeGroup(4);
            vm.ActiveTab = tabs[0];
            vm.ActiveTab = tabs[1];
            vm.ActiveTab = tabs[3];

            vm.RemoveTab(tabs[3], returnToLastActive: true);
            Assert.Equal(tabs[1], vm.ActiveTab);

            // The previous-tab memory is one step deep (macOS/gtk parity):
            // it now points at the closed tab, so the neighbor is selected.
            vm.RemoveTab(tabs[1], returnToLastActive: true);
            Assert.Equal(tabs[2], vm.ActiveTab);
        }
    }
}
