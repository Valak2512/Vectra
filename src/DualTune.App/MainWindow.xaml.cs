using System.Windows;
using DualTune.App.Guide;
using DualTune.App.Theme;
using DualTune.App.ViewModels;

namespace DualTune.App;

public partial class MainWindow : Window
{
    private GuideWindow? _guideWindow;

    public MainWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel old) old.GuideRequested -= ShowGuide;
            if (e.NewValue is MainViewModel vm) vm.GuideRequested += ShowGuide;
        };
    }

    private void ShowGuide(GuideViewModel guide)
    {
        if (_guideWindow is not null)
        {
            _guideWindow.Activate();
            return;
        }

        _guideWindow = new GuideWindow(guide) { Owner = this };
        _guideWindow.Closed += (_, _) =>
        {
            _guideWindow = null;
            (DataContext as MainViewModel)?.GuideClosed();
        };
        _guideWindow.Show();
    }
}
