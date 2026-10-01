using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using DirectoryManagement.Services;
using MessageBox = System.Windows.MessageBox;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;

namespace DirectoryManagement;

public partial class ScanTargetDialog : Window
{
    private bool _suppressPathEvents;
    private bool _scanAccepted;
    private readonly AppTheme _themeBeforeDialog;
    private TaskCompletionSource<string?>? _selectionTcs;

    public string SelectedPath { get; private set; } = "";

    public Task<string?> WaitForSelectionAsync()
    {
        _selectionTcs = new TaskCompletionSource<string?>();
        return _selectionTcs.Task;
    }

    public Task FadeInAsync(TimeSpan duration)
    {
        var tcs = new TaskCompletionSource();
        Opacity = 0;

        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = duration,
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            Opacity = 1;
            tcs.TrySetResult();
        };

        BeginAnimation(OpacityProperty, animation);
        return tcs.Task;
    }

    public Task FadeOutAsync(TimeSpan duration)
    {
        var tcs = new TaskCompletionSource();

        var animation = new DoubleAnimation
        {
            From = Opacity,
            To = 0,
            Duration = duration,
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            Opacity = 0;
            tcs.TrySetResult();
        };

        BeginAnimation(OpacityProperty, animation);
        return tcs.Task;
    }

    public ScanTargetDialog()
    {
        _themeBeforeDialog = ThemeService.Current;
        ThemeService.Apply(AppTheme.Light);

        InitializeComponent();
        Opacity = 0;
        AppIcon.ApplyTo(this);
        LocalizationService.AttachLanguageComboBox(LanguageComboBox);
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalization();
        LoadDrives();
        PathBox.Text = AppPaths.ApplicationRoot;
    }

    private void Chrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            e.Handled = true;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CancelButton_Click(sender, e);

    protected override void OnClosed(EventArgs e)
    {
        if (_selectionTcs is { Task.IsCompleted: false } tcs)
        {
            var path = _scanAccepted && !string.IsNullOrWhiteSpace(SelectedPath)
                ? SelectedPath
                : null;
            tcs.TrySetResult(path);
        }

        base.OnClosed(e);
        if (ThemeService.Current != _themeBeforeDialog)
        {
            ThemeService.Apply(_themeBeforeDialog);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyLocalization();
        LoadDrives();
    }

    private void ApplyLocalization()
    {
        FlowDirection = LocalizationService.FlowDirection;
        Title = LocalizationService.T("ScanDialogTitle");
        Localize.RefreshTree(this);
        LocalizationService.SyncLanguageComboBoxSelection(LanguageComboBox);
    }

    private void LoadDrives()
    {
        DriveList.Items.Clear();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady).OrderBy(d => d.Name))
            {
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? LocalizationService.T("DriveNoLabel")
                    : drive.VolumeLabel;
                var display = FormatDriveDisplay(drive, label);
                DriveList.Items.Add(new DriveListItem(drive.Name, display));
            }
        }
        catch
        {
            // ignore drive enumeration errors
        }
    }

    private static string FormatDriveDisplay(DriveInfo drive, string label)
    {
        var total = drive.TotalSize;
        var free = drive.AvailableFreeSpace;
        var used = Math.Max(0, total - free);
        var usedPercent = total > 0 ? used * 100.0 / total : 0;
        var freePercent = total > 0 ? free * 100.0 / total : 0;

        return string.Format(
            LocalizationService.T("DriveLine"),
            drive.Name,
            label,
            SizeFormatter.FormatBytes(total),
            SizeFormatter.FormatBytes(free),
            usedPercent,
            freePercent);
    }

    private void DriveList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DriveList.SelectedItem is DriveListItem item)
        {
            _suppressPathEvents = true;
            PathBox.Text = item.RootPath;
            _suppressPathEvents = false;
        }
    }

    private void DriveList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DriveList.SelectedItem is not null)
        {
            StartButton_Click(sender, e);
        }
    }

    private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressPathEvents)
        {
            return;
        }

        DriveList.SelectedItem = null;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = LocalizationService.T("BrowseFolderTitle"),
            Multiselect = false
        };

        var current = PathBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            dialog.InitialDirectory = current;
        }

        if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            _suppressPathEvents = true;
            PathBox.Text = dialog.FolderName;
            _suppressPathEvents = false;
            DriveList.SelectedItem = null;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _scanAccepted = false;
        Close();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var raw = PathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            MessageBox.Show(this, LocalizationService.T("MsgEnterPath"), LocalizationService.T("MsgScanTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string full;
        try
        {
            full = Path.GetFullPath(raw);
        }
        catch
        {
            MessageBox.Show(this, LocalizationService.T("MsgInvalidPath"), LocalizationService.T("MsgScanTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(full))
        {
            MessageBox.Show(this, LocalizationService.T("MsgFolderNotFound"), LocalizationService.T("MsgScanTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SelectedPath = full.TrimEnd('\\');
        _scanAccepted = true;
        IsEnabled = false;
        await FadeOutAsync(App.ScanDialogFadeOutDuration);
        Close();
    }

    private sealed class DriveListItem(string rootPath, string display)
    {
        public string RootPath { get; } = rootPath;
        public string Display { get; } = display;
    }
}
