namespace DirectoryManagement.Models;

public sealed class FolderScanCache
{
    public required IReadOnlyList<SizeEntry> Rows { get; init; }
    public long TotalBytes { get; init; }
    public bool SkippedExternal { get; init; }
    public int AccessErrors { get; init; }
}
