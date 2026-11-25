using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Orion
{
    public class OrionTabViewItem : TabViewItem
    {
        /// <summary>
        /// Shows an indeterminate ProgressRing in the icon slot. TabViewItem's
        /// template has no loading state, so the ring is inserted beside the
        /// icon box; IconSource is expected to be null while this is true.
        /// </summary>
        public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
            nameof(IsLoading), typeof(bool), typeof(OrionTabViewItem),
            new PropertyMetadata(false, (d, _) => ((OrionTabViewItem)d).UpdateLoadingRing()));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        private ProgressRing? _loadingRing;

        public OrionTabViewItem()
        {
            var opaque = (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"];
            Resources["TabViewItemHeaderCloseButtonBackground"] = opaque;
            Resources["TabViewItemHeaderPointerOverCloseButtonBackground"] = opaque;
            Resources["TabViewItemHeaderSelectedCloseButtonBackground"] = opaque;
            Resources["TabViewItemHeaderPressedCloseButtonBackground"] = opaque;
            Resources["TabViewItemHeaderCloseButtonBackgroundPointerOver"] = opaque;
            Resources["TabViewItemHeaderCloseButtonBackgroundPressed"] = opaque;
            Resources["TabViewItemHeaderCloseButtonWidth"] = 20.0;
            Resources["TabViewItemHeaderCloseButtonHeight"] = 20.0;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("TabContainer") is Grid container
                && GetTemplateChild("IconBox") is FrameworkElement iconBox)
            {
                // Placed like IconBox (column 0), which the NoIcon state
                // collapses while loading. Kept collapsed while idle: a
                // collapsed control does not expand its template.
                _loadingRing = new ProgressRing
                {
                    Width = iconBox.MaxWidth,
                    Height = iconBox.MaxHeight,
                    Margin = iconBox.Margin,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                container.Children.Add(_loadingRing);
                UpdateLoadingRing();
            }

            if (GetTemplateChild("CloseButton") is not FrameworkElement closeButton)
                return;

            Grid.SetColumn(closeButton, 0);
            Grid.SetColumnSpan(closeButton, 2);
            closeButton.HorizontalAlignment = HorizontalAlignment.Right;

            closeButton.Opacity = 0;
            closeButton.IsHitTestVisible = false;
            PointerEntered += (_, _) =>
            {
                closeButton.Opacity = 1;
                closeButton.IsHitTestVisible = true;
            };
            PointerExited += (_, _) =>
            {
                closeButton.Opacity = 0;
                closeButton.IsHitTestVisible = false;
            };
        }

        private void UpdateLoadingRing()
        {
            if (_loadingRing == null)
                return;
            _loadingRing.IsActive = IsLoading;
            _loadingRing.Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
