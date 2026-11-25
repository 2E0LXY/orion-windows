using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Orion
{
    public sealed partial class StartPage : UserControl
    {
        public StartPage()
        {
            InitializeComponent();
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string url })
                NavigateRequested?.Invoke(this, url);
        }

        public event System.EventHandler<string>? NavigateRequested;
    }
}
