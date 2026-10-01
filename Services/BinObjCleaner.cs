using System.IO;

namespace DirectoryManagement.Services;

public sealed class BinObjCleaner
{
    private readonly CloudStorageRules _cloud;
    private int _accessErrors;

    public BinObjCleaner(CloudStorageRules cloud) => _cloud = cloud;

    public int AccessErrors => _accessErrors;

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
