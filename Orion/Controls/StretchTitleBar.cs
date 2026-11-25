using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Orion
{
    public class StretchTitleBar : TitleBar
    {
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("PART_ContentPresenter") is ContentPresenter cp)
                cp.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }
}
