using Avalonia.Controls;

namespace MirahelpBLEToolkit
{
    public sealed partial class WidgetTile : UserControl
    {
        public WidgetTile()
        {
            InitializeComponent();
        }

        public void SetTitle(System.String title)
        {
            FooterTitle.Text = title ?? System.String.Empty;
        }

        public void SetContent(Control control)
        {
            ContentHost.Content = control;
        }
    }
}
