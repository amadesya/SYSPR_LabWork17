using System.Globalization;
using System.IO;
using System.Windows.Data;

public class FileDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string path = value as string;
        if (File.Exists(path))
        {
            return File.GetLastWriteTime(path).ToString("g");
        }
        else
            return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
