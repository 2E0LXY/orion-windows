using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Orion
{
    public sealed partial class ResetWindowContent : UserControl
    {
        internal UIElement TitleBarElement => AppTitleBar;
        internal Window? ParentWindow { get; set; }

        public ResetWindowContent()
        {
            InitializeComponent();
            ThemeService.Register(this);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => ParentWindow?.Close();

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: perform reset operations based on checked items
            ParentWindow?.Close();
        }
    }
}
