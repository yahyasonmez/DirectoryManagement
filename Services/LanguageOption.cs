using System.Windows.Media;

namespace DirectoryManagement.Services;

public sealed class LanguageOption
{
    public LanguageOption(AppLanguage language, string code)
    {
        Language = language;
        Code = code;
        Flag = LanguageFlagImages.Get(language);
    }

    public AppLanguage Language { get; }
    public string Code { get; }
    public ImageSource Flag { get; }
}
