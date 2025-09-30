using System;
using System.Globalization;
using System.Windows.Data;
using SeamlessPuzzle.Utils;

namespace SeamlessPuzzle.Views
{
    public class PuzzleModeToLanguageConverter : IMultiValueConverter
    {
        public static PuzzleModeToLanguageConverter Instance { get; } = new PuzzleModeToLanguageConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length > 0 && values[0] is SeamlessPuzzle.Services.PuzzleMode mode)
            {
                // 根据当前语言返回对应的文本
                switch (LanguageManager.Instance.CurrentLanguage)
                {
                    case SeamlessPuzzle.Utils.Language.Chinese:
                        return mode switch
                        {
                            SeamlessPuzzle.Services.PuzzleMode.Horizontal => "水平拼接",
                            SeamlessPuzzle.Services.PuzzleMode.Vertical => "垂直拼接",
                            SeamlessPuzzle.Services.PuzzleMode.Grid4 => "4宫格拼接",
                            SeamlessPuzzle.Services.PuzzleMode.Grid9 => "9宫格拼接",
                            _ => mode.ToString()
                        };
                    case SeamlessPuzzle.Utils.Language.English:
                        return mode switch
                        {
                            SeamlessPuzzle.Services.PuzzleMode.Horizontal => "Horizontal",
                            SeamlessPuzzle.Services.PuzzleMode.Vertical => "Vertical",
                            SeamlessPuzzle.Services.PuzzleMode.Grid4 => "4-Grid",
                            SeamlessPuzzle.Services.PuzzleMode.Grid9 => "9-Grid",
                            _ => mode.ToString()
                        };
                    default:
                        return mode.ToString();
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