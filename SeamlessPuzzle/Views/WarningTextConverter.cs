using System;
using System.Globalization;
using System.Windows.Data;
using SeamlessPuzzle.Utils;

namespace SeamlessPuzzle.Views
{
    public class WarningTextConverter : IMultiValueConverter
    {
        public static WarningTextConverter Instance { get; } = new WarningTextConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] 是 WarningText 属性的值
            // values[1] 是 LanguageManager.Instance.CurrentLanguage 的值
            if (values.Length > 0 && values[0] is string warningText)
            {
                // 如果警告文本不为空，则返回它
                if (!string.IsNullOrEmpty(warningText))
                {
                    // 根据当前语言返回对应的警告文本
                    return LanguageManager.Instance.GetString("ReorderWarning");
                }
            }
            return "";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}