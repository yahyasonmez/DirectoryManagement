using System.Windows;
using DirectoryManagement.Services;

namespace DirectoryManagement;

public partial class App : Application
{
    internal static readonly TimeSpan ScanDialogFadeInDuration = TimeSpan.FromMilliseconds(100);
    internal static readonly TimeSpan ScanDialogFadeOutDuration = TimeSpan.FromMilliseconds(250);

    protected override void OnStartup(StartupEventArgs e)
    {
        LocalizationService.Initialize();
        ThemeService.Initialize();

        _ = RunStartupAsync();
        base.OnStartup(e);
    }

    private static async Task RunStartupAsync()
    {
        var dialog = new ScanTargetDialog();
        var selectionTask = dialog.WaitForSelectionAsync();
        dialog.Show();

        await dialog.FadeInAsync(ScanDialogFadeInDuration);
        dialog.Activate();

        var selectedPath = await selectionTask;
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            Current.Shutdown();
            return;
        }

        var main = new MainWindow();
        Current.MainWindow = main;
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        main.Show();
        await main.StartInitialScanAsync(selectedPath);
    }
}
