using System.Collections.Generic;
using Xunit;

namespace Orion
{
    public class BrowserTabViewModelTests
    {
        [Fact]
        public void HasNativeWebView_FalseWhenNull()
        {
            var vm = new BrowserTabViewModel(new BrowserTab());
            Assert.False(vm.HasNativeWebView);
        }

        [Fact]
        public void HasNativeWebView_TrueWhenSet()
        {
            var vm = new BrowserTabViewModel(new BrowserTab()) { NativeWebView = new NativeWebView() };
            Assert.True(vm.HasNativeWebView);
        }

        [Fact]
        public void GoBackCommand_CannotExecuteWhenNoWebView()
        {
            var vm = new BrowserTabViewModel(new BrowserTab());
            Assert.False(vm.GoBackCommand.CanExecute(null));
        }

        [Fact]
        public void GoBackCommand_CannotExecuteWithoutBackHistory()
        {
            // A web view alone is not enough — the engine must have reported
            // somewhere to go back to (NavigationStateChanged → CanGoBack).
            var vm = new BrowserTabViewModel(new BrowserTab()) { NativeWebView = new NativeWebView() };
            Assert.False(vm.GoBackCommand.CanExecute(null));
        }

        [Fact]
        public void GoBackCommand_CanExecuteWhenWebViewSetAndCanGoBack()
        {
            var vm = new BrowserTabViewModel(new BrowserTab())
            {
                NativeWebView = new NativeWebView(),
                CanGoBack = true,
            };
            Assert.True(vm.GoBackCommand.CanExecute(null));
        }

        [Fact]
        public void CanGoBack_WithoutWebView_DoesNotEnableGoBack()
        {
            var vm = new BrowserTabViewModel(new BrowserTab()) { CanGoBack = true };
            Assert.False(vm.GoBackCommand.CanExecute(null));
        }

        [Fact]
        public void SetCanGoForward_FiresCanExecuteChangedOnGoForward()
        {
            var vm = new BrowserTabViewModel(new BrowserTab()) { NativeWebView = new NativeWebView() };
            bool changed = false;
            vm.GoForwardCommand.CanExecuteChanged += (_, _) => changed = true;

            vm.CanGoForward = true;

            Assert.True(changed);
            Assert.True(vm.GoForwardCommand.CanExecute(null));
        }

        [Fact]
        public void SetNativeWebView_FiresPropertyChangedForHasNativeWebView()
        {
            var vm = new BrowserTabViewModel(new BrowserTab());
            var raised = new List<string>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            vm.NativeWebView = new NativeWebView();

            Assert.Contains(nameof(BrowserTabViewModel.HasNativeWebView), raised);
        }

        [Fact]
        public void SetNativeWebView_FiresCanExecuteChangedOnCommands()
        {
            var vm = new BrowserTabViewModel(new BrowserTab());
            bool goBackChanged = false;
            bool goForwardChanged = false;
            bool reloadChanged = false;
            vm.GoBackCommand.CanExecuteChanged += (_, _) => goBackChanged = true;
            vm.GoForwardCommand.CanExecuteChanged += (_, _) => goForwardChanged = true;
            vm.ReloadCommand.CanExecuteChanged += (_, _) => reloadChanged = true;

            vm.NativeWebView = new NativeWebView();

            Assert.True(goBackChanged);
            Assert.True(goForwardChanged);
            Assert.True(reloadChanged);
        }
    }
}
