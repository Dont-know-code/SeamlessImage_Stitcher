using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using SeamlessPuzzle.ViewModels;
using SeamlessPuzzle.Utils;

namespace SeamlessPuzzle.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point? _dragStartPoint;
        private ListBoxItem? _draggedItem;

        public MainWindow()
        {
            InitializeComponent();
            
            // 注册全局键盘钩子以捕获Ctrl+V粘贴操作
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;
            
            // 注册撤销和重做的快捷键
            this.KeyDown += MainWindow_KeyDown;
            
            // 优化渲染性能
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.LowQuality);
            
            // 添加窗口圆角效果
            this.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri("pack://application:,,,/PresentationFramework.Aero2;component/themes/Aero2.NormalColor.xaml") });
            
            // 订阅DataContextChanged事件以监听PreviewImage变化
            this.DataContextChanged += MainWindow_DataContextChanged;
        }
        
        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            
            if (e.NewValue is MainViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }
        
        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.PreviewImage))
            {
                // 当PreviewImage更新时，重置缩放和滚动位置
                ResetPreviewImageLayout();
            }
        }
        
        private void ResetPreviewImageLayout()
        {
            // 确保在UI线程上执行
            Dispatcher.BeginInvoke(new Action(() => {
                // 重置缩放
                PreviewImageGrid.LayoutTransform = new ScaleTransform(1.0, 1.0);
                
                // 重置滚动位置
                PreviewScrollViewer.ScrollToHorizontalOffset(0);
                PreviewScrollViewer.ScrollToVerticalOffset(0);
                
                // 触发一次布局更新
                PreviewScrollViewer.UpdateLayout();
                
                // 自动调整缩放到适合视图的大小
                AutoFitPreviewImage();
            }));
        }
        
        private void AutoFitPreviewImage()
        {
            // 确保预览图像和相关控件已初始化
            if (PreviewImage?.Source == null || PreviewScrollViewer == null || PreviewImageGrid == null)
                return;
                
            // 获取预览图像的实际渲染尺寸（使用ActualWidth和ActualHeight获取拼接后图像的实际尺寸）
            double imageWidth = PreviewImage.ActualWidth;
            double imageHeight = PreviewImage.ActualHeight;
            
            // 如果获取不到实际尺寸，尝试使用像素尺寸
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                var bitmapSource = PreviewImage.Source as BitmapSource;
                if (bitmapSource != null)
                {
                    imageWidth = bitmapSource.PixelWidth;
                    imageHeight = bitmapSource.PixelHeight;
                }
            }
            
            // 获取滚动视图的可视区域尺寸
            double viewWidth = PreviewScrollViewer.ViewportWidth;
            double viewHeight = PreviewScrollViewer.ViewportHeight;
            
            // 如果图像尺寸或视图尺寸无效，则返回
            if (imageWidth <= 0 || imageHeight <= 0 || viewWidth <= 0 || viewHeight <= 0)
                return;
                
            // 计算适合视图的缩放比例，确保整个图像都能显示在视图内
            // 添加一些边距，确保图像边缘与视图边缘有一定距离
            double margin = 20; // 20像素边距
            double scale = Math.Min((viewWidth - margin) / imageWidth, (viewHeight - margin) / imageHeight);
            
            // 应用缩放变换，不设置最大缩放限制，确保整张图片完整可见
            var scaleTransform = new ScaleTransform(scale, scale);
            PreviewImageGrid.LayoutTransform = scaleTransform;
            
            // 等待布局更新后再居中显示图像
            Dispatcher.BeginInvoke(new Action(() => {
                // 居中显示图像
                PreviewScrollViewer.ScrollToHorizontalOffset((imageWidth * scale - viewWidth) / 2);
                PreviewScrollViewer.ScrollToVerticalOffset((imageHeight * scale - viewHeight) / 2);
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 处理Ctrl+V粘贴图片
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
            {
                var viewModel = DataContext as MainViewModel;
                if (viewModel != null)
                {
                    // 从剪贴板获取图片
                    if (Clipboard.ContainsImage())
                    {
                        System.Windows.Media.Imaging.BitmapSource bitmap = Clipboard.GetImage();
                        viewModel.LoadPastedImage(bitmap);
                        e.Handled = true; // 标记事件已处理，防止继续传播
                    }
                    else
                    {
                        // 尝试从剪贴板获取文件路径
                        if (Clipboard.ContainsFileDropList())
                        {
                            var files = Clipboard.GetFileDropList();
                            var imageFiles = files.Cast<string>().Where(file =>
                            {
                                string extension = Path.GetExtension(file).ToLower();
                                return extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                                       extension == ".bmp" || extension == ".gif";
                            }).ToList();

                            if (imageFiles.Count > 0)
                            {
                                viewModel.LoadImageFiles(imageFiles);
                                e.Handled = true;
                            }
                        }
                    }
                }
            }
        }
        
        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            var viewModel = DataContext as MainViewModel;
            if (viewModel == null) return;
            
            // 处理Ctrl+Z撤销操作
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                if (viewModel.UndoCommand.CanExecute(null))
                {
                    viewModel.UndoCommand.Execute(null);
                    e.Handled = true;
                }
            }
            // 处理Ctrl+Shift+Z重做操作
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z)
            {
                if (viewModel.RedoCommand.CanExecute(null))
                {
                    viewModel.RedoCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        // 拖放预览处理
        private void ImageDropPreviewDragOver(object sender, DragEventArgs e)
        {
            // 检查拖放的数据格式是否包含文件
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        // 拖放完成处理
        private void ImageDrop(object sender, DragEventArgs e)
        {
            // 获取拖放的文件列表
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 转换为文件路径数组
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                
                // 过滤出图片文件
                var imageFiles = files.Where(file =>
                {
                    string extension = Path.GetExtension(file).ToLower();
                    return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || 
                           extension == ".bmp" || extension == ".gif";
                }).ToList();
                
                if (imageFiles.Count > 0)
                {
                    var viewModel = DataContext as MainViewModel;
                    if (viewModel != null)
                    {
                        viewModel.LoadImageFiles(imageFiles);
                    }
                }
            }
        }

        // 拖放过程中的键盘事件处理
        private void ImageDropPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 按下ESC键取消拖放操作
            if (e.Key == Key.Escape)
            {
                DragDrop.AddGiveFeedbackHandler(this, GiveFeedbackHandler);
                e.Handled = true;
            }
        }

        // 提供拖放反馈的处理程序
        private void GiveFeedbackHandler(object sender, GiveFeedbackEventArgs e)
        {
            if (e.Effects == DragDropEffects.None)
            {
                e.UseDefaultCursors = false;
                Mouse.SetCursor(Cursors.No);
                e.Handled = true;
            }
        }
        
        // 窗口关闭时清理资源
        protected override void OnClosed(System.EventArgs e)
        {
            var viewModel = DataContext as MainViewModel;
            viewModel?.Cleanup();
            base.OnClosed(e);
        }
        
        // 预览图像区域的鼠标滚轮处理
        private void PreviewImageScroll(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // 检查是否按住Ctrl键进行缩放操作
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    // 获取鼠标位置
                    Point mousePosition = e.GetPosition(PreviewImageGrid);
                    
                    // 计算缩放中心点（相对于图像）
                    double centerX = mousePosition.X / PreviewImageGrid.ActualWidth;
                    double centerY = mousePosition.Y / PreviewImageGrid.ActualHeight;
                    
                    // 获取当前缩放值
                    ScaleTransform scaleTransform = PreviewImageGrid.LayoutTransform as ScaleTransform;
                    double currentScale = scaleTransform?.ScaleX ?? 1.0;
                    
                    // 根据滚轮方向调整缩放值（每步缩放10%）
                    double newScale = e.Delta > 0 ? currentScale * 1.1 : currentScale / 1.1;
                    
                    // 限制缩放范围在0.1到5倍之间
                    newScale = Math.Max(0.1, Math.Min(newScale, 5.0));
                    
                    // 应用新的缩放变换
                    if (scaleTransform == null)
                    {
                        scaleTransform = new ScaleTransform(newScale, newScale);
                        PreviewImageGrid.LayoutTransform = scaleTransform;
                    }
                    else
                    {
                        scaleTransform.ScaleX = newScale;
                        scaleTransform.ScaleY = newScale;
                    }
                    
                    // 调整滚动位置以保持缩放中心点不变
                    double newCenterX = centerX * PreviewImageGrid.ActualWidth;
                    double newCenterY = centerY * PreviewImageGrid.ActualHeight;
                    
                    double offsetDeltaX = newCenterX - mousePosition.X;
                    double offsetDeltaY = newCenterY - mousePosition.Y;
                    
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - offsetDeltaX);
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - offsetDeltaY);
                    
                    e.Handled = true;
                }
                else
                {
                    // 默认滚动行为
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3);
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta / 3);
                    e.Handled = true;
                }
            }
        }
        
        private void ImageListItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }
        
        private void ImageListItemPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragStartPoint.HasValue)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint.Value - currentPosition;
                
                // 只有当鼠标移动超过一定距离时才开始拖拽
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    ListBox listBox = sender as ListBox;
                    ListBoxItem listBoxItem = FindVisualParent<ListBoxItem>((DependencyObject)e.OriginalSource);
                    
                    if (listBoxItem != null)
                    {
                        _draggedItem = listBoxItem;
                        DragDrop.DoDragDrop(listBoxItem, listBoxItem.DataContext, DragDropEffects.Move);
                        _dragStartPoint = null;
                    }
                }
            }
        }
        
        private void ImageListItemDrop(object sender, DragEventArgs e)
        {
            if (_draggedItem != null)
            {
                var viewModel = DataContext as MainViewModel;
                if (viewModel != null)
                {
                    var sourceItem = _draggedItem.DataContext as SeamlessPuzzle.Models.ImageModel;
                    var targetElement = e.OriginalSource as FrameworkElement;
                    if (targetElement != null)
                    {
                        var targetItem = targetElement.DataContext as SeamlessPuzzle.Models.ImageModel;
                        
                        if (sourceItem != null && targetItem != null && sourceItem != targetItem)
                        {
                            int sourceIndex = viewModel.Images.IndexOf(sourceItem);
                            int targetIndex = viewModel.Images.IndexOf(targetItem);
                            
                            // 移动图片顺序
                            viewModel.Images.RemoveAt(sourceIndex);
                            viewModel.Images.Insert(targetIndex, sourceItem);
                            
                            // 如果已经有预览图像，则显示警告信息
                            if (viewModel.PreviewImage != null)
                            {
                                viewModel.WarningText = "ReorderWarning"; // 设置键名而不是具体文本
                            }
                            
                            // 保存状态到撤销管理器
                            viewModel.SaveCurrentState();
                        }
                    }
                }
                _draggedItem = null;
            }
        }
        
        private void ImageListItemDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
        }
        
        // 查找可视化树中的父元素
        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindVisualParent<T>(parentObject);
        }
        
        // 应用主题
        private void ApplyTheme(string themeName)
        {
            try
            {
                var resourceDict = new ResourceDictionary();
                
                if (themeName == "Light")
                {
                    resourceDict.Source = new Uri("pack://application:,,,/SeamlessPuzzle;component/Resources/Themes/LightTheme.xaml");
                }
                else
                {
                    // 默认使用浅色主题
                    resourceDict.Source = new Uri("pack://application:,,,/SeamlessPuzzle;component/Resources/Themes/LightTheme.xaml");
                }
                
                // 清除现有的主题资源
                var existingDictionaries = Application.Current.Resources.MergedDictionaries
                    .Where(rd => rd.Source != null && 
                                (rd.Source.ToString().Contains("LightTheme.xaml") || 
                                 rd.Source.ToString().Contains("DarkTheme.xaml")))
                    .ToList();
                
                foreach (var dict in existingDictionaries)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(dict);
                }
                
                // 添加新主题
                Application.Current.Resources.MergedDictionaries.Add(resourceDict);
                
                // 强制刷新所有控件以应用新主题
                RefreshAllControls(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换主题失败: {ex.Message}", "无缝拼图", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 递归刷新所有控件以应用新主题
        private void RefreshAllControls(DependencyObject obj)
        {
            if (obj == null) return;
            
            // 强制更新控件的资源引用
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                
                // 如果是 FrameworkElement，强制更新其资源
                if (child is FrameworkElement fe)
                {
                    fe.InvalidateProperty(FrameworkElement.StyleProperty);
                    fe.InvalidateProperty(Control.BackgroundProperty);
                    fe.InvalidateProperty(Control.ForegroundProperty);
                    fe.InvalidateProperty(Border.BorderBrushProperty);
                }
                
                // 递归处理子控件
                RefreshAllControls(child);
            }
        }
        
        // 设置按钮点击事件处理
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建设置菜单
            var contextMenu = new ContextMenu();
            
            // 应用自定义样式
            contextMenu.Style = FindResource("SettingsContextMenuStyle") as Style;
            
            // 创建MenuItem样式
            var menuItemStyle = new Style(typeof(MenuItem));
            menuItemStyle.Setters.Add(new Setter(MenuItem.BackgroundProperty, FindResource("PanelBackground")));
            menuItemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, FindResource("TextColor")));
            
            // 应用MenuItem样式到ContextMenu
            contextMenu.ItemContainerStyle = menuItemStyle;
            
            // 添加语言选项
            var languageHeader = new MenuItem
            {
                Header = LanguageManager.Instance.GetString("Language"),
                FontWeight = FontWeights.Bold,
                IsEnabled = false
            };
            contextMenu.Items.Add(languageHeader);
            
            // 中文选项
            var chineseItem = new MenuItem
            {
                Header = LanguageManager.Instance.GetString("Chinese"),
                IsChecked = LanguageManager.Instance.CurrentLanguage == SeamlessPuzzle.Utils.Language.Chinese
            };
            chineseItem.Click += (s, args) => {
                LanguageManager.Instance.CurrentLanguage = SeamlessPuzzle.Utils.Language.Chinese;
            };
            contextMenu.Items.Add(chineseItem);
            
            // 英文选项
            var englishItem = new MenuItem
            {
                Header = LanguageManager.Instance.GetString("English"),
                IsChecked = LanguageManager.Instance.CurrentLanguage == SeamlessPuzzle.Utils.Language.English
            };
            englishItem.Click += (s, args) => {
                LanguageManager.Instance.CurrentLanguage = SeamlessPuzzle.Utils.Language.English;
            };
            contextMenu.Items.Add(englishItem);
            
            // 显示菜单
            contextMenu.IsOpen = true;
            contextMenu.PlacementTarget = sender as Button;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        }
    }
    
    // 布尔值到进度条不确定状态的转换器
    public class BooleanToIndeterminateConverter : System.Windows.Data.IValueConverter
    {
        public static BooleanToIndeterminateConverter Instance { get; } = new BooleanToIndeterminateConverter();
        
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool && (bool)value;
        }
        
        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
    

    
    // 索引转换器，用于显示列表项的序号
    public class IndexConverter : System.Windows.Data.IValueConverter
    {
        public static IndexConverter Instance { get; } = new IndexConverter();
        
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var item = (ListBoxItem)value;
            var listBox = ItemsControl.ItemsControlFromItemContainer(item) as ListBox;
            var index = listBox?.ItemContainerGenerator.IndexFromContainer(item) + 1;
            return index?.ToString() ?? "0";
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
    

}