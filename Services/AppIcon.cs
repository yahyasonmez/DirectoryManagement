using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DirectoryManagement.Services;

public static class AppIcon
{
    private static readonly Lazy<ImageSource?> WindowIcon = new(LoadWindowIcon);

    public static void ApplyTo(Window window)
    {
        if (WindowIcon.Value is not null)
        {
            window.Icon = WindowIcon.Value;
        }
    }

    private static ImageSource? LoadWindowIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Themes/directory-management-logo.ico", UriKind.Absolute);
            return BitmapFrame.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }
        catch
        {
            return null;
        }
    }
}
