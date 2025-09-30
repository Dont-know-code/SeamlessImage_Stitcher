using System;
using System.Windows;

namespace SeamlessPuzzle
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 默认使用浅色主题
            var resourceDict = new ResourceDictionary();
            resourceDict.Source = new Uri("pack://application:,,,/SeamlessPuzzle;component/Resources/Themes/LightTheme.xaml");
            this.Resources.MergedDictionaries.Add(resourceDict);
            
            base.OnStartup(e);
        }
    }
}