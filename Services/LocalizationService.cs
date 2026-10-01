using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DirectoryManagement.Services;

public enum AppLanguage
{
    Tr,
    Ar,
    En,
    Es,
    Ru,
    Fr
}

public static class LocalizationService
{
    private const string LanguageFileName = "language.txt";

    public static event EventHandler? LanguageChanged;

    public static AppLanguage Current { get; private set; } = AppLanguage.Tr;

    public static void Initialize()
    {
        Apply(LoadSavedLanguage(), notify: false);
    }

    public static AppLanguage LoadSavedLanguage()
    {
        try
        {
            var path = GetLanguageFilePath();
            if (!File.Exists(path))
            {
                return AppLanguage.Tr;
            }

            var text = File.ReadAllText(path).Trim();
            return Enum.TryParse<AppLanguage>(text, ignoreCase: true, out var lang)
                ? lang
                : AppLanguage.Tr;
        }
        catch
        {
            return AppLanguage.Tr;
        }
    }

    public static void Apply(AppLanguage language, bool notify = true)
    {
        Current = language;
        try
        {
            File.WriteAllText(GetLanguageFilePath(), language.ToString());
        }
        catch
        {
            // ignore
        }

        if (notify)
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    private static bool _comboSyncInProgress;

    public static string T(string key) => UiTranslations.Get(Current, key);

    public static string Tf(string key, params object?[] args) =>
        string.Format(T(key), args);

    public static string TypeName(string typeCode) => typeCode switch
    {
        EntryTypes.Folder => T("TypeFolder"),
        EntryTypes.File => T("TypeFile"),
        EntryTypes.Link => T("TypeLink"),
        _ => typeCode
    };

    public static FlowDirection FlowDirection =>
        Current == AppLanguage.Ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public static (string Code, AppLanguage Language)[] Languages =>
    [
        ("TR", AppLanguage.Tr),
        ("AR", AppLanguage.Ar),
        ("EN", AppLanguage.En),
        ("ES", AppLanguage.Es),
        ("RU", AppLanguage.Ru),
        ("FR", AppLanguage.Fr)
    ];

    public static void AttachLanguageComboBox(ComboBox comboBox)
    {
        if (comboBox.GetValue(ComboAttachedProperty) is true)
        {
            SyncLanguageComboBoxSelection(comboBox);
            return;
        }

        comboBox.SetValue(ComboAttachedProperty, true);
        comboBox.ItemsSource = CreateLanguageOptions();
        comboBox.ItemTemplate = (DataTemplate)Application.Current.FindResource("LanguageComboItemTemplate");
        comboBox.ToolTip = T("LangTip");
        SyncLanguageComboBoxSelection(comboBox);

        comboBox.SelectionChanged += (_, _) =>
        {
            if (_comboSyncInProgress)
            {
                return;
            }

            if (comboBox.SelectedItem is LanguageOption option && option.Language != Current)
            {
                Apply(option.Language);
            }
        };
    }

    public static void SyncLanguageComboBoxSelection(ComboBox comboBox)
    {
        if (comboBox.Items.Count == 0)
        {
            comboBox.ItemsSource = CreateLanguageOptions();
        }

        _comboSyncInProgress = true;
        try
        {
            foreach (var item in comboBox.Items)
            {
                if (item is LanguageOption option && option.Language == Current)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }

            comboBox.ToolTip = T("LangTip");
        }
        finally
        {
            _comboSyncInProgress = false;
        }
    }

    private static List<LanguageOption> CreateLanguageOptions() =>
        Languages.Select(l => new LanguageOption(l.Language, l.Code)).ToList();

    private static readonly DependencyProperty ComboAttachedProperty =
        DependencyProperty.RegisterAttached(
            "ComboAttached",
            typeof(bool),
            typeof(LocalizationService),
            new PropertyMetadata(false));

    private static string GetLanguageFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DirectoryManagement");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, LanguageFileName);
    }
}
