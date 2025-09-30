using System;
using System.Globalization;
using System.Windows.Data;
using SeamlessPuzzle.Utils;

namespace SeamlessPuzzle.Views
{
    public class LanguageConverter : IValueConverter
    {
        public static LanguageConverter Instance { get; } = new LanguageConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Language language && parameter is string key)
            {
                return LanguageManager.Instance.Resources[language][key];
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}