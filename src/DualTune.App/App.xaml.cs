using System.IO;
using System.Windows;
using DualTune.App.Engine;
using DualTune.App.Localization;
using DualTune.App.ViewModels;
using DualTune.Core;
using DualTune.Device;
using DualTune.Output;

namespace DualTune.App;

public partial class App : Application
{
    private MainViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Dossier de données gardé sous l'ancien nom « DualTune » : les profils existants restent en place.
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DualTune");
        AppLog.Init(dataDir);
        AppLog.Info("Démarrage de Vectra");
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error(args.Exception);
            MessageBox.Show(args.Exception.Message, Loc.T("app.errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) AppLog.Error(ex);
        };

        try
        {
            var hider = new HidHideController(TryCreateHidHide(), dataDir);
            hider.RecoverFromCrash();
            // Avant toute recherche de manette : sinon une manette déjà masquée par un autre logiciel reste invisible.
            if (Environment.ProcessPath is { } exe) hider.RegisterApp(exe);

            var source = new DualSenseSource();
            var pad = new ViGemPad();
            var engine = new InputEngine(source, pad);

            _vm = new MainViewModel(engine, new ProfileStore(dataDir), pad, hider, source);
            engine.Start();

            MainWindow = new MainWindow { DataContext = _vm };
            MainWindow.Show();
            if (_vm.ShouldShowGuide) _vm.OpenGuide();
        }
        catch (Exception ex)
        {
            // Sans fenêtre, le processus resterait vivant mais invisible : on prévient puis on quitte.
            AppLog.Error(ex);
            MessageBox.Show(Loc.F("app.startFailed", ex.Message, Path.Combine(dataDir, "log.txt")),
                "Vectra",MessageBoxButton.OK, MessageBoxImage.Error);
            _vm?.Shutdown();
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _vm?.Shutdown();
        AppLog.Info("Fermeture de Vectra");
        base.OnExit(e);
    }

    private static IHidHideDriver? TryCreateHidHide()
    {
        try
        {
            return new NefariusHidHideDriver();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return null;
        }
    }
}
