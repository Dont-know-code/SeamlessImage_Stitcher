using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SeamlessPuzzle.Models;
using SeamlessPuzzle.Services;
using SeamlessPuzzle.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SeamlessPuzzle.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ImageProcessingService _imageService = new();
        private readonly UndoManager<List<ImageModel>> _undoManager = new();
        private PuzzleMode _currentMode = PuzzleMode.Horizontal;
        private bool _isProcessing = false;
        private float _progress = 0;
        private string _statusText = "就绪";
        private string _warningText = ""; // 确保初始化为空字符串
        private bool _isSaveWarningVisible = false; // 保存警告可见性
        private BitmapSource? _previewImage;
        // 用于预览生成的防抖
        private CancellationTokenSource? _previewCts;
        // 缓存全尺寸拼接图，用于快速保存
        private Image<Rgba32>? _cachedFullSizeImage;
        private PuzzleMode _cachedFullSizeMode;
        private List<ImageModel>? _cachedSelectedImages;
        
        // 添加并行处理的信号量，限制并发数量
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
        
        // 添加批量处理的取消令牌
        private CancellationTokenSource? _batchProcessingCts;

        // 当前拼图会话
        private PuzzleSession? _currentSession;
        
        // 是否已生成预览图的标志
        private bool _isPreviewGenerated = false;

        public ObservableCollection<ImageModel> Images { get; } = new();
        
        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            set
            {
                _previewImage = value;
                OnPropertyChanged(nameof(PreviewImage));
            }
        }
        
        public bool IsSaveWarningVisible
        {
            get => _isSaveWarningVisible;
            set
            {
                _isSaveWarningVisible = value;
                OnPropertyChanged(nameof(IsSaveWarningVisible));
            }
        }
        public ICommand HorizontalPuzzleCommand { get; }
        public ICommand VerticalPuzzleCommand { get; }
        public ICommand Grid4PuzzleCommand { get; }
        public ICommand Grid9PuzzleCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand RemoveAllCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        public PuzzleMode CurrentMode
        {
            get => _currentMode;
            set { _currentMode = value; OnPropertyChanged(nameof(CurrentMode)); }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set { _isProcessing = value; OnPropertyChanged(nameof(IsProcessing)); }
        }

        public float Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(nameof(Progress)); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public string WarningText
        {
            get => _warningText;
            set { _warningText = value; OnPropertyChanged(nameof(WarningText)); }
        }

        public MainViewModel()
        {
            HorizontalPuzzleCommand = new RelayCommand(() => CreatePuzzle(PuzzleMode.Horizontal));
            VerticalPuzzleCommand = new RelayCommand(() => CreatePuzzle(PuzzleMode.Vertical));
            Grid4PuzzleCommand = new RelayCommand(() => CreatePuzzle(PuzzleMode.Grid4));
            Grid9PuzzleCommand = new RelayCommand(() => CreatePuzzle(PuzzleMode.Grid9));
            SaveCommand = new RelayCommand(async () => await SavePuzzleAsync(), () => Images.Count > 0 && _isPreviewGenerated);
            RemoveAllCommand = new RelayCommand(RemoveAllImages);
            UndoCommand = new RelayCommand(Undo, () => _undoManager.CanUndo);
            RedoCommand = new RelayCommand(Redo, () => _undoManager.CanRedo);

            // 首次启动加载示例图
            LoadSampleImagesAsync();
            
            // 初始化当前会话
            _currentSession = new PuzzleSession();
        }

        private async void LoadSampleImagesAsync()
        {
            try
            {
                var sample1 = await _imageService.LoadSampleImageAsync(1);
                var sample2 = await _imageService.LoadSampleImageAsync(2);

                if (sample1 != null) 
                {
                    Images.Add(sample1);
                    _currentSession?.AddImage(sample1);
                }
                if (sample2 != null) 
                {
                    Images.Add(sample2);
                    _currentSession?.AddImage(sample2);
                }

                SaveCurrentState();
                
                // 更新保存命令的状态
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                // 忽略示例图片加载错误
            }
        }

        public async void LoadImageFiles(IReadOnlyList<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0) return;

            IsProcessing = true;
            StatusText = "正在加载图片...";
            Progress = 0;

            // 取消之前的批量处理任务
            _batchProcessingCts?.Cancel();
            _batchProcessingCts = new CancellationTokenSource();

            try
            {
                var progress = new Progress<float>(p =>
                {
                    Progress = p;
                    StatusText = $"正在加载图片... {(int)(p * 100)}%";
                });

                var newImages = await _imageService.LoadImageFilesAsync(filePaths, progress);
                AddImages(newImages);
            }
            catch (Exception ex)
            {
                ShowToast($"加载失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                StatusText = "就绪";
                Progress = 0;
            }
        }

        public async void LoadPastedImage(BitmapSource bitmap)
        {
            IsProcessing = true;
            StatusText = "正在处理粘贴的图片...";

            try
            {
                var imageModel = await _imageService.LoadPastedImageAsync(bitmap);
                if (imageModel != null)
                {
                    AddImages(new[] { imageModel });
                }
                else
                {
                    ShowToast("粘贴的图片格式不支持或处理失败");
                }
            }
            catch (Exception ex)
            {
                ShowToast($"粘贴失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                StatusText = "就绪";
            }
        }

        private void AddImages(IEnumerable<ImageModel> newImages)
        {
            foreach (var newImage in newImages)
            {
                Images.Add(newImage);
                _currentSession?.AddImage(newImage);
            }
            
            // 更新保存命令的状态
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();

            SaveCurrentState();
        }

        private async void CreatePuzzle(PuzzleMode mode)
        {
            CurrentMode = mode;
            WarningText = ""; // 清除警告文本
            _isPreviewGenerated = true; // 标记已生成预览图
            await GeneratePreviewWithDebounceAsync();
        }

        /// <summary>
        /// 获取当前语言的警告文本
        /// </summary>
        public string GetReorderWarningText()
        {
            return SeamlessPuzzle.Utils.LanguageManager.Instance.GetString("ReorderWarning");
        }

        /// <summary>
        /// 添加防抖的预览生成方法
        /// </summary>
        private async Task GeneratePreviewWithDebounceAsync()
        {
            // 取消之前的预览任务
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();

            try
            {
                // 延迟50ms再生成预览（进一步减少延迟）
                await Task.Delay(50, _previewCts.Token);
                await RenderPreviewAsync();
            }
            catch (OperationCanceledException)
            {
                // 忽略取消操作
            }
        }

        /// <summary>
        /// 预生成全尺寸拼接图，用于快速保存
        /// </summary>
        private async void PreGenerateFullSizeImage(List<ImageModel> selectedImages, PuzzleMode mode, CancellationToken cancellationToken = default)
        {
            try
            {
                // 检查是否已取消
                if (cancellationToken.IsCancellationRequested || _previewCts?.Token.IsCancellationRequested == true)
                    return;
                
                // 限制并发数量，避免占用过多资源
                await _semaphore.WaitAsync(cancellationToken);
                
                // 再次检查取消状态
                if (cancellationToken.IsCancellationRequested || _previewCts?.Token.IsCancellationRequested == true)
                    return;
                
                // 生成全尺寸拼接图并缓存
                var fullImage = await _imageService.CreatePuzzleAsync(selectedImages, mode);
                
                // 再次检查取消状态
                if (cancellationToken.IsCancellationRequested || _previewCts?.Token.IsCancellationRequested == true)
                {
                    fullImage?.Dispose();
                    return;
                }
                
                // 更新缓存
                _cachedFullSizeImage?.Dispose();
                _cachedFullSizeImage = fullImage;
                _cachedFullSizeMode = mode;
                _cachedSelectedImages = new List<ImageModel>(selectedImages);
                
                // 将合成图像添加到当前会话中
                if (_currentSession != null)
                {
                    _currentSession.CompositeImage?.Dispose();
                    _currentSession.CompositeImage = fullImage;
                }
            }
            catch (OperationCanceledException)
            {
                // 忽略取消操作
            }
            catch { /* 忽略预生成错误，不影响用户体验 */ }
            finally
            {
                _semaphore.Release();
            }
        }
        
        private async Task RenderPreviewAsync()
        {
            // 1. 清空当前预览
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                PreviewImage = null;
            });

            // 2. 若无图，直接返回
            if (Images.Count == 0)
            {
                _isPreviewGenerated = false; // 没有图片时重置预览生成标志
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged(); // 更新保存命令状态
                
                // 完全取消保存完成后的弹窗
                // ShowToast("请先添加图片");
                return;
            }

            IsProcessing = true;
            StatusText = "正在生成预览...";

            try
            {
                var selectedImages = Images.Where(img => img.IsSelected).ToList();
                if (selectedImages.Count == 0)
                {
                    selectedImages = Images.ToList();
                }

                // 3. 使用降采样的预览图生成方法
                using var result = await _imageService.CreatePreviewPuzzleAsync(selectedImages, CurrentMode);
                
                // 将预览图像添加到当前会话中
                if (_currentSession != null)
                {
                    _currentSession.PreviewImage?.Dispose();
                    _currentSession.PreviewImage = result.Clone();
                }
                
                var previewImage = await Task.Run(() => ConvertToBitmapSource(result));

                // 4. 主线程更新 UI
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PreviewImage = previewImage;
                    _isPreviewGenerated = true; // 成功生成预览图后设置标志为true
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged(); // 更新保存命令状态
                });

                // 5. 预生成全尺寸拼接图（在后台，不阻塞UI）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(100, _previewCts.Token); // 进一步减少延迟
                        PreGenerateFullSizeImage(selectedImages, CurrentMode, _previewCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // 忽略取消操作
                    }
                }, _previewCts.Token);
            }
            catch (Exception ex)
            {
                _isPreviewGenerated = false; // 生成失败时重置预览生成标志
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged(); // 更新保存命令状态
                
                // 完全取消保存完成后的弹窗
                // ShowToast($"生成预览失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                StatusText = "就绪";
                

            }
        }
        
        private BitmapSource ConvertToBitmapSource(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image)
        {
            using var memoryStream = new MemoryStream();
            // 使用较低的压缩级别来提高转换速度
            image.Save(memoryStream, new SixLabors.ImageSharp.Formats.Png.PngEncoder() { CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.Level1 });
            memoryStream.Seek(0, SeekOrigin.Begin);
            var bitmap = BitmapFrame.Create(memoryStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            bitmap.Freeze(); // 冻结以提高性能并帮助垃圾回收
            return bitmap;
        }

        private async Task SavePuzzleAsync()
        {
            if (Images.Count == 0)
            {
                return;
            }

            // 显示保存警告文本
            IsSaveWarningVisible = true;
            
            IsProcessing = true;
            StatusText = "正在拼接图像...";
            Progress = 0;

            // 创建取消令牌源，用于支持取消操作（尽管当前UI未提供此功能，但为将来扩展预留）
            var cancellationTokenSource = new CancellationTokenSource();

            Image<Rgba32>? puzzleImage = null;
            try
            {
                var selectedImages = Images.Where(img => img.IsSelected).ToList();
                if (selectedImages.Count == 0)
                {
                    selectedImages = Images.ToList();
                }

                // 创建进度报告器，用于拼图生成阶段 (0% - 70%)
                var generateProgress = new Progress<float>(p =>
                {
                    // 在UI线程更新进度
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Progress = p * 0.7f; // 生成阶段占总进度的70%
                        StatusText = $"正在拼接图像... {(int)(p * 70)}%";
                    });
                });

                // 根据文档要求，将CPU密集型操作放在Task.Run中执行
                StatusText = "正在拼接图像...";
                puzzleImage = await Task.Run(() =>
                {
                    return _imageService.CreatePuzzleAsync(selectedImages, CurrentMode, generateProgress, cancellationTokenSource.Token);
                }, cancellationTokenSource.Token);

                // 保存图片阶段 (70% - 100%)
                var saveProgress = new Progress<float>(p =>
                {
                    // 在UI线程更新进度
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Progress = 70 + p * 30f; // 保存阶段占总进度的30%，从70%开始
                        StatusText = $"正在保存到磁盘... {(int)(70 + p * 30)}%";
                    });
                });

                // 根据文档要求，在Task.Run中执行保存操作，避免阻塞UI线程
                var filePath = await Task.Run(() =>
                {
                    // 调用新的SavePuzzleAsync方法，使用默认参数（不强制物理写入）
                    return _imageService.SavePuzzleAsync(puzzleImage, CurrentMode, selectedImages.Count, saveProgress, cancellationTokenSource.Token);
                }, cancellationTokenSource.Token);

                // 保存成功后更新状态 - 根据文档要求，文件句柄关闭后立即报告100%并提示保存成功
                StatusText = "✅ 已保存（系统正在后台写入）";
                Progress = 100;
                
                // 隐藏保存警告文本
                IsSaveWarningVisible = false;
                
                // 根据文档要求，在UI线程显示友好的成功消息
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowToast($"图片已保存到: {filePath}");
                });
            }
            catch (Exception ex)
            {
                // 根据文档要求，在UI线程上显示用户友好的错误消息
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 根据异常类型提供更友好的错误消息
                    string userFriendlyMessage = ex switch
                    {
                        OutOfMemoryException => "内存不足，无法完成拼图保存。请尝试减少图片数量或使用更低分辨率的图片。",
                        IOException => "文件保存失败，可能是磁盘空间不足或文件被占用。",
                        OperationCanceledException => "操作被取消。",
                        _ => $"保存失败: {ex.Message}"
                    };
                    ShowToast(userFriendlyMessage);
                });
            }
            finally
            {
                // 确保释放拼图图像资源
                puzzleImage?.Dispose();
                
                IsProcessing = false;
                StatusText = "就绪";
                Progress = 0;
                
                // 释放取消令牌源
                cancellationTokenSource.Dispose();
                
                // 释放缓存的全尺寸图像
                _cachedFullSizeImage?.Dispose();
                _cachedFullSizeImage = null;
                _cachedSelectedImages = null;
            }
        }

        private void RemoveAllImages()
        {
            // 先保存当前状态
            SaveCurrentState();
            
            // 在后台线程执行资源释放和垃圾回收，避免阻塞UI
            Task.Run(() =>
            {
                try
                {
                    // 实施三重释放协议
                    // 1. 取消所有异步操作
                    _previewCts?.Cancel();
                    _batchProcessingCts?.Cancel();
                    
                    // 2. 显式释放所有图像资源
                    foreach (var image in Images)
                    {
                        try
                        {
                            // 先释放ImageModel中的图像资源
                            if (image.Image != null)
                            {
                                image.Image.Dispose();
                                image.Image = null;
                            }
                            
                            // 再释放整个ImageModel
                            image.Dispose();
                        }
                        catch
                        {
                            // 忽略单个图像释放中的异常
                        }
                    }
                    
                    // 清理图像处理服务的缓存
                    _imageService.ClearCache();
                    
                    // 清理缓存的图像资源
                    try
                    {
                        _cachedFullSizeImage?.Dispose();
                    }
                    catch
                    {
                        // 忽略异常
                    }
                    
                    // 在UI线程上更新UI相关资源
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 3. 切断所有强引用
                        Images.Clear();
                        
                        _cachedFullSizeImage = null;
                        _cachedSelectedImages = null;
                        
                        // 清空预览图像并释放资源
                        if (_previewImage != null)
                        {
                            // 如果_previewImage是可释放的资源，则释放它
                            if (_previewImage is System.Windows.Media.Imaging.BitmapImage bitmapImage)
                            {
                                bitmapImage.UriSource = null;
                            }
                            _previewImage = null;
                        }
                        
                        PreviewImage = null;
                        
                        // 清除警告文本
                        WarningText = "";
                        IsSaveWarningVisible = false;
                        
                        // 重置预览生成标志
                        _isPreviewGenerated = false;
                        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                        
                        // 释放并重置当前会话
                        // 先显式清理会话中的图像资源
                        if (_currentSession != null)
                        {
                            try
                            {
                                // 清理会话中的预览图和合成图
                                _currentSession.PreviewImage?.Dispose();
                                _currentSession.PreviewImage = null;
                                _currentSession.CompositeImage?.Dispose();
                                _currentSession.CompositeImage = null;
                                _currentSession.Preview = null;
                            }
                            catch
                            {
                                // 忽略清理过程中的异常
                            }
                            _currentSession.Dispose();
                        }
                        _currentSession = new PuzzleSession();
                        
                        // 重要：清空UndoManager，移除所有历史状态的引用
                        _undoManager.Clear();
                        OnPropertyChanged(nameof(UndoCommand));
                        OnPropertyChanged(nameof(RedoCommand));
                        
                        // 更新保存命令的状态
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                });
            }
            catch (Exception ex)
            {
                // 记录异常但不影响用户体验
                // 静默处理异常，避免影响用户体验
            }
            });
        }

        private void Undo()
        {
            var previousState = _undoManager.Undo();
            if (previousState != null)
            {
                // 释放当前图像资源
                foreach (var image in Images)
                {
                    try
                    {
                        image.Dispose();
                    }
                    catch
                    {
                        // 忽略单个图像释放中的异常
                    }
                }
                
                Images.Clear();
                foreach (var img in previousState)
                {
                    Images.Add(img);
                    _currentSession?.AddImage(img);
                }
                
                OnPropertyChanged(nameof(UndoCommand));
                OnPropertyChanged(nameof(RedoCommand));
            }
        }

        private void Redo()
        {
            var nextState = _undoManager.Redo();
            if (nextState != null)
            {
                // 释放当前图像资源
                foreach (var image in Images)
                {
                    try
                    {
                        image.Dispose();
                    }
                    catch
                    {
                        // 忽略单个图像释放中的异常
                    }
                }
                
                Images.Clear();
                foreach (var img in nextState)
                {
                    Images.Add(img);
                    _currentSession?.AddImage(img);
                }
                
                OnPropertyChanged(nameof(UndoCommand));
                OnPropertyChanged(nameof(RedoCommand));
            }
        }

        public void SaveCurrentState()
        {
            // 创建深拷贝，避免UndoManager持有对原对象的引用
            var clonedImages = Images.Select(img => img.Clone()).ToList();
            _undoManager.PushState(clonedImages);
            OnPropertyChanged(nameof(UndoCommand));
            OnPropertyChanged(nameof(RedoCommand));
            
            // 更新保存命令的状态
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            });
        }

        private void ShowToast(string message)
        {
            try
            {
                // 在WPF中简化Toast通知实现
                // 使用MessageBox作为替代解决方案
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, "无缝拼图", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch {}
        }
        
        // 增强的垃圾回收方法，确保在生产环境中也能有效释放内存
        private void ForceGarbageCollection()
        {
            try
            {
                // 释放所有可能的非托管资源压力
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, 
                    (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
            }
            catch { /* 忽略可能的平台调用异常 */ }
            
            // 第一步：强制进行第2代垃圾回收（最大的对象）
            GC.Collect(2, GCCollectionMode.Forced, true);
            
            // 等待所有终结器完成执行
            GC.WaitForPendingFinalizers();
            
            // 第二步：再次强制进行所有代的垃圾回收
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            
            // 第三步：再次等待终结器
            GC.WaitForPendingFinalizers();
            
            // 第四步：针对大型对象堆进行专门的回收
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            
            // 优化大型对象堆(LOH)的压缩
            try
            {
                // 触发大型对象堆的压缩
                // 这在.NET 5+上可用，但为了兼容性我们使用反射调用
                var heapCompactionMethod = typeof(GC).GetMethod("Collect", 
                    new Type[] { typeof(int), typeof(GCCollectionMode), typeof(bool), typeof(bool) });
                if (heapCompactionMethod != null)
                {
                    heapCompactionMethod.Invoke(null, new object[] { 2, GCCollectionMode.Forced, true, true });
                }
                
                // 尝试使用GCSettings.LargeObjectHeapCompactionMode
                var lohCompactionMode = typeof(GC).GetProperty("LargeObjectHeapCompactionMode", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (lohCompactionMode != null)
                {
                    var compactingValue = Enum.Parse(lohCompactionMode.PropertyType, "CompactOnce");
                    lohCompactionMode.SetValue(null, compactingValue);
                    GC.Collect(2, GCCollectionMode.Forced, true);
                }
            }
            catch { /* 忽略反射调用可能的异常 */ }
        }
        
        // 引入P/Invoke调用，用于设置进程工作集大小，帮助释放内存
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        // 在程序退出时清理资源
        public void Cleanup()
        {
            Dispose();
        }
        
        private bool _disposed = false;
        
        public void Dispose()
        {
            if (!_disposed)
            {
                // 实施三重释放协议
                // 1. 取消所有异步操作
                _previewCts?.Cancel();
                _batchProcessingCts?.Cancel();
                
                // 2. 显式释放所有图像资源
                foreach (var image in Images)
                {
                    try
                    {
                        image.Dispose();
                    }
                    catch
                    {
                        // 忽略单个图像释放中的异常
                    }
                }
                
                // 3. 切断所有强引用并释放其他资源
                try
                {
                    _cachedFullSizeImage?.Dispose();
                }
                catch
                {
                    // 忽略异常
                }
                
                try
                {
                    _imageService.Dispose();
                }
                catch
                {
                    // 忽略异常
                }
                
                try
                {
                    _semaphore.Dispose();
                }
                catch
                {
                    // 忽略异常
                }
                
                try
                {
                    _batchProcessingCts?.Dispose();
                }
                catch
                {
                    // 忽略异常
                }
                
                try
                {
                    _previewCts?.Dispose();
                }
                catch
                {
                    // 忽略异常
                }
                
                try
                {
                    _currentSession?.Dispose();
                }
                catch
                {
                    // 忽略异常
                }
                
                _disposed = true;
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // 为了编译通过，定义Process类
    public static class Process
    {
        public static void Start(string fileName, string arguments)
        {
            try
            {
                System.Diagnostics.Process.Start(fileName, arguments);
            }
            catch {}
        }
    }
}