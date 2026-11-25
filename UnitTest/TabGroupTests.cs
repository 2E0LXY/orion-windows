using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Xunit;

namespace Orion
{
    public class TabGroupTests
    {
        private static TabGroup MakeGroup(int tabCount = 0)
        {
            var group = new TabGroup();
            for (int i = 0; i < tabCount; i++)
            {
                group.AddTab(new BrowserTab { Id = i + 1, Title = $"Tab {i + 1}" });
            }

            return group;
        }

        [Fact]
        public void DisplayName_EmptyUnsaved_ShowsZeroTabs()
        {
            var group = new TabGroup();
            Assert.Equal("0 tabs", group.DisplayName);
        }

        [Fact]
        public void DisplayName_OneTab_Singular()
        {
            TabGroup group = MakeGroup(1);
            Assert.Equal("1 tab", group.DisplayName);
        }

        [Fact]
        public void DisplayName_ThreeTabs_Plural()
        {
            TabGroup group = MakeGroup(3);
            Assert.Equal("3 tabs", group.DisplayName);
        }

        [Fact]
        public void DisplayName_SavedGroup_ShowsNameAndCount()
        {
            TabGroup group = MakeGroup(2);
            group.Name = "MyGroup";
            group.IsSaved = true;
            Assert.Equal("MyGroup (2)", group.DisplayName);
        }

        [Fact]
        public void SetIsSaved_FiresPropertyChangedForIsSavedAndDisplayName()
        {
            var group = new TabGroup();
            var raised = new List<string>();
            group.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

            group.IsSaved = true;

            Assert.Contains(nameof(TabGroup.IsSaved), raised);
            Assert.Contains(nameof(TabGroup.DisplayName), raised);
        }

        [Fact]
        public void AddTab_IncreasesTabsCount()
        {
            var group = new TabGroup();
            group.AddTab(new BrowserTab());
            _ = Assert.Single(group.Tabs);
        }

        [Fact]
        public void AddTab_FiresResetOnFlattenedTabCollection()
        {
            var group = new TabGroup();
            bool resetFired = false;
            group.FlattenedTabCollection.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    resetFired = true;
                }
            };

            group.AddTab(new BrowserTab());

            Assert.True(resetFired);
        }

        [Fact]
        public void RemoveTab_RemovesTabFromGroup()
        {
            TabGroup group = MakeGroup(2);
            BrowserTab tab = group.Tabs[0];
            group.RemoveTab(tab);
            Assert.DoesNotContain(tab, group.Tabs);
        }

        [Fact]
        public void RemoveTab_WithChildren_PromotesChildrenToParentCollection()
        {
            var group = new TabGroup();
            var parent = new BrowserTab { Id = 1 };
            var child = new BrowserTab { Id = 2 };
            parent.Children.Add(child);
            group.AddTab(parent);
            group.AddTab(new BrowserTab { Id = 3 });

            group.RemoveTab(parent);

            Assert.DoesNotContain(parent, group.Tabs);
            Assert.Contains(child, group.Tabs);
        }

        [Fact]
        public void RemoveActiveTab_SetsNewActiveTab()
        {
            TabGroup group = MakeGroup(3);
            var vm = new TabGroupViewModel(group);
            BrowserTab second = group.Tabs[1];
            vm.ActiveTab = second;

            vm.RemoveTab(second, returnToLastActive: false);

            Assert.NotNull(vm.ActiveTab);
            Assert.NotEqual(second, vm.ActiveTab);
        }

        [Fact]
        public void TabExists_ReturnsTrueForRootTab()
        {
            TabGroup group = MakeGroup(1);
            Assert.True(group.TabExists(group.Tabs[0]));
        }

        [Fact]
        public void TabExists_ReturnsFalseForUnknownTab()
        {
            TabGroup group = MakeGroup(1);
            Assert.False(group.TabExists(new BrowserTab { Id = 99 }));
        }

        [Fact]
        public void FindContainingCollection_ReturnsTabs_ForRootTab()
        {
            TabGroup group = MakeGroup(1);
            BrowserTab tab = group.Tabs[0];
            Assert.Same(group.Tabs, group.FindContainingCollection(tab));
        }

        [Fact]
        public void FindContainingCollection_ReturnsChildrenCollection_ForNestedTab()
        {
            var group = new TabGroup();
            var parent = new BrowserTab { Id = 1 };
            var child = new BrowserTab { Id = 2 };
            parent.Children.Add(child);
            group.AddTab(parent);

            ObservableCollection<BrowserTab> collection = group.FindContainingCollection(child);

            Assert.Same(parent.Children, collection);
        }

        [Fact]
        public void FindContainingCollection_Throws_ForUnknownTab()
        {
            TabGroup group = MakeGroup(1);
            _ = Assert.Throws<System.ArgumentException>(() => group.FindContainingCollection(new BrowserTab { Id = 99 }));
        }
    }
}
