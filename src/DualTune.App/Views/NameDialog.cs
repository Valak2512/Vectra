using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DualTune.App.Localization;

namespace DualTune.App.Views;

/// <summary>Petite fenêtre « saisir un nom ». Renvoie null si annulée.</summary>
public static class NameDialog
{
    public static string? Ask(string suggestion)
    {
        var box = new TextBox { Text = suggestion, Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(4) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80 };
        var cancel = new Button { Content = Loc.T("common.cancel"), IsCancel = true, MinWidth = 80 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = Loc.T("profiles.nameLabel"), Margin = new Thickness(0, 0, 0, 6) });
        panel.Children.Add(box);
        panel.Children.Add(buttons);

        var win = new Window
        {
            Title = "Vectra",
            Content = panel,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 320,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.MainWindow,
            Background = (System.Windows.Media.Brush)Application.Current!.FindResource("Side"),
            Foreground = (System.Windows.Media.Brush)Application.Current!.FindResource("Text"),
            BorderBrush = (System.Windows.Media.Brush)Application.Current!.FindResource("Red"),
            BorderThickness = new Thickness(0, 3, 0, 0),
        };
        Theme.DarkTitleBar.Apply(win);
        ok.Click += (_, _) => win.DialogResult = true;
        win.Loaded += (_, _) =>
        {
            box.SelectAll();
            Keyboard.Focus(box);
        };
        return win.ShowDialog() == true ? box.Text : null;
    }

    /// <summary>Emplacement où exporter un profil ; null si annulé.</summary>
    public static string? PickSavePath(string fileName)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog { FileName = fileName, Filter = Loc.T("profiles.fileFilter"), DefaultExt = ".json" };
        return dlg.ShowDialog(Application.Current?.MainWindow) == true ? dlg.FileName : null;
    }

    /// <summary>Profil à importer ; null si annulé.</summary>
    public static string? PickOpenPath()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = Loc.T("profiles.fileFilter") };
        return dlg.ShowDialog(Application.Current?.MainWindow) == true ? dlg.FileName : null;
    }

    public static bool Confirm(string question) =>
        MessageBox.Show(question, "Vectra", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
