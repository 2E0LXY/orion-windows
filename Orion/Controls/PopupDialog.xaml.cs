using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace Orion
{
    public enum PopupDialogResult
    {
        None,
        Primary,
        Secondary,
        Close
    }

    public sealed partial class PopupDialog : UserControl
    {
        private TaskCompletionSource<PopupDialogResult>? _tcs;
        private FrameworkElement? _parentElement;

        public PopupDialogResult Result { get; private set; } = PopupDialogResult.None;

        public string Title
        {
            get => TitleText.Text;
            set => TitleText.Text = value;
        }

        public object DialogContent
        {
            get => ContentArea.Content;
            set => ContentArea.Content = value;
        }

        public string? PrimaryButtonText
        {
            get => PrimaryButton.Content as string;
            set
            {
                PrimaryButton.Content = value;
                PrimaryButton.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public string? SecondaryButtonText
        {
            get => SecondaryButton.Content as string;
            set
            {
                SecondaryButton.Content = value;
                SecondaryButton.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public string? CloseButtonText
        {
            get => CloseButton.Content as string;
            set
            {
                CloseButton.Content = value;
                CloseButton.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public PopupDialog() => InitializeComponent();

        /// <summary>
        /// Shows the dialog as a modal popup over the specified parent element.
        /// </summary>
        public Task<PopupDialogResult> ShowAsync(FrameworkElement parent)
        {
            _tcs = new TaskCompletionSource<PopupDialogResult>();
            _parentElement = parent;

            // Size overlay to match parent
            UpdateOverlaySize();
            parent.SizeChanged += OnParentSizeChanged;

            // Show popup
            DialogPopup.XamlRoot = parent.XamlRoot;
            DialogPopup.IsOpen = true;

            return _tcs.Task;
        }

        private void UpdateOverlaySize()
        {
            if (_parentElement != null)
            {
                OverlayGrid.Width = _parentElement.ActualWidth;
                OverlayGrid.Height = _parentElement.ActualHeight;
            }
        }

        private void OnParentSizeChanged(object sender, SizeChangedEventArgs e) => UpdateOverlaySize();

        private void CloseWithResult(PopupDialogResult result)
        {
            Result = result;

            _parentElement?.SizeChanged -= OnParentSizeChanged;

            DialogPopup.IsOpen = false;
            _ = _tcs?.TrySetResult(result);
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e) => CloseWithResult(PopupDialogResult.Primary);

        private void SecondaryButton_Click(object sender, RoutedEventArgs e) => CloseWithResult(PopupDialogResult.Secondary);

        private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseWithResult(PopupDialogResult.Close);

        /// <summary>
        /// Shows a simple message dialog.
        /// </summary>
        public static async Task<PopupDialogResult> ShowMessageAsync(
            FrameworkElement parent,
            string title,
            string message,
            string? primaryButton = null,
            string? secondaryButton = null,
            string? closeButton = "OK")
        {
            var dialog = new PopupDialog
            {
                Title = title,
                DialogContent = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = primaryButton,
                SecondaryButtonText = secondaryButton,
                CloseButtonText = closeButton
            };

            return await dialog.ShowAsync(parent);
        }

        /// <summary>
        /// Shows a confirmation dialog. Returns true if confirmed.
        /// </summary>
        public static async Task<bool> ShowConfirmAsync(
            FrameworkElement parent,
            string title,
            string? message = null,
            string confirmButton = "OK",
            string cancelButton = "Cancel")
        {
            var dialog = new PopupDialog
            {
                Title = title,
                PrimaryButtonText = confirmButton,
                CloseButtonText = cancelButton
            };

            if (message != null)
            {
                dialog.DialogContent = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                };
            }

            PopupDialogResult result = await dialog.ShowAsync(parent);
            return result == PopupDialogResult.Primary;
        }

        /// <summary>
        /// Shows a single-text-input dialog. Returns the entered text, or null if cancelled.
        /// </summary>
        public static async Task<string?> ShowInputAsync(
            FrameworkElement parent,
            string title,
            string label,
            string? placeholder = null,
            string? defaultValue = null,
            string confirmButton = "OK",
            string cancelButton = "Cancel")
        {
            var textBox = new TextBox
            {
                PlaceholderText = placeholder ?? "",
                Text = defaultValue ?? ""
            };

            var stackPanel = new StackPanel { Spacing = 8 };
            stackPanel.Children.Add(new TextBlock { Text = label });
            stackPanel.Children.Add(textBox);

            var dialog = new PopupDialog
            {
                Title = title,
                DialogContent = stackPanel,
                PrimaryButtonText = confirmButton,
                CloseButtonText = cancelButton
            };

            PopupDialogResult result = await dialog.ShowAsync(parent);
            return result == PopupDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text)
                ? textBox.Text
                : null;
        }
    }
}
