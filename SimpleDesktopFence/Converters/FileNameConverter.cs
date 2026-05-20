using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace SimpleDesktopFence.Converters;

/// <summary>
/// Converts a file name to its display form.
/// Binding values: [0] Name (string), [1] IsDirectory (bool), [2] ShowExtension (bool)
/// Directories always show their full name; files obey ShowExtension.
/// </summary>
public class FileNameConverter : IMultiValueConverter
{
    public static readonly FileNameConverter Instance = new();

    public object Convert(object[] values, Type targetType,
                          object parameter, CultureInfo culture)
    {
        if (values.Length < 3
            || values[0] is not string name
            || values[1] is not bool isDirectory
            || values[2] is not bool showExtension)
            return string.Empty;

        // Folders never have extensions stripped
        if (isDirectory || showExtension) return name;

        return Path.GetFileNameWithoutExtension(name);
    }

    public object[] ConvertBack(object value, Type[] targetTypes,
                                object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
