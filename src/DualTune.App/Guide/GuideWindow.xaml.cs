using System.Windows;
using DualTune.App.Theme;

namespace DualTune.App.Guide;

public partial class GuideWindow : Window
{
    public GuideWindow(GuideViewModel vm)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        DataContext = vm;
        vm.CloseRequested += Close;
        Closed += (_, _) =>
        {
            vm.CloseRequested -= Close;
            vm.Close();
        };
    }
}
