using System.IO;
using System.Windows;

namespace DirectoryManagement.Services;

public enum AppTheme
{
    Light,
    Dark
}

public static class ThemeService
{
    private const string ThemeFileName = "theme.txt";

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static void Initialize()
    {
        Apply(LoadSavedTheme());
    }

    public static AppTheme LoadSavedTheme()
    {
        try
        {
            var path = GetThemeFilePath();
            if (!File.Exists(path))
            {
                return AppTheme.Light;
            }

            var text = File.ReadAllText(path).Trim();
            return text.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? AppTheme.Dark
                : AppTheme.Light;
        }
        catch
        {
            return AppTheme.Light;
        }
    }

    public static void Apply(AppTheme theme)
    {
        Current = theme;
        var app = Application.Current;
        var resources = app.Resources;
        var merged = resources.MergedDictionaries;

        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var source = merged[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("ColorsLight", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("ColorsDark", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }

        var uri = theme == AppTheme.Dark
            ? new Uri("Themes/ColorsDark.xaml", UriKind.Relative)
            : new Uri("Themes/ColorsLight.xaml", UriKind.Relative);

        merged.Insert(0, new ResourceDictionary { Source = uri });

        try
        {
            File.WriteAllText(GetThemeFilePath(), theme == AppTheme.Dark ? "Dark" : "Light");
        }
        catch
        {
            // ignore persistence errors
        }
    }

    public static AppTheme Toggle()
    {
        var next = Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        Apply(next);
        return next;
    }

    public static (string ActionLabel, string SwitchTooltip, string IconGlyph) GetTogglePresentation(AppTheme current) =>
        current == AppTheme.Light
            ? (LocalizationService.T("ThemeNameDark"), LocalizationService.T("ThemeDark"), "\uE708")
            : (LocalizationService.T("ThemeNameLight"), LocalizationService.T("ThemeLight"), "\uE706");

    private static string GetThemeFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DirectoryManagement");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, ThemeFileName);
    }
}
