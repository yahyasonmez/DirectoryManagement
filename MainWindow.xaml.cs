using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using DirectoryManagement.Models;
using DirectoryManagement.Services;
using MessageBox = System.Windows.MessageBox;

namespace DirectoryManagement;

public partial class MainWindow : Window
{
    private readonly CloudStorageRules _cloud = new();
    private readonly DirectoryAnalyzer _analyzer;
    private readonly BinObjCleaner _cleaner;
    private readonly ObservableCollection<SizeEntry> _rows = [];
    private readonly Dictionary<string, FolderScanCache> _folderCache = new(StringComparer.OrdinalIgnoreCase);
    private string _appRoot = "";
    private string _currentPath = "";
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _deleteCts;
    private bool _renameCommitInProgress;
    private long? _currentFolderTotalBytes;
    private readonly List<SizeEntry> _folderRows = [];
    private GridSortColumn _gridSortColumn = GridSortColumn.Size;
    private bool _gridSortAscending;
    private GridTypeFilter _gridTypeFilter = GridTypeFilter.All;
    private int _statusItemCount;
    private string _statusSuffixKey = "StatusListed";
    private long? _statusTotalOverride;
    private readonly List<StoredLogEntry> _logEntries = [];

    private sealed record StoredLogEntry(string Key, object?[] Args, bool Blank);

    private enum GridSortColumn
    {
        Name,
        Type,
        Size
    }

    private enum GridTypeFilter
    {
        All,
        FoldersOnly,
        FilesOnly
    }

    public MainWindow()
    {
        InitializeComponent();
        AppIcon.ApplyTo(this);
        _cloud.Initialize();
        _analyzer = new DirectoryAnalyzer(_cloud);
        _cleaner = new BinObjCleaner(_cloud);
        SizeGrid.ItemsSource = _rows;
        LocalizationService.AttachLanguageComboBox(LanguageComboBox);
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalization();
        UpdateThemeToggleUi();
        UpdateSortGlyphs();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLocalization();

    private void ApplyLocalization()
    {
        FlowDirection = LocalizationService.FlowDirection;
        Title = LocalizationService.T("AppTitle");
        Localize.RefreshTree(this);
        SelectColumn.Header = LocalizationService.T("ColSelect");
        ActionsColumn.Header = LocalizationService.T("ColActions");
        TypeFilterAllMenuItem.Header = LocalizationService.T("FilterAll");
        TypeFilterFolderMenuItem.Header = LocalizationService.T("FilterFolders");
        TypeFilterFileMenuItem.Header = LocalizationService.T("FilterFiles");
        LogGroupBox.Header = LocalizationService.T("LogHeader");
        LocalizationService.SyncLanguageComboBoxSelection(LanguageComboBox);
        UpdateThemeToggleUi();
        CollectionViewSource.GetDefaultView(_rows)?.Refresh();
        if (_statusItemCount > 0 || _currentFolderTotalBytes.HasValue)
        {
            RefreshStatusBar();
        }

        RefreshLogDisplay();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeService.Toggle();
        UpdateThemeToggleUi();
    }

    private void UpdateThemeToggleUi()
    {
        var (actionLabel, switchTooltip, icon) = ThemeService.GetTogglePresentation(ThemeService.Current);
        ThemeToggleButton.ToolTip = switchTooltip;
        ThemeToggleLabel.Text = actionLabel;
        ThemeToggleIcon.Text = icon;
    }

    public async Task StartInitialScanAsync(string selectedPath)
    {
        _appRoot = Path.GetFullPath(selectedPath.TrimEnd('\\'));
        _currentPath = _appRoot;
        _folderCache.Clear();
        UpdateLocationDisplay();
        await ShowFolderAsync(_currentPath, forceRescan: true, "HeadingFirstScan");
    }

    private async void RescanButton_Click(object sender, RoutedEventArgs e) =>
        await ShowFolderAsync(_currentPath, forceRescan: true, "HeadingRescan");

    private async void UpFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_currentPath)?.FullName;
        if (parent is null || !TryNavigateToFolder(parent))
        {
            if (parent is not null && _cloud.IsExternalPath(parent))
            {
                AppendLogKey("LogCloudNavBlocked");
            }
            return;
        }

