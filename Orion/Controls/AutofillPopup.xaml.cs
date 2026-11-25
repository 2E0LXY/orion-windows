using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace Orion
{
    public sealed partial class AutofillPopup : UserControl
    {
        public event Action<PasswordEntry>? CredentialSelected;

        private FrameworkElement? _parentElement;

        public bool IsOpen => PickerPopup.IsOpen;

        public AutofillPopup() => InitializeComponent();

        public void Show(FrameworkElement parent, double offsetX, double offsetY,
            string site, List<PasswordEntry> entries)
        {
            if (entries.Count == 0)
                return;

            _parentElement = parent;
            SiteText.Text = site;
            ItemsPanel.Children.Clear();

            foreach (PasswordEntry entry in entries)
            {
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 10, 12, 10),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(4),
                    Tag = entry,
                    Content = new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = entry.Username,
                                FontSize = 14,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                            },
                            new TextBlock
                            {
                                Text = "••••••••••",
                                FontSize = 12,
                                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                            },
                        }
                    }
                };
                button.Click += OnEntryClicked;
                ItemsPanel.Children.Add(button);
            }

            OverlayCanvas.Width = parent.ActualWidth;
            OverlayCanvas.Height = parent.ActualHeight;
            Canvas.SetTop(PickerBorder, offsetY);

            parent.SizeChanged += OnParentSizeChanged;

            PickerPopup.XamlRoot = parent.XamlRoot;
            PickerPopup.IsOpen = true;

            PickerBorder.UpdateLayout();
            Canvas.SetLeft(PickerBorder, offsetX - (PickerBorder.ActualWidth / 2));
        }

        public void Close()
        {
            if (PickerPopup.IsOpen)
                PickerPopup.IsOpen = false;
        }

        private void OnEntryClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: PasswordEntry entry })
            {
                Close();
                CredentialSelected?.Invoke(entry);
            }
        }

        private void OnParentSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_parentElement != null)
            {
                OverlayCanvas.Width = _parentElement.ActualWidth;
                OverlayCanvas.Height = _parentElement.ActualHeight;
            }
        }

        private void PickerPopup_Closed(object sender, object e)
        {
            _parentElement?.SizeChanged -= OnParentSizeChanged;
            _parentElement = null;
            ItemsPanel.Children.Clear();
        }
    }
}
