using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Tiff;
using ResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;
using SeamlessPuzzle.Models;

namespace SeamlessPuzzle.Services
{
    public enum PuzzleMode
    {
        Horizontal,
        Vertical,
        Grid4,
        Grid9
    }

    public class ImageProcessingService
    {
        // 预览图最大宽度和高度限制
        private static readonly int MaxPreviewWidth = Math.Min(2560, (int)SystemParameters.PrimaryScreenWidth);
        private static readonly int MaxPreviewHeight = Math.Min(1440, (int)SystemParameters.PrimaryScreenHeight);
        
        // 使用并发字典缓存缩略图
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageCacheItem> _thumbnailCache 
            = new System.Collections.Concurrent.ConcurrentDictionary<string, ImageCacheItem>();
        
        // 添加内存缓存限制，避免占用过多内存
        private const int MaxCacheSize = 100;
        
        // 实现 IDisposable 以正确释放缓存资源
        private bool _disposed = false;
        
        // 缓存项类，用于实现LRU缓存策略
        private class ImageCacheItem
        {
            public Image<Rgba32> Image { get; set; }
            public DateTime LastAccessed { get; set; }
            
            public ImageCacheItem(Image<Rgba32> image)
            {
                Image = image;
                LastAccessed = DateTime.Now;
            }
        }
        
        public async Task<List<ImageModel>> LoadImageFilesAsync(IReadOnlyList<string> filePaths, IProgress<float>? progress = null)
        {
            var result = new List<ImageModel>();
            var processedCount = 0;

            // 增加并发度，提高加载速度
            var tasks = filePaths.Select(async filePath =>
            {
                try
                {
                    var model = await LoadSingleImageAsync(filePath, progress, processedCount, filePaths.Count);
                    if (model != null)
                    {
                        lock (result)
                        {
                            result.Add(model);
                        }
                    }
                }
                catch { 
                    // 发生异常时不中断整个过程
                }
                finally
                {
                    Interlocked.Increment(ref processedCount);
                    progress?.Report((float)processedCount / filePaths.Count);
                }
            });

            await Task.WhenAll(tasks);

            return result.OrderBy(m => m.OrderIndex).ToList();
        }

        private async Task<ImageModel?> LoadSingleImageAsync(string filePath, IProgress<float>? progress, int index, int total)
        {
            if (!File.Exists(filePath)) return null;

            var model = new ImageModel
            {
                FilePath = filePath,
                SourceType = ImageSourceType.File,
                OrderIndex = index,
                IsLoading = true
            };

            try
            {
                var image = await Task.Run(async () =>
                {
                    // 使用更大的缓冲区和异步读取提高性能
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, useAsync: true);
                    var img = await Image.LoadAsync<Rgba32>(stream);
                    
                    // 对于高分辨率图片，预先进行降采样处理以减少内存占用
                    if (img.Width > 8000 || img.Height > 8000)
                    {
                        var resized = img.Clone(x => x.Resize(new ResizeOptions
                        {
                            Size = new SixLabors.ImageSharp.Size(8000, 8000),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Triangle
                        }));
                        img.Dispose();
                        img = resized;
                    }
                    
                    img.Mutate(x => x.AutoOrient());
                    return img;
                });

                model.Image = image;
                model.Hash = CalculateImageHash(image);
                return model;
            }
            catch
            {
                // 出现异常时确保释放资源
                model.Dispose();
                return null;
            }
            finally
            {
                model.IsLoading = false;
            }
        }

