using Microsoft.UI.Xaml;

namespace Orion
{
    /// <summary>
    /// Thin shell window for the Library. All UI lives in LibraryWindowContent (UserControl).
    /// </summary>
    public sealed partial class LibraryWindow : Window
    {
        private static LibraryWindow? _instance;

        public LibraryWindow()
        {
            InitializeComponent();
            Title = "Library";
            AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 600));

            LibraryContent.ParentWindow = this;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(LibraryContent.TitleBarElement);
            Utils.SetOrionWindowIcon(this);
        }

        internal static void CloseIfOpen()
        {
            _instance?.Close();
            _instance = null;
        }

        public static void ShowHistory(string? category = null, long profileId = 0)
        {
            if (_instance == null)
            {
                _instance = new LibraryWindow();
                _instance.Closed += (s, e) => _instance = null;
            }

            _instance.LibraryContent.ProfileId = profileId;
            _instance.LibraryContent.LoadData(category);
            _instance.Activate();
        }
    }
}
