using System.IO;
using DirectoryManagement.Models;

namespace DirectoryManagement.Services;

public sealed class DirectoryAnalyzer
{
    private static readonly EnumerationOptions ListOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Device
    };

    private static readonly EnumerationOptions FolderContentOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Device
    };

    private readonly CloudStorageRules _cloud;
    private int _accessErrors;

    public DirectoryAnalyzer(CloudStorageRules cloud)
    {
        _cloud = cloud;
    }

    public int AccessErrors => _accessErrors;

    public ScanResult Scan(string root, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        _accessErrors = 0;
        _cloud.ResetSkippedFlag();

        if (_cloud.IsExternalPath(root))
        {
            return ScanResult.CloudBlocked(root);
        }

        var pending = new List<FileSystemInfo>();
        foreach (var item in new DirectoryInfo(root).EnumerateFileSystemInfos("*", ListOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldSkipTopLevel(item)) continue;
            pending.Add(item);
        }

        pending.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        var rows = new List<SizeEntry>();
        var done = 0;
        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            done++;
            progress?.Report(new ScanProgress(done, pending.Count, item.Name));

            long size;
            string type;
            if (item.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                type = EntryTypes.Link;
                size = 0;
            }
            else if (item is DirectoryInfo dir)
            {
                type = EntryTypes.Folder;
                size = GetDirectorySize(dir.FullName, cancellationToken);
            }
            else
            {
                type = EntryTypes.File;
                size = item is FileInfo file ? file.Length : 0;
            }

            rows.Add(new SizeEntry
            {
                Name = item.Name,
                Type = type,
                FullPath = item.FullName,
                Bytes = size,
                SizeDisplay = SizeFormatter.FormatBytes(size)
            });
        }

        rows.Sort((a, b) =>
        {
            var bySize = b.Bytes.CompareTo(a.Bytes);
            return bySize != 0 ? bySize : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return ScanResult.Success(root, rows, _accessErrors, _cloud.SkippedExternal);
    }

    private bool ShouldSkipTopLevel(FileSystemInfo item)
    {
        if (CloudStorageRules.IsCloudAttribute(item.Attributes)) { _cloud.MarkSkipped(); return true; }
        if (item is DirectoryInfo && (CloudStorageRules.IsCloudDirectoryName(item.Name) || _cloud.IsUnderExternalRoot(item.FullName)))
        {
            _cloud.MarkSkipped();
            return true;
        }
        return false;
    }

    private long GetDirectorySize(string path, CancellationToken cancellationToken)
    {
        long sum = 0;
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = stack.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(current, "*", FolderContentOptions))
                {
                    try
                    {
                        var attributes = File.GetAttributes(file);
                        if (CloudStorageRules.IsCloudAttribute(attributes)) { _cloud.MarkSkipped(); continue; }
                        if (attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                        sum += new FileInfo(file).Length;
                    }
                    catch
                    {
                        _accessErrors++;
                    }
                }
            }
            catch
            {
                _accessErrors++;
            }

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(current, "*", FolderContentOptions))
                {
                    try
                    {
                        var attributes = File.GetAttributes(dir);
                        if (attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                        if (CloudStorageRules.IsCloudAttribute(attributes) ||
                            CloudStorageRules.IsCloudDirectoryName(Path.GetFileName(dir)) ||
                            _cloud.IsUnderExternalRoot(dir))
                        {
                            _cloud.MarkSkipped();
                            continue;
                        }
                        stack.Push(dir);
                    }
                    catch
                    {
                        _accessErrors++;
                    }
                }
            }
            catch
            {
                _accessErrors++;
            }
        }

        return sum;
    }
}

public readonly record struct ScanProgress(int Done, int Total, string CurrentName);

public sealed class ScanResult
{
    public string Root { get; }
    public IReadOnlyList<SizeEntry> Rows { get; }
    public int AccessErrors { get; }
    public bool SkippedExternal { get; }
    public bool IsCloudBlocked { get; }
    public string? Message { get; }

    private ScanResult(string root, IReadOnlyList<SizeEntry> rows, int accessErrors, bool skippedExternal, bool cloudBlocked, string? message)
    {
        Root = root;
        Rows = rows;
        AccessErrors = accessErrors;
        SkippedExternal = skippedExternal;
        IsCloudBlocked = cloudBlocked;
        Message = message;
    }

    public static ScanResult CloudBlocked(string root) =>
        new(root, Array.Empty<SizeEntry>(), 0, false, true, "LogCloudBlocked");

    public static ScanResult Success(string root, IReadOnlyList<SizeEntry> rows, int accessErrors, bool skippedExternal) =>
        new(root, rows, accessErrors, skippedExternal, false, null);
}
