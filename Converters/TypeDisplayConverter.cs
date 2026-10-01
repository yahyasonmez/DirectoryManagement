using System.Globalization;
using System.Windows.Data;
using DirectoryManagement.Services;

namespace DirectoryManagement.Converters;

public sealed class TypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string code ? LocalizationService.TypeName(EntryTypes.Normalize(code)) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
