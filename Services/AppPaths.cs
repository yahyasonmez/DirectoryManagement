using System.IO;

namespace DirectoryManagement.Services;

public static class AppPaths
{
    /// <summary>Portable kök: exe'nin bulunduğu klasör (betikteki %~dp0 ile aynı mantık).</summary>
    public static string ApplicationRoot
    {
        get
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                return Path.GetFullPath(baseDir).TrimEnd('\\');
            }

            var path = Environment.ProcessPath;
            var dir = string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);
            return string.IsNullOrWhiteSpace(dir) ? Environment.CurrentDirectory : dir.TrimEnd('\\');
        }
    }
}
