using Microsoft.UI.Xaml;

namespace Orion
{
    public sealed partial class ResetWindow : Window
    {
        private static ResetWindow? _instance;

        public ResetWindow()
        {
            InitializeComponent();
            Title = "Reset Orion";
            AppWindow.Resize(new Windows.Graphics.SizeInt32(500, 700));

            ResetContent.ParentWindow = this;
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(ResetContent.TitleBarElement);
            Utils.SetOrionWindowIcon(this);
        }

        internal static void CloseIfOpen()
        {
            _instance?.Close();
            _instance = null;
        }

        public static void Show()
        {
            if (_instance == null)
            {
                _instance = new ResetWindow();
                _instance.Closed += (s, e) => _instance = null;
            }

            _instance.Activate();
        }
    }
}
