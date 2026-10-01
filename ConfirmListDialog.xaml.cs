using System.Windows;
using DirectoryManagement.Services;

namespace DirectoryManagement;

public partial class ConfirmListDialog : Window
{
    public ConfirmListDialog(string title, string message, IEnumerable<string> items)
    {
        InitializeComponent();
        AppIcon.ApplyTo(this);
        Title = title;
        MessageText.Text = message;
        ItemList.ItemsSource = items.ToList();
        MaxHeight = SystemParameters.WorkArea.Height / 2;
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalization();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyLocalization();

    private void ApplyLocalization()
    {
        FlowDirection = LocalizationService.FlowDirection;
        Localize.RefreshTree(this);
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
