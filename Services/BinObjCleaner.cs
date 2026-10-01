using System.IO;

namespace DirectoryManagement.Services;

public sealed class BinObjCleaner
{
    private readonly CloudStorageRules _cloud;
    private int _accessErrors;

    public BinObjCleaner(CloudStorageRules cloud) => _cloud = cloud;

    public int AccessErrors => _accessErrors;

    public string ResolveTargetFolder(string root, string relativeName)
    {
        var name = relativeName.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Boş klasör adı.");
        if (name.IndexOfAny(['*', '?', '<', '>', '|', '"']) >= 0) throw new InvalidOperationException("Geçersiz karakter.");
        if (name.Length >= 2 && name[1] == ':' && char.IsLetter(name[0])) throw new InvalidOperationException("Tam yol kullanılamaz.");

        var parts = name.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p != ".").ToArray();
        foreach (var part in parts)
        {
            if (part == "..") throw new InvalidOperationException("Üst klasöre çıkış (..) kullanılamaz.");
        }
        if (parts.Length == 0) throw new InvalidOperationException("Geçersiz klasör adı.");

        var full = parts.Aggregate(root, Path.Combine);
        full = Path.GetFullPath(full);
        var rootFull = Path.GetFullPath(root);
        var rootPrefix = rootFull.TrimEnd('\\') + '\\';
        if (!full.Equals(rootFull, StringComparison.OrdinalIgnoreCase) &&
            !full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Klasör uygulama konumunun dışında.");
        }
        if (_cloud.IsExternalPath(full)) throw new InvalidOperationException("Harici bulut konumu seçilemez.");
        if (!Directory.Exists(full)) throw new InvalidOperationException("Klasör bulunamadı.");
        return full;
    }

    public IReadOnlyList<string> FindBinObjDirectories(IEnumerable<string> targetRoots, CancellationToken cancellationToken)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in targetRoots)
        {
            var stack = new Stack<string>();
            stack.Push(target);
            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = stack.Pop();
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(current))
                    {
                        try
                        {
                            var attributes = File.GetAttributes(dir);
                            if (attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                            if (CloudStorageRules.IsCloudAttribute(attributes)) continue;
                            if (_cloud.IsUnderExternalRoot(dir)) continue;
                            if (CloudStorageRules.IsCloudDirectoryName(Path.GetFileName(dir))) continue;

                            var leaf = Path.GetFileName(dir);
                            if (leaf.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                leaf.Equals("obj", StringComparison.OrdinalIgnoreCase))
                            {
                                found.Add(dir);
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
        }

        return found.OrderByDescending(p => p.Length).ToList();
    }

    public CleanupResult DeleteDirectories(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        var removed = 0;
        var failed = 0;
        var log = new List<LogLine>();
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.Delete(path, recursive: true);
                removed++;
                log.Add(new LogLine("LogDeleted", path));
            }
            catch (Exception ex)
            {
                failed++;
                _accessErrors++;
                log.Add(new LogLine("LogDeleteFailed", path, ex.Message));
            }
        }

        return new CleanupResult(removed, failed, log);
    }
}

public readonly record struct CleanupResult(int Removed, int Failed, IReadOnlyList<LogLine> LogLines);
