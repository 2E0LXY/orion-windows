using System.Collections.Specialized;
using Xunit;

namespace Orion
{
    public class FlattenedTabCollectionTests
    {
        // Flat group: root0, root1, root2
        private readonly TabGroup _flat;
        private readonly BrowserTab _root0, _root1, _root2;

        // Nested group: root0 → [child0, child1], root1
        // Depth-first order: root0, child0, child1, root1
        private readonly TabGroup _nested;
        private readonly BrowserTab _nroot0, _nchild0, _nchild1, _nroot1;

        public FlattenedTabCollectionTests()
        {
            _flat = new TabGroup();
            _root0 = new BrowserTab { Id = 1 };
            _root1 = new BrowserTab { Id = 2 };
            _root2 = new BrowserTab { Id = 3 };
            _flat.Tabs.Add(_root0);
            _flat.Tabs.Add(_root1);
            _flat.Tabs.Add(_root2);

            _nested = new TabGroup();
            _nroot0 = new BrowserTab { Id = 1 };
            _nchild0 = new BrowserTab { Id = 2 };
            _nchild1 = new BrowserTab { Id = 3 };
            _nroot1 = new BrowserTab { Id = 4 };
            _nroot0.Children.Add(_nchild0);
            _nroot0.Children.Add(_nchild1);
            _nested.Tabs.Add(_nroot0);
            _nested.Tabs.Add(_nroot1);
        }

        [Fact]
        public void Count_EmptyGroup_IsZero() => Assert.Empty(new TabGroup().FlattenedTabCollection);

        [Fact]
        public void Count_FlatGroup_MatchesTabCount() => Assert.Equal(3, _flat.FlattenedTabCollection.Count);

        [Fact]
        public void Count_WithChildren_IsDepthFirstTotal() => Assert.Equal(4, _nested.FlattenedTabCollection.Count);

        [Fact]
        public void Indexer_ReturnsDepthFirstOrder()
        {
            FlattenedTabCollection flat = _nested.FlattenedTabCollection;
            Assert.Equal(_nroot0, flat[0]);
            Assert.Equal(_nchild0, flat[1]);
            Assert.Equal(_nchild1, flat[2]);
            Assert.Equal(_nroot1, flat[3]);
        }

        [Fact]
        public void Contains_ReturnsTrueForTabInCollection()
        {
            Assert.Contains(_root0, _flat.FlattenedTabCollection);
            Assert.Contains(_root1, _flat.FlattenedTabCollection);
        }

        [Fact]
        public void Contains_ReturnsFalseForUnknownTab() => Assert.DoesNotContain(new BrowserTab { Id = 99 }, _flat.FlattenedTabCollection);

        [Fact]
        public void IndexOf_ReturnsCorrectFlatIndex()
        {
            Assert.Equal(0, _flat.FlattenedTabCollection.IndexOf(_root0));
            Assert.Equal(1, _flat.FlattenedTabCollection.IndexOf(_root1));
            Assert.Equal(2, _flat.FlattenedTabCollection.IndexOf(_root2));
        }

        [Fact]
        public void Insert_AtZero_InsertsBeforeFirstTab()
        {
            var newTab = new BrowserTab { Id = 99 };
            _flat.FlattenedTabCollection.Insert(0, newTab);
            Assert.Equal(newTab, _flat.FlattenedTabCollection[0]);
        }

        [Fact]
        public void Insert_AtCount_AppendsToRoot()
        {
            var newTab = new BrowserTab { Id = 99 };
            int originalCount = _flat.FlattenedTabCollection.Count;
            _flat.FlattenedTabCollection.Insert(originalCount, newTab);
            Assert.Equal(newTab, _flat.FlattenedTabCollection[originalCount]);
        }

        [Fact]
        public void RemoveAt_Insert_ReordersTab()
        {
            // TabView reorder: RemoveAt then Insert at new position
            // Move root0 to the end (index 2 in post-removal list)
            _flat.FlattenedTabCollection.RemoveAt(0);
            _flat.FlattenedTabCollection.Insert(2, _root0);
            Assert.Equal(_root1, _flat.FlattenedTabCollection[0]);
            Assert.Equal(_root2, _flat.FlattenedTabCollection[1]);
            Assert.Equal(_root0, _flat.FlattenedTabCollection[2]);
            Assert.Equal(3, _flat.FlattenedTabCollection.Count);
        }

        [Fact]
        public void Insert_ParentPastChild_DoesNotCrash()
        {
            // Dragging a parent tab past its child must not create a cycle.
            // nroot0 has children [nchild0, nchild1]; flat = [nroot0, nchild0, nchild1, nroot1]
            // Simulate dragging nroot0 to after nchild1 (post-removal index 2)
            _nested.FlattenedTabCollection.RemoveAt(0);
            _nested.FlattenedTabCollection.Insert(2, _nroot0);

            Assert.DoesNotContain(_nroot0, _nroot0.Children);
            Assert.Contains(_nroot0, _nested.FlattenedTabCollection);
            Assert.Contains(_nchild0, _nested.FlattenedTabCollection);
            Assert.Contains(_nchild1, _nested.FlattenedTabCollection);
            Assert.Contains(_nroot1, _nested.FlattenedTabCollection);
            Assert.Equal(4, _nested.FlattenedTabCollection.Count);
        }

        [Fact]
        public void Insert_ParentToAdjacentPosition_DoesNotCrash()
        {
            // Drag nroot0 one position right (post-removal index 1 = nchild0's old position)
            _nested.FlattenedTabCollection.RemoveAt(0);
            _nested.FlattenedTabCollection.Insert(1, _nroot0);

            Assert.DoesNotContain(_nroot0, _nroot0.Children);
            Assert.Equal(4, _nested.FlattenedTabCollection.Count);
        }

        [Fact]
        public void Refresh_FiresCollectionChangedReset()
        {
            bool resetFired = false;
            _flat.FlattenedTabCollection.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    resetFired = true;
                }
            };

            _flat.FlattenedTabCollection.Refresh();

            Assert.True(resetFired);
        }

        [Fact]
        public void Insert_FiresCollectionChanged()
        {
            bool fired = false;
            _flat.FlattenedTabCollection.CollectionChanged += (_, _) => fired = true;
            _flat.FlattenedTabCollection.Insert(0, new BrowserTab { Id = 99 });
            Assert.True(fired);
        }

        [Fact]
        public void RemoveAt_Insert_FiresCollectionChanged()
        {
            bool fired = false;
            _flat.FlattenedTabCollection.CollectionChanged += (_, _) => fired = true;
            _flat.FlattenedTabCollection.RemoveAt(0);
            _flat.FlattenedTabCollection.Insert(2, _root0);
            Assert.True(fired);
        }
    }
}