        await ShowFolderAsync(parent, forceRescan: false, "HeadingUpFolder");
    }

    private void RenameItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not SizeEntry entry)
        {
            return;
        }

        if (!entry.CanRename)
        {
            return;
        }

        CancelAllRenaming();
        entry.EditingName = entry.Name;
        entry.IsRenaming = true;
    }

    private void RenameTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox || textBox.DataContext is not SizeEntry entry)
        {
            return;
        }

        if (!entry.IsRenaming)
        {
            return;
        }

        textBox.Focus();
        textBox.SelectAll();
    }

    private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox || textBox.DataContext is not SizeEntry entry)
        {
            return;
        }

        if (!entry.IsRenaming)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            CommitRename(entry);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            entry.IsRenaming = false;
            e.Handled = true;
        }
    }

    private void RenameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_renameCommitInProgress)
        {
            return;
        }

        if (sender is not System.Windows.Controls.TextBox textBox || textBox.DataContext is not SizeEntry entry)
        {
            return;
        }

        if (!entry.IsRenaming)
        {
            return;
        }

        CommitRename(entry);
    }

    private void CancelAllRenaming()
    {
        foreach (var row in _rows)
        {
            row.IsRenaming = false;
        }
    }

    private void CommitRename(SizeEntry entry)
    {
        if (!entry.IsRenaming || _renameCommitInProgress)
        {
            return;
        }

        _renameCommitInProgress = true;
        try
        {
            var newName = entry.EditingName.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Ad boş olamaz.", "Yeniden adlandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newName.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
            {
                entry.IsRenaming = false;
                return;
            }

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("Geçersiz karakter içeriyor.", "Yeniden adlandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsUnderAppRoot(entry.FullPath))
            {
                MessageBox.Show("Bu öğe yeniden adlandırılamaz.", "Yeniden adlandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                entry.IsRenaming = false;
                return;
            }

            if (_cloud.IsExternalPath(entry.FullPath))
            {
                MessageBox.Show("Harici bulut konumu değiştirilemez.", "Yeniden adlandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                entry.IsRenaming = false;
                return;
            }

            var parentDir = Path.GetDirectoryName(entry.FullPath);
            if (string.IsNullOrWhiteSpace(parentDir))
            {
                entry.IsRenaming = false;
                return;
            }

            var newPath = Path.Combine(parentDir, newName);
            if (File.Exists(newPath) || Directory.Exists(newPath))
            {
                MessageBox.Show("Bu ad zaten kullanılıyor.", "Yeniden adlandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (entry.IsFolder)
            {
                Directory.Move(entry.FullPath, newPath);
            }
            else
            {
                File.Move(entry.FullPath, newPath);
            }

            entry.Name = newName;
            entry.FullPath = newPath;
            entry.IsRenaming = false;
            InvalidateCacheFromPath(_currentPath);
            RefreshCurrentFolderTotalBytesFromGrid();
            AppendLogKey("LogRenamed", newPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Yeniden adlandırma hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            AppendLogKey("LogRenameFailed", entry.FullPath, ex.Message);
        }
        finally
        {
            _renameCommitInProgress = false;
        }
    }

    private void OpenItemInExplorerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not SizeEntry entry)
        {
            return;
        }

        if (!entry.CanDelete || string.IsNullOrWhiteSpace(entry.FullPath))
        {
            return;
        }

        try
        {
            if (entry.IsFolder)
            {
                if (!Directory.Exists(entry.FullPath))
                {
                    AppendLogKey("LogFolderNotFound", entry.FullPath);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = entry.FullPath,
                    UseShellExecute = true
                });
            }
            else
            {
                if (!File.Exists(entry.FullPath))
                {
                    AppendLogKey("LogFileNotFound", entry.FullPath);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{entry.FullPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            AppendLogKey("LogExplorerOpenFailed", entry.FullPath, ex.Message);
            MessageBox.Show(ex.Message, "Gezginde gösterilemedi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void DeleteBinObjRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not SizeEntry entry)
        {
            return;
        }

        if (!entry.IsFolder)
        {
            return;
        }

        var relative = ToCleanupRelativePath(entry.Name);
        await RunBinObjCleanupAsync([relative]);
    }

    private async void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not SizeEntry entry)
        {
            return;
        }

        if (!entry.CanDelete || string.IsNullOrWhiteSpace(entry.FullPath))
        {
            return;
        }

        if (!IsUnderAppRoot(entry.FullPath))
        {
            AppendLogKey("LogDeleteOutsideRoot");
            return;
        }

        if (_cloud.IsExternalPath(entry.FullPath))
        {
            AppendLogKey("LogCloudDeleteBlocked");
            return;
        }

        var kind = entry.IsFolder ? "klasörü" : "dosyayı";
        var sizeHint = entry.IsFolder
            ? $" ({entry.SizeDisplay}, alt öğeler dahil)"
            : $" ({entry.SizeDisplay})";
        var confirmDialog = new ConfirmListDialog(
            "Silme onayı",
            $"\"{entry.Name}\" {kind} kalıcı olarak silinecek{sizeHint}. Devam edilsin mi?",
            [entry.FullPath])
        {
            Owner = this
        };
        if (confirmDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (entry.IsFolder)
            {
                Directory.Delete(entry.FullPath, recursive: true);
            }
            else
            {
                File.Delete(entry.FullPath);
            }

            AppendLogKey("LogDeleted", entry.FullPath);
            InvalidateCacheFromPath(_currentPath);
            if (entry.IsFolder)
            {
                InvalidateCacheFromPath(entry.FullPath);
            }
            await ShowFolderAsync(_currentPath, forceRescan: true, "HeadingAfterDelete");
        }
        catch (Exception ex)
        {
            AppendLogKey("LogDeleteFailed", entry.FullPath, ex.Message);
            MessageBox.Show(ex.Message, "Silme hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SizeGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SizeGrid.SelectedItem is not SizeEntry entry || !entry.IsFolder) return;

        var target = Path.Combine(_currentPath, entry.Name);
        if (!TryNavigateToFolder(target))
        {
            AppendLogKey("LogFolderEnterFailed", entry.Name);
            return;
        }

        await ShowFolderAsync(target, forceRescan: false, "HeadingFolderNav", entry.Name);
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
        LogBox.Clear();
    }

    private void SizeGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var row in _rows.Where(r => r.CanDelete))
        {
            row.IsSelectedForCleanup = SizeGrid.SelectedItems.Contains(row);
        }

    }

    private void SizeGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ClickCount > 1)
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        if (FindVisualParent<CheckBox>(source) is not null)
        {
            ToggleCleanupSelection(FindVisualParent<DataGridRow>(source));
            e.Handled = true;
            return;
        }

        if (IsGridInteractiveClick(source))
        {
            return;
        }

        var gridRow = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (gridRow?.Item is not SizeEntry entry || !entry.CanDelete)
        {
            return;
        }

        if (!gridRow.IsSelected || !SizeGrid.SelectedItems.Contains(entry))
        {
            return;
        }

        SizeGrid.SelectedItems.Remove(entry);
        entry.IsSelectedForCleanup = false;
        e.Handled = true;
    }

    private void ToggleCleanupSelection(DataGridRow? gridRow)
    {
        if (gridRow?.Item is not SizeEntry entry || !entry.CanDelete)
        {
            return;
        }

        if (SizeGrid.SelectedItems.Contains(entry))
        {
            SizeGrid.SelectedItems.Remove(entry);
        }
        else
        {
            SizeGrid.SelectedItems.Add(entry);
        }
    }

    private void CleanupCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.DataContext is SizeEntry entry && entry.CanDelete)
        {
            if (entry.IsSelectedForCleanup)
            {
                if (!SizeGrid.SelectedItems.Contains(entry))
                {
                    SizeGrid.SelectedItems.Add(entry);
                }
            }
            else if (SizeGrid.SelectedItems.Contains(entry))
            {
                SizeGrid.SelectedItems.Remove(entry);
            }
        }
    }

    private async void DeleteBinObjToolbarButton_Click(object sender, RoutedEventArgs e) =>
        await RunBinObjCleanupAsync(CollectFolderNamesFromList());

    private async void DeleteSelectedToolbarButton_Click(object sender, RoutedEventArgs e) =>
        await DeleteSelectedEntriesAsync();

    private static bool IsGridInteractiveClick(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button or CheckBox or TextBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
            {
                return match;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private string ToCleanupRelativePath(string itemName)
    {
        var full = Path.GetFullPath(Path.Combine(_currentPath, itemName));
        var relative = Path.GetRelativePath(_appRoot, full);
        return relative.Replace('/', '\\');
    }

    private List<string> CollectFolderNamesFromList()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows.Where(r => r.IsFolder && (r.IsSelectedForCleanup || SizeGrid.SelectedItems.Contains(r))))
        {
            var full = Path.GetFullPath(Path.Combine(_currentPath, row.Name));
            if (!IsUnderAppRoot(full))
            {
                continue;
            }

            set.Add(ToCleanupRelativePath(row.Name));
        }

        return set.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private List<SizeEntry> CollectSelectedEntries()
    {
        var set = new HashSet<SizeEntry>();
        foreach (SizeEntry row in SizeGrid.SelectedItems)
        {
            if (row.CanDelete)
            {
                set.Add(row);
            }
        }

        foreach (var row in _rows.Where(r => r.CanDelete && r.IsSelectedForCleanup))
        {
            set.Add(row);
        }

        return set.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task DeleteSelectedEntriesAsync()
    {
        var entries = CollectSelectedEntries();
        if (entries.Count == 0)
        {
            AppendLogKey("LogSelectItemsToDelete");
            return;
        }

        var paths = new List<string>();
        foreach (var entry in entries)
        {
            if (!IsUnderAppRoot(entry.FullPath))
            {
                AppendLogKey("LogSkippedOutsideRoot", entry.Name);
                continue;
            }

            if (_cloud.IsExternalPath(entry.FullPath))
            {
                AppendLogKey("LogSkippedCloud", entry.Name);
                continue;
            }

            paths.Add(entry.FullPath);
        }

        if (paths.Count == 0)
        {
            AppendLogKey("LogNothingDeletableSelected");
            return;
        }

        var confirmDialog = new ConfirmListDialog(
            "Seçilenleri silme onayı",
            $"{paths.Count} öğe kalıcı olarak silinecek. Devam edilsin mi?",
            paths)
        {
            Owner = this
        };
        if (confirmDialog.ShowDialog() != true)
        {
            AppendLogKey("LogBulkDeleteCancelled");
            return;
        }

        SetHeavyOperationBusy(true);
        var removed = 0;
        var failed = 0;
        try
        {
            foreach (var path in paths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    else
                    {
                        failed++;
                        AppendLogKey("LogNotFound", path);
                        continue;
                    }

                    removed++;
                    AppendLogKey("LogDeleted", path);
                    InvalidateCacheFromPath(path);
                }
                catch (Exception ex)
                {
                    failed++;
                    AppendLogKey("LogDeleteFailed", path, ex.Message);
                }
            }

            AppendLogKey("LogBulkDeleteSummary", removed, failed);
            if (removed > 0)
            {
                InvalidateCacheFromPath(_currentPath);
                await ShowFolderAsync(_currentPath, forceRescan: true, "HeadingAfterDelete");
            }
        }
        finally
        {
            SetHeavyOperationBusy(false);
        }
    }

    private void CopyLocationButton_Click(object sender, RoutedEventArgs e) =>
        CopyPathToClipboard(_currentPath, "LogLocationCopied");

    private void CopyItemPathButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not SizeEntry entry)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.FullPath))
        {
            return;
        }

        CopyPathToClipboard(entry.FullPath, "LogPathCopied", entry.Name);
    }

    private void CopyPathToClipboard(string path, string statusMessageKey, params object?[] statusArgs)
    {
        try
        {
            System.Windows.Clipboard.SetText(path);
            StatusText.Text = LocalizationService.Tf(statusMessageKey, statusArgs);
        }
        catch (Exception ex)
        {
            AppendLogKey("LogPathCopyFailed", ex.Message);
        }
    }

    private void RefreshCurrentFolderTotalBytesFromGrid()
    {
        _currentFolderTotalBytes = _rows.Sum(r => r.Bytes);
        UpdateLocationDisplay();
        UpdateListStatusFromVisibleRows();
    }

    private long GetDisplayedTotalBytes() =>
        _currentFolderTotalBytes ?? _rows.Sum(r => r.Bytes);

    private void SetListStatusText(int itemCount, string suffixKey, long? totalBytesOverride = null)
    {
        _statusItemCount = itemCount;
        _statusSuffixKey = suffixKey;
        _statusTotalOverride = totalBytesOverride;
        RefreshStatusBar();
    }

    private void RefreshStatusBar()
    {
        var total = _statusTotalOverride ?? GetDisplayedTotalBytes();
        StatusText.Text = string.Format(
            LocalizationService.T("StatusItems"),
            _statusItemCount,
            LocalizationService.T(_statusSuffixKey),
            LocalizationService.T("StatusTotal"),
            SizeFormatter.FormatBytes(total));
    }

    private void UpdateListStatusFromVisibleRows()
    {
        if (_rows.Count == 0 && !_currentFolderTotalBytes.HasValue)
        {
            return;
        }

        SetListStatusText(_rows.Count, "StatusListed");
    }

    private void UpdateLocationDisplay()
    {
        var showSize = !PathsEqual(_currentPath, _appRoot) && _currentFolderTotalBytes.HasValue;
        LocationPathBox.Text = showSize
            ? $"{_currentPath} ({SizeFormatter.FormatBytes(_currentFolderTotalBytes!.Value)})"
            : _currentPath;
        UpFolderButton.IsEnabled = Directory.GetParent(_currentPath) is not null;
        UpdateTypeFilterMenuChecks();
    }

    private void SetFolderRows(IEnumerable<SizeEntry> rows)
    {
        _folderRows.Clear();
        _folderRows.AddRange(rows);
        ApplyGridView();
    }

    private void ApplyGridView()
    {
        IEnumerable<SizeEntry> query = _folderRows;
        query = _gridTypeFilter switch
        {
            GridTypeFilter.FoldersOnly => query.Where(r => r.IsFolder),
            GridTypeFilter.FilesOnly => query.Where(r => r.IsFile),
            _ => query
        };

        query = _gridSortColumn switch
        {
            GridSortColumn.Name => _gridSortAscending
                ? query.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(r => r.Name, StringComparer.OrdinalIgnoreCase),
            GridSortColumn.Type => _gridSortAscending
                ? query.OrderBy(r => r.Type, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(r => r.Type, StringComparer.OrdinalIgnoreCase),
            _ => _gridSortAscending
                ? query.OrderBy(r => r.Bytes)
                : query.OrderByDescending(r => r.Bytes)
        };

        var visible = query.ToList();
        _rows.Clear();
        foreach (var row in visible)
        {
            _rows.Add(row);
        }

        UpdateSortGlyphs();
        UpdateTypeFilterGlyph();
        if (_folderRows.Count > 0)
        {
            SetListStatusText(visible.Count, "StatusListed", visible.Sum(r => r.Bytes));
        }
    }

    private void SortColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            ApplySortByTag(tag);
        }
    }

    private void SortColumnHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsGridInteractiveClick(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            ApplySortByTag(tag);
            e.Handled = true;
        }
    }

    private void ApplySortByTag(string tag)
    {
        var column = tag switch
        {
            "Name" => GridSortColumn.Name,
            "Type" => GridSortColumn.Type,
            "Size" => GridSortColumn.Size,
            _ => (GridSortColumn?)null
        };
        if (column is null)
        {
            return;
        }

        if (_gridSortColumn == column.Value)
        {
            _gridSortAscending = !_gridSortAscending;
        }
        else
        {
            _gridSortColumn = column.Value;
            _gridSortAscending = column.Value != GridSortColumn.Size;
        }

        ApplyGridView();
    }

    private void TypeFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void TypeFilterMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string tag)
        {
            return;
        }

        _gridTypeFilter = tag switch
        {
            "Folders" => GridTypeFilter.FoldersOnly,
            "Files" => GridTypeFilter.FilesOnly,
            _ => GridTypeFilter.All
        };

        UpdateTypeFilterMenuChecks();
        ApplyGridView();
    }

    private void UpdateTypeFilterMenuChecks()
    {
        TypeFilterAllMenuItem.IsChecked = _gridTypeFilter == GridTypeFilter.All;
        TypeFilterFolderMenuItem.IsChecked = _gridTypeFilter == GridTypeFilter.FoldersOnly;
        TypeFilterFileMenuItem.IsChecked = _gridTypeFilter == GridTypeFilter.FilesOnly;
    }

    private void UpdateTypeFilterGlyph()
    {
        TypeFilterGlyph.Foreground = _gridTypeFilter == GridTypeFilter.All
            ? (System.Windows.Media.Brush)FindResource("PrimaryBrush")
            : (System.Windows.Media.Brush)FindResource("DangerBrush");
    }

    private void UpdateSortGlyphs()
    {
        SetSortGlyph(SortGlyphName, GridSortColumn.Name);
        SetSortGlyph(SortGlyphType, GridSortColumn.Type);
        SetSortGlyph(SortGlyphSize, GridSortColumn.Size);
    }

    private void SetSortGlyph(TextBlock glyph, GridSortColumn column)
    {
        var active = _gridSortColumn == column;
        glyph.FontFamily = new FontFamily("Segoe MDL2 Assets");
        // Küçükten büyüğe: yukarı (E70E); büyükten küçüğe: aşağı (E70D). Yan ok glifleri kullanılmaz.
        if (active)
        {
            glyph.Text = _gridSortAscending ? "\uE70E" : "\uE70D";
            glyph.Foreground = (Brush)FindResource("PrimaryBrush");
        }
        else
        {
            glyph.FontFamily = new FontFamily("Segoe UI");
            glyph.Text = "\u2195";
            glyph.Foreground = (Brush)FindResource("ForegroundMutedBrush");
            glyph.Opacity = 0.65;
        }
    }

    private bool TryNavigateToFolder(string path)
    {
        var full = Path.GetFullPath(path);
        if (_cloud.IsExternalPath(full)) return false;
        return Directory.Exists(full);
    }

    private bool IsUnderAppRoot(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd('\\');
        var root = _appRoot.TrimEnd('\\');
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return true;
        return full.StartsWith(root + '\\', StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string a, string b) =>
        Path.GetFullPath(a).TrimEnd('\\').Equals(Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    private static string CacheKey(string path) => Path.GetFullPath(path).TrimEnd('\\');

    private void InvalidateCacheFromPath(string path)
    {
        var key = CacheKey(path);
        var prefix = key + '\\';
        foreach (var cachedPath in _folderCache.Keys.ToList())
        {
            if (cachedPath.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                cachedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _folderCache.Remove(cachedPath);
            }
        }

        var parent = Directory.GetParent(path)?.FullName;
        while (parent is not null && IsUnderAppRootStatic(parent, _appRoot))
        {
            _folderCache.Remove(CacheKey(parent));
            parent = Directory.GetParent(parent)?.FullName;
        }
    }

    private static bool IsUnderAppRootStatic(string path, string appRoot)
    {
        var full = Path.GetFullPath(path).TrimEnd('\\');
        var root = appRoot.TrimEnd('\\');
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase)) return true;
        return full.StartsWith(root + '\\', StringComparison.OrdinalIgnoreCase);
    }

    private static SizeEntry CloneEntry(SizeEntry source) => new()
    {
        Name = source.Name,
        Type = EntryTypes.Normalize(source.Type),
        FullPath = source.FullPath,
        Bytes = source.Bytes,
        SizeDisplay = source.SizeDisplay,
        IsRenaming = false,
        EditingName = source.Name
    };

    private bool TryApplyCache(string path)
    {
        if (!_folderCache.TryGetValue(CacheKey(path), out var cached))
        {
            return false;
        }

        var cloned = cached.Rows.Select(CloneEntry).ToList();
        _currentFolderTotalBytes = cached.TotalBytes;
        SetFolderRows(cloned);
        UpdateLocationDisplay();
        SetListStatusText(cloned.Count, "StatusFromCache");
        return true;
    }

    private void SaveCache(string path, ScanResult result)
    {
        var rows = result.Rows.Select(CloneEntry).ToList();
        _folderCache[CacheKey(path)] = new FolderScanCache
        {
            Rows = rows,
            TotalBytes = rows.Sum(r => r.Bytes),
            SkippedExternal = result.SkippedExternal,
            AccessErrors = result.AccessErrors
        };
    }

    private async Task ShowFolderAsync(string path, bool forceRescan, string headingKey, params object?[] headingArgs)
    {
        var previousPath = _currentPath;
        _currentPath = Path.GetFullPath(path);
        if (!string.IsNullOrEmpty(previousPath) && !PathsEqual(previousPath, _currentPath))
        {
            _gridTypeFilter = GridTypeFilter.All;
        }

        UpdateLocationDisplay();

        if (!forceRescan && TryApplyCache(_currentPath))
        {
            AppendLogKey("LogLocationFromCache", _currentPath);
            return;
        }

        await RunScanAsync(headingKey, headingArgs);
    }

    private void SetHeavyOperationBusy(bool busy)
    {
        RescanButton.IsEnabled = !busy;
        DeleteBinObjToolbarButton.IsEnabled = !busy;
        DeleteSelectedToolbarButton.IsEnabled = !busy;
        SizeGrid.IsEnabled = !busy;
        if (busy)
        {
            UpFolderButton.IsEnabled = false;
        }
        else
        {
            UpdateLocationDisplay();
        }
    }

    private async Task RunBinObjCleanupAsync(IReadOnlyList<string> names)
    {
        if (_deleteCts is not null) return;

        if (names.Count == 0)
        {
            AppendLogKey("LogSelectFoldersForBinObj");
            return;
        }
        var targets = new List<string>();
        foreach (var name in names)
        {
            try
            {
                var resolved = _cleaner.ResolveTargetFolder(_appRoot, name);
                if (!targets.Contains(resolved, StringComparer.OrdinalIgnoreCase))
                {
                    targets.Add(resolved);
                }
            }
            catch (Exception ex)
            {
                AppendLogKey("LogSkippedResolve", name, ex.Message);
            }
        }

        if (targets.Count == 0)
        {
            AppendLogKey("LogNoValidFolderSelected");
            return;
        }

        AppendLogKey("LogTargetFoldersHeader");
        foreach (var t in targets)
        {
            AppendLogKey("LogTargetFolderLine", t);
        }

        _deleteCts = new CancellationTokenSource();
        SetHeavyOperationBusy(true);
        try
        {
            var toDelete = await Task.Run(() =>
                _cleaner.FindBinObjDirectories(targets, _deleteCts.Token), _deleteCts.Token);

            if (toDelete.Count == 0)
            {
                AppendLogKey("LogBinObjNotFound");
                return;
            }

            AppendLogKey("LogBinObjWillDelete", toDelete.Count);
            var selectedSummary = string.Join(", ", names);
            var confirmDialog = new ConfirmListDialog(
                "bin / obj silme onayı",
                $"Seçili klasörler ({names.Count}): {selectedSummary}\n\n" +
                $"Bu klasörlerde bulunan {toDelete.Count} adet bin/obj klasörü silinecek. Devam edilsin mi?",
                toDelete)
            {
                Owner = this
            };
            if (confirmDialog.ShowDialog() != true)
            {
                AppendLogKey("LogDeleteCancelled");
                return;
            }

            var result = await Task.Run(() =>
                _cleaner.DeleteDirectories(toDelete, _deleteCts.Token), _deleteCts.Token);
            foreach (var line in result.LogLines)
            {
                AppendLogKeyWithArgs(line.Key, line.Args);
            }

            AppendLogKey("LogBinObjDone", result.Removed, result.Failed);

            if (result.Removed > 0)
            {
                foreach (var target in targets)
                {
                    InvalidateCacheFromPath(target);
                }
                InvalidateCacheFromPath(_currentPath);
                await ShowFolderAsync(_currentPath, forceRescan: true, "HeadingAfterDeleteSizes");
            }
        }
        catch (OperationCanceledException)
        {
            AppendLogKey("LogOperationCancelled");
        }
        finally
        {
            _deleteCts?.Dispose();
            _deleteCts = null;
            SetHeavyOperationBusy(false);
        }
    }

    private async Task RunScanAsync(string headingKey, params object?[] headingArgs)
    {
        if (_scanCts is not null)
        {
            _scanCts.Cancel();
            _scanCts.Dispose();
        }

        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;
        SetHeavyOperationBusy(true);
        ScanProgress.Visibility = Visibility.Visible;
        ScanProgress.IsIndeterminate = false;
        ScanProgress.Value = 0;

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                if (p.Total > 0)
                {
                    ScanProgress.Value = 100.0 * p.Done / p.Total;
                }
                StatusText.Text = string.Format(
                    LocalizationService.T("ScanCalculating"),
                    p.Done,
                    p.Total,
                    p.CurrentName);
            });

            var scanPath = _currentPath;
            var result = await Task.Run(() => _analyzer.Scan(scanPath, progress, token), token);

            if (result.IsCloudBlocked)
            {
                _folderRows.Clear();
                _rows.Clear();
                AppendLogKey(result.Message ?? "LogCloudBlocked");
                StatusText.Text = LocalizationService.T("ScanFailed");
                return;
            }

            var rowList = result.Rows.ToList();
            SaveCache(scanPath, result);
            _currentFolderTotalBytes = rowList.Sum(r => r.Bytes);
            SetFolderRows(rowList);
            UpdateLocationDisplay();

            var totalBytes = _currentFolderTotalBytes.Value;
            AppendLogBlank();
            if (!string.IsNullOrWhiteSpace(headingKey))
            {
                AppendLogSection(headingKey, headingArgs);
            }

            AppendLogKey("LogLocation", _currentPath);
            AppendLogKey("LogScanSummary", rowList.Count, SizeFormatter.FormatBytes(totalBytes), totalBytes);
            if (result.SkippedExternal)
            {
                AppendLogKey("LogSkippedExternalNote");
            }

            if (result.AccessErrors > 0)
            {
                AppendLogKey("LogAccessErrorsNote");
            }

            SetListStatusText(rowList.Count, "StatusListed");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = LocalizationService.T("ScanCancelled");
        }
        catch (Exception ex)
        {
            AppendLogKey("LogScanError", ex.Message);
            StatusText.Text = LocalizationService.T("ErrorShort");
        }
        finally
        {
            ScanProgress.Visibility = Visibility.Collapsed;
            SetHeavyOperationBusy(false);
        }
    }

    private void AppendLogKey(string key, params object?[] args) =>
        AppendLogKeyWithArgs(key, args);

    private void AppendLogKeyWithArgs(string key, object?[]? args)
    {
        var copy = args is { Length: > 0 } ? args.ToArray() : [];
        var entry = new StoredLogEntry(key, copy, false);
        _logEntries.Add(entry);
        LogBox.AppendText(FormatLogEntry(entry) + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    private void AppendLogBlank()
    {
        _logEntries.Add(new StoredLogEntry(string.Empty, [], true));
        LogBox.AppendText(Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    private void AppendLogSection(string titleKey, params object?[] titleArgs)
    {
        var args = new object?[titleArgs.Length + 1];
        args[0] = titleKey;
        for (var i = 0; i < titleArgs.Length; i++)
        {
            args[i + 1] = titleArgs[i];
        }

        AppendLogKeyWithArgs("LogSection", args);
    }

    private void RefreshLogDisplay()
    {
        LogBox.Clear();
        foreach (var entry in _logEntries)
        {
            if (entry.Blank)
            {
                LogBox.AppendText(Environment.NewLine);
            }
            else
            {
                LogBox.AppendText(FormatLogEntry(entry) + Environment.NewLine);
            }
        }

        LogBox.ScrollToEnd();
    }

    private static string FormatLogEntry(StoredLogEntry entry)
    {
        if (entry.Key == "LogSection")
        {
            var titleKey = (string)entry.Args[0]!;
            var titleArgs = entry.Args.Skip(1).ToArray();
            var title = titleArgs.Length > 0
                ? LocalizationService.Tf(titleKey, titleArgs)
                : LocalizationService.T(titleKey);
            return LocalizationService.Tf("LogSection", title);
        }

        return LocalizationService.Tf(entry.Key, entry.Args);
    }
}
