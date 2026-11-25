using Microsoft.UI.Xaml;

namespace Orion
{
    /// <summary>
    /// Thin shell window for Popular Extensions. All UI lives in PopularExtensionsWindowContent (UserControl).
    /// </summary>
    public sealed partial class PopularExtensionsWindow : Window
    {
        private static PopularExtensionsWindow? _instance;

        public PopularExtensionsWindow()
        {
            InitializeComponent();
            Title = "Popular Extensions";
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

            ExtensionsContent.ParentWindow = this;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(ExtensionsContent.TitleBarElement);
            Utils.SetOrionWindowIcon(this);
        }

        internal static void CloseIfOpen()
        {
            _instance?.Close();
            _instance = null;
        }

        public static void Show(string? category = null, long profileId = 0)
        {
            if (_instance == null)
            {
                _instance = new PopularExtensionsWindow();
                _instance.Closed += (s, e) => _instance = null;
            }

            _instance.ExtensionsContent.ProfileId = profileId;
            _instance.ExtensionsContent.RefreshAndSelect(category);
            _instance.Activate();
        }
    }
}