        public async Task<ImageModel?> LoadPastedImageAsync(System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            var model = new ImageModel
            {
                SourceType = ImageSourceType.Clipboard,
                IsLoading = true
            };

            try
            {
                var image = await Task.Run(async () =>
                {
                    using var stream = new MemoryStream();
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                    stream.Seek(0, SeekOrigin.Begin);
                    var img = await Image.LoadAsync<Rgba32>(stream);
                    
                    // 对于高分辨率图片，预先进行降采样处理以减少内存占用
                    if (img.Width > 8000 || img.Height > 8000)
                    {
                        var resized = img.Clone(x => x.Resize(new ResizeOptions
                        {
                            Size = new SixLabors.ImageSharp.Size(8000, 8000),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Triangle
                        }));
                        img.Dispose();
                        img = resized;
                    }
                    
                    // 处理自动方向
                    img.Mutate(x => x.AutoOrient());
                    return img;
                });

                model.Image = image;
                model.Hash = CalculateImageHash(image);
                return model;
            }
            catch (Exception ex)
            {
                // 记录异常但不抛出，避免中断用户操作
                model.Dispose();
                return null;
            }
            finally
            {
                model.IsLoading = false;
            }
        }

        /// <summary>
        /// 生成原始分辨率的拼图（用于保存）
        /// </summary>
        public Task<Image<Rgba32>> CreatePuzzleAsync(IReadOnlyList<ImageModel> images, PuzzleMode mode, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
        {
            // 根据文档要求，CPU密集型操作必须在Task.Run中执行
            return Task.Run(() =>
            {
                switch (mode)
                {
                    case PuzzleMode.Horizontal:
                        return CreateHorizontalPuzzleAsync(images, false, progress, cancellationToken);
                    case PuzzleMode.Vertical:
                        return CreateVerticalPuzzleAsync(images, false, progress, cancellationToken);
                    case PuzzleMode.Grid4:
                        return CreateGridPuzzleAsync(images, 2, 2, false, progress, cancellationToken);
                    case PuzzleMode.Grid9:
                        return CreateGridPuzzleAsync(images, 3, 3, false, progress, cancellationToken);
                    default:
                        throw new ArgumentException("Unsupported puzzle mode");
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 生成降采样的预览图
        /// </summary>
        public Task<Image<Rgba32>> CreatePreviewPuzzleAsync(IReadOnlyList<ImageModel> images, PuzzleMode mode)
        {
            // 根据文档要求，CPU密集型操作必须在Task.Run中执行
            return Task.Run(() =>
            {
                // 对原始图片进行降采样处理
                var previewImages = new List<ImageModel>();
                try
                {
                    foreach (var model in images)
                    {
                        if (model.Image == null) continue;

                        // 使用更快的重采样算法来提高预览速度
                        var previewModel = new ImageModel
                        {
                            Image = model.Image.Clone(x => x.Resize(new ResizeOptions
                            {
                                Size = new SixLabors.ImageSharp.Size(MaxPreviewWidth / 4, MaxPreviewHeight / 4),
                                Mode = ResizeMode.Max,
                                Sampler = KnownResamplers.Triangle  // 使用更快的采样器
                            }))
                        };
                        previewImages.Add(previewModel);
                    }

                    // 使用降采样后的图片生成拼图
                    switch (mode)
                    {
                        case PuzzleMode.Horizontal:
                            return CreateHorizontalPuzzleAsync(previewImages, true);
                        case PuzzleMode.Vertical:
                            return CreateVerticalPuzzleAsync(previewImages, true);
                        case PuzzleMode.Grid4:
                            return CreateGridPuzzleAsync(previewImages, 2, 2, true);
                        case PuzzleMode.Grid9:
                            return CreateGridPuzzleAsync(previewImages, 3, 3, true);
                        default:
                            throw new ArgumentException("Unsupported puzzle mode");
                    }
                }
                finally
                {
                    // 确保预览图片被正确释放
                    foreach (var previewModel in previewImages)
                    {
                        try
                        {
                            previewModel.Dispose();
                        }
                        catch
                        {
                            // 忽略单个图像释放中的异常
                        }
                    }
                }
            });
        }

        private Image<Rgba32> CreateHorizontalPuzzleAsync(IReadOnlyList<ImageModel> images, bool isPreview = false, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
        {
            if (images.Count == 0) throw new ArgumentException("No images provided");

            var maxHeight = images.Max(img => img.Image!.Height);
            var totalWidth = images.Sum(img =>
            {
                var ratio = (float)maxHeight / img.Image!.Height;
                return (int)(img.Image.Width * ratio);
            });

            var result = new Image<Rgba32>(totalWidth, maxHeight);
            int currentX = 0;

            for (int i = 0; i < images.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var imgModel = images[i];
                var img = imgModel.Image!;
                var ratio = (float)maxHeight / img.Height;
                var newWidth = (int)(img.Width * ratio);

                // 使用原地操作避免创建不必要的中间图像副本
                using var resizedImage = img.Clone(ctx => ctx.Resize(newWidth, maxHeight, KnownResamplers.Lanczos3));
                result.Mutate(ctx => ctx.DrawImage(resizedImage, new SixLabors.ImageSharp.Point(currentX, 0), 1f));

                currentX += newWidth;
                
                // 报告进度 (0% - 100%)
                progress?.Report((float)(i + 1) / images.Count);
            }

            return result;
        }

        private Image<Rgba32> CreateVerticalPuzzleAsync(IReadOnlyList<ImageModel> images, bool isPreview = false, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
        {
            if (images.Count == 0) throw new ArgumentException("No images provided");

            var maxWidth = images.Max(img => img.Image!.Width);
            var totalHeight = images.Sum(img =>
            {
                var ratio = (float)maxWidth / img.Image!.Width;
                return (int)(img.Image.Height * ratio);
            });

            var result = new Image<Rgba32>(maxWidth, totalHeight);
            int currentY = 0;

            for (int i = 0; i < images.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var imgModel = images[i];
                var img = imgModel.Image!;
                var ratio = (float)maxWidth / img.Width;
                var newHeight = (int)(img.Height * ratio);

                // 使用原地操作避免创建不必要的中间图像副本
                using var resizedImage = img.Clone(ctx => ctx.Resize(maxWidth, newHeight, KnownResamplers.Lanczos3));
                result.Mutate(ctx => ctx.DrawImage(resizedImage, new SixLabors.ImageSharp.Point(0, currentY), 1f));

                currentY += newHeight;
                
                // 报告进度 (0% - 100%)
                progress?.Report((float)(i + 1) / images.Count);
            }

            return result;
        }

        private Image<Rgba32> CreateGridPuzzleAsync(IReadOnlyList<ImageModel> images, int cols, int rows, bool isPreview = false, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
        {
            if (images.Count == 0) throw new ArgumentException("No images provided");

            // 根据是否预览模式选择不同的单元格大小
            var cellSize = isPreview ? Math.Min(MaxPreviewWidth / cols, MaxPreviewHeight / rows) : 1000;
            var totalWidth = cols * cellSize;
            var totalHeight = rows * cellSize;

            var result = new Image<Rgba32>(totalWidth, totalHeight);

            // 记录上次更新进度的时间
            var lastProgressReport = DateTime.UtcNow;
            var minProgressInterval = TimeSpan.FromMilliseconds(50); // 最小进度更新间隔

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var index = i * cols + j;
                    if (index < images.Count)
                    {
                        var imgModel = images[index];
                        var img = imgModel.Image!;

                        // 使用原地操作避免创建不必要的中间图像副本
                        using var resizedImage = img.Clone(ctx => ctx.Resize(new ResizeOptions
                        {
                            Size = new SixLabors.ImageSharp.Size(cellSize, cellSize),
                            Mode = ResizeMode.Crop,
                            Position = AnchorPositionMode.Center,
                            Sampler = KnownResamplers.Lanczos3
                        }));
                        
                        result.Mutate(ctx => ctx.DrawImage(resizedImage, new SixLabors.ImageSharp.Point(j * cellSize, i * cellSize), 1f));
                    }
                    
                    // 智能进度报告 - 避免过于频繁的更新
                    if (images.Count > 0)
                    {
                        var now = DateTime.UtcNow;
                        if (images.Count < 10 || (now - lastProgressReport) >= minProgressInterval)
                        {
                            progress?.Report((float)(index + 1) / images.Count);
                            lastProgressReport = now;
                        }
                    }
                }
            }
            
            // 确保最后一次进度更新
            if (images.Count > 0)
            {
                progress?.Report(1.0f);
            }

            return result;
        }

        /// <summary>
        /// 为高可靠性场景预留扩展点，添加可选参数 ensurePhysicalWrite
        /// </summary>
        public async Task<string> SavePuzzleAsync(Image<Rgba32> image, PuzzleMode mode, int imageCount, IProgress<float>? progress = null, CancellationToken cancellationToken = default, bool ensurePhysicalWrite = false)
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"SeamlessPuzzle_{timestamp}.png";
            var filePath = Path.Combine(desktopPath, fileName);

            // 防止文件覆盖
            int counter = 1;
            while (File.Exists(filePath))
            {
                fileName = $"SeamlessPuzzle_{timestamp}_{counter}.png";
                filePath = Path.Combine(desktopPath, fileName);
                counter++;
            }

            FileStream? fileStream = null;
            try
            {
                // 处理自动方向
                image.Mutate(x => x.AutoOrient());

                // 根据文档要求设置压缩级别为Level6
                var encoder = new PngEncoder
                {
                    CompressionLevel = PngCompressionLevel.Level6,
                    ColorType = PngColorType.RgbWithAlpha
                };

                // 报告编码开始进度 (0%)
                progress?.Report(0.0f);
                
                // 创建文件流
                fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                
                // 保存图片到文件流
                await image.SaveAsync(fileStream, encoder, cancellationToken);
                
                // 根据保存系统文档要求，根据 ensurePhysicalWrite 参数决定是否强制物理落盘
                await fileStream.FlushAsync(cancellationToken);
                if (ensurePhysicalWrite)
                {
                    // 仅在需要高可靠性时才强制刷新到磁盘
                    fileStream.Flush(true);
                }

                // 报告完成进度 (100%)
                progress?.Report(1.0f);

                return filePath;
            }
            catch (Exception)
            {
                // 重新抛出异常，让调用方处理
                throw;
            }
            finally
            {
                // 确保文件流被正确释放
                if (fileStream != null)
                {
                    try
                    {
                        await fileStream.DisposeAsync();
                    }
                    catch
                    {
                        // 忽略异常
                    }
                }
            }
        }

        /// <summary>
        /// 根据图片像素数量设置合理的压缩级别
        /// </summary>
        private SixLabors.ImageSharp.Formats.Png.PngCompressionLevel GetCompressionLevel(Image<Rgba32> image)
        {
            // 计算总像素数
            long totalPixels = (long)image.Width * image.Height;
            
            // 根据像素数量设置压缩级别
            // 像素≤2M，使用压缩等级6
            if (totalPixels <= 2000000)
                return SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.Level6;
            // 像素2M - 4M，使用压缩等级7
            else if (totalPixels <= 4000000)
                return SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.Level7;
            // 像素4M - 7M，使用压缩等级8
            else if (totalPixels <= 7000000)
                return SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.Level8;
            // 像素7M - 12M，使用压缩等级9
            else if (totalPixels <= 12000000)
                return SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.Level9;
            // 像素12M - 22M，使用压缩等级9 (ImageSharp最高支持Level9)
            else if (totalPixels <= 22000000)
                return SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.Level9;
            // 像素>22M，使用压缩等级9 (ImageSharp最高支持Level9)
            else
                return SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.Level9;
        }

        private string CalculateImageHash(Image<Rgba32> image)
        {
            using var resized = image.Clone(x => x.Resize(8, 8, KnownResamplers.Triangle));
            using var stream = new MemoryStream();
            resized.Save(stream, new PngEncoder { CompressionLevel = 0 });
            var hash = System.Security.Cryptography.SHA256.Create().ComputeHash(stream.ToArray());
            return BitConverter.ToString(hash).Replace("-", "");
        }

        public async Task<ImageModel?> LoadSampleImageAsync(int sampleIndex)
        {
            var resourcePath = $"pack://application:,,,/Resources/sample{sampleIndex}.png";
            var model = new ImageModel
            {
                SourceType = ImageSourceType.File,
                IsLoading = true
            };

            try
            {
                var image = await Task.Run(async () =>
                {
                    using var stream = System.Windows.Application.GetResourceStream(new Uri(resourcePath)).Stream;
                    var img = await Image.LoadAsync<Rgba32>(stream);
                    
                    // 对于高分辨率图片，预先进行降采样处理以减少内存占用
                    if (img.Width > 8000 || img.Height > 8000)
                    {
                        var resized = img.Clone(x => x.Resize(new ResizeOptions
                        {
                            Size = new SixLabors.ImageSharp.Size(8000, 8000),
                            Mode = ResizeMode.Max,
                            Sampler = KnownResamplers.Triangle
                        }));
                        img.Dispose();
                        img = resized;
                    }
                    
                    return img;
                });

                model.Image = image;
                model.Hash = CalculateImageHash(image);
                return model;
            }
            catch
            {
                // 出现异常时确保释放资源
                model.Dispose();
                return null;
            }
            finally
            {
                model.IsLoading = false;
            }
        }
        
        private BitmapSource ConvertToBitmapSource(Image<Rgba32> image)
        {
            using var memoryStream = new MemoryStream();
            // 使用更快的压缩级别来提高转换速度
            image.Save(memoryStream, new PngEncoder() { 
                CompressionLevel = PngCompressionLevel.Level1,
                ColorType = PngColorType.RgbWithAlpha
            });
            memoryStream.Seek(0, SeekOrigin.Begin);
            var bitmap = BitmapFrame.Create(memoryStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            bitmap.Freeze(); // 冻结以提高性能并帮助垃圾回收
            return bitmap;
        }
        
        // 添加或更新缓存项，并实现LRU策略
        private void AddToCache(string key, Image<Rgba32> image)
        {
            // 如果缓存已达到最大大小，移除最少使用的项
            if (_thumbnailCache.Count >= MaxCacheSize)
            {
                // 获取最早访问的10个项并释放
                var itemsToRemove = _thumbnailCache.OrderBy(kvp => kvp.Value.LastAccessed).Take(10);
                foreach (var kvp in itemsToRemove)
                {
                    if (_thumbnailCache.TryRemove(kvp.Key, out var item))
                    {
                        try
                        {
                            item.Image.Dispose();
                        }
                        catch
                        {
                            // 忽略异常
                        }
                    }
                }
            }
            
            // 添加新项到缓存
            _thumbnailCache[key] = new ImageCacheItem(image);
        }
        
        // 从缓存获取项，并更新最后访问时间
        private Image<Rgba32>? GetFromCache(string key)
        {
            if (_thumbnailCache.TryGetValue(key, out var item))
            {
                // 更新最后访问时间
                item.LastAccessed = DateTime.Now;
                return item.Image.Clone(); // 返回克隆以避免外部代码修改缓存的图像
            }
            return null;
        }
        
        // 清理缓存，增强版本
        public void ClearCache()
        {
            foreach (var kvp in _thumbnailCache)
            {
                try
                {
                    kvp.Value?.Image.Dispose();
                }
                catch
                {
                    // 忽略释放过程中的异常，确保清理过程不被中断
                }
            }
            _thumbnailCache.Clear();
            
            // 强制垃圾回收，特别是大型对象堆
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced);
            
            // 压缩大型对象堆
            try
            {
                var gcType = typeof(GC);
                var loHeapCompactionMode = gcType.GetProperty("LargeObjectHeapCompactionMode", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                if (loHeapCompactionMode != null)
                {
                    loHeapCompactionMode.SetValue(null, System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce);
                    GC.Collect(2, GCCollectionMode.Forced);
                }
            }
            catch { }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        ClearCache();
                    }
                    catch
                    {
                        // 忽略异常
                    }
                }
                _disposed = true;
            }
        }
    }
}