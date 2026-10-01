using System.IO;
using Microsoft.Win32;

namespace DirectoryManagement.Services;

public sealed class CloudStorageRules
{
    private readonly List<string> _externalRoots = [];

    public bool SkippedExternal { get; private set; }

    public void Initialize()
    {
        _externalRoots.Clear();
        SkippedExternal = false;

        foreach (var name in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            AddExternalRoot(Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process));
            AddExternalRoot(Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User));
        }

        try
        {
            using var accounts = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts");
            if (accounts is not null)
            {
                foreach (var subName in accounts.GetSubKeyNames())
                {
                    using var account = accounts.OpenSubKey(subName);
                    var folder = account?.GetValue("UserFolder") as string;
                    AddExternalRoot(folder);
                }
            }
        }
        catch
        {
            // ignore registry errors
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AddExternalRoot(Path.Combine(profile, "Google Drive"));
        AddExternalRoot(Path.Combine(profile, "GoogleDrive"));

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                var label = drive.VolumeLabel;
                if (label.Equals("Google Drive", StringComparison.OrdinalIgnoreCase) ||
                    label.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                    label.Equals("SharePoint", StringComparison.OrdinalIgnoreCase))
                {
                    AddExternalRoot(drive.Name);
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    public void ResetSkippedFlag() => SkippedExternal = false;

    private void AddExternalRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var full = path.Trim().TrimEnd('\\');
        if (full.Length == 2 && full[1] == ':') full += '\\';
        if (_externalRoots.Any(r => r.Equals(full, StringComparison.OrdinalIgnoreCase))) return;
        _externalRoots.Add(full);
    }

    public static bool IsCloudDirectoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("OneDrive - ", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("SharePoint", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("SharePoint - ", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Google Drive", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Google Drive - ", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("GoogleDrive", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsCloudAttribute(FileAttributes attributes)
    {
        const FileAttributes recallOnDataAccess = (FileAttributes)0x400000;
        const FileAttributes recallOnOpen = (FileAttributes)0x40000;
        return (attributes & recallOnDataAccess) != 0 || (attributes & recallOnOpen) != 0;
    }

    public bool IsUnderExternalRoot(string path)
    {
        var full = path.TrimEnd('\\');
        foreach (var root in _externalRoots)
        {
            var rootFull = root.TrimEnd('\\');
            if (full.Equals(rootFull, StringComparison.OrdinalIgnoreCase)) return true;
            if (full.StartsWith(rootFull + '\\', StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public bool IsExternalPath(string path)
    {
        if (IsUnderExternalRoot(path)) return true;
        foreach (var segment in path.TrimEnd('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (IsCloudDirectoryName(segment)) return true;
        }
        return false;
    }

    public void MarkSkipped() => SkippedExternal = true;
}
