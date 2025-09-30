using System;
using System.Windows;
using System.Windows.Controls;

namespace SeamlessPuzzle.Views
{
    /// <summary>
    /// MemoryInfoView.xaml 的交互逻辑
    /// </summary>
    public partial class MemoryInfoView : UserControl
    {
        public MemoryInfoView()
        {
            InitializeComponent();
        }

        private void UpdateMemoryInfo(object sender, RoutedEventArgs e)
        {
            // 获取当前内存使用情况
            var currentMemory = GC.GetTotalMemory(false);
            MemoryInfoLabel.Content = $"当前内存使用: {currentMemory / (1024 * 1024):F2} MB";
        }

        private void ClearImageCache(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ViewModels.MainViewModel;
            if (viewModel != null)
            {
                // 获取ImageProcessingService实例并清除缓存
                var imageService = viewModel.GetType().GetField("_imageService", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(viewModel) as Services.ImageProcessingService;
                
                if (imageService != null)
                {
                    imageService.ClearCache();
                }
                
                // 强制垃圾回收以释放内存
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);
            }
            
            // 更新内存信息显示
            UpdateMemoryInfo(sender, e);
        }
    }
}