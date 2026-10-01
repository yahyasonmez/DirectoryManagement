using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DirectoryManagement.Services;

public static class Localize
{
    public static readonly DependencyProperty ToolTipKeyProperty =
        DependencyProperty.RegisterAttached(
            "ToolTipKey",
            typeof(string),
            typeof(Localize),
            new PropertyMetadata(null, OnKeyChanged));

    public static readonly DependencyProperty TextKeyProperty =
        DependencyProperty.RegisterAttached(
            "TextKey",
            typeof(string),
            typeof(Localize),
            new PropertyMetadata(null, OnKeyChanged));

    public static void SetToolTipKey(DependencyObject element, string value) =>
        element.SetValue(ToolTipKeyProperty, value);

    public static string? GetToolTipKey(DependencyObject element) =>
        (string?)element.GetValue(ToolTipKeyProperty);

    public static void SetTextKey(DependencyObject element, string value) =>
        element.SetValue(TextKeyProperty, value);

    public static string? GetTextKey(DependencyObject element) =>
        (string?)element.GetValue(TextKeyProperty);

    public static void RefreshTree(DependencyObject root)
    {
        foreach (var node in EnumerateSelfAndDescendants(root))
        {
            ApplyToElement(node);
        }
    }

    private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ApplyToElement(d);
    }

    private static void ApplyToElement(DependencyObject d)
    {
        var textKey = GetTextKey(d);
        if (!string.IsNullOrEmpty(textKey))
        {
            var text = LocalizationService.T(textKey);
            if (d is TextBlock tb)
            {
                tb.Text = text;
            }
            else if (d is ContentControl cc && cc.Content is string)
            {
                cc.Content = text;
            }
        }

        var tipKey = GetToolTipKey(d);
        if (!string.IsNullOrEmpty(tipKey) && d is FrameworkElement fe)
        {
            fe.ToolTip = LocalizationService.T(tipKey);
        }
    }

    private static IEnumerable<DependencyObject> EnumerateSelfAndDescendants(DependencyObject root)
    {
        yield return root;
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            foreach (var nested in EnumerateSelfAndDescendants(child))
            {
                yield return nested;
            }
        }
    }
}
