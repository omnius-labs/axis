using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Omnius.Axis.Ui.Desktop.Windows.Main;

public partial class StatusView : UserControl
{
    public StatusView()
    {
        this.InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}