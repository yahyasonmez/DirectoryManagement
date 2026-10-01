using System.ComponentModel;
using System.Runtime.CompilerServices;
using DirectoryManagement.Services;

namespace DirectoryManagement.Models;

public sealed class SizeEntry : INotifyPropertyChanged
{
    private bool _isSelectedForCleanup;
    private bool _isRenaming;
    private string _name = "";
    private string _fullPath = "";
    private string _editingName = "";

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public string Type { get; init; } = "";

    public string FullPath
    {
        get => _fullPath;
        set
        {
            if (_fullPath == value) return;
            _fullPath = value;
            OnPropertyChanged();
        }
    }

    public long Bytes { get; init; }
    public string SizeDisplay { get; init; } = "";

    public bool IsFolder => EntryTypes.Normalize(Type) == EntryTypes.Folder;

    public bool IsFile => EntryTypes.Normalize(Type) == EntryTypes.File;

    public bool CanDelete => EntryTypes.Normalize(Type) != EntryTypes.Link;

    public bool CanRename => CanDelete;

    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (_isRenaming == value) return;
            _isRenaming = value;
            OnPropertyChanged();
        }
    }

    public string EditingName
    {
        get => _editingName;
        set
        {
            if (_editingName == value) return;
            _editingName = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelectedForCleanup
    {
        get => _isSelectedForCleanup;
        set
        {
            if (!CanDelete || _isSelectedForCleanup == value) return;
            _isSelectedForCleanup = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
