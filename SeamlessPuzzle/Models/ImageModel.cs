using System; 
using SixLabors.ImageSharp; 
using SixLabors.ImageSharp.PixelFormats; 
using System.ComponentModel; 
using System.Windows.Media.Imaging;
using System.IO;
using SixLabors.ImageSharp.Formats.Png;

namespace SeamlessPuzzle.Models
{
    public enum ImageSourceType
    {
        File,
        Clipboard
    }

    public class ImageModel : IDisposable, INotifyPropertyChanged
    {
        private Image<Rgba32>? _image;
        private bool _isSelected = true;
        private BitmapSource? _thumbnail;
        
        // 添加缩略图缓存标志
        private bool _isThumbnailCached = false;

        public string? FilePath { get; set; }
        
        public Image<Rgba32>? Image 
        { 
            get => _image; 
            set 
            { 
                _image = value; 
                OnPropertyChanged(nameof(Image));
                // 当Image属性变更时，清空缩略图缓存，以便下次访问时重新生成
                _thumbnail = null;
                _isThumbnailCached = false;
                OnPropertyChanged(nameof(Thumbnail));
            } 
        }
        
        public ImageSourceType SourceType { get; set; }
        public int OrderIndex { get; set; }
        public string? Hash { get; set; }
        
        public bool IsSelected 
        { 
            get => _isSelected; 
            set 
            { 
                _isSelected = value; 
                OnPropertyChanged(nameof(IsSelected));
            } 
        }
        
        public bool IsLoading { get; set; }
        public float LoadProgress { get; set; }
        
        // 缩略图属性，用于UI显示
        public BitmapSource Thumbnail
        {
            get
            {
                if (_thumbnail == null && _image != null)
                {
                    _thumbnail = ConvertToBitmapSource(_image);
                    _isThumbnailCached = true;
                }
                return _thumbnail ?? BitmapFrame.Create(new MemoryStream(), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
        }

        private BitmapSource ConvertToBitmapSource(Image<Rgba32> image)
        {
            using var memoryStream = new MemoryStream();
            // 使用更快的压缩级别来提高转换速度
            image.Save(memoryStream, new PngEncoder() { 
                CompressionLevel = SixLabors.ImageSharp.Formats.Png.PngCompressionLevel.Level1,
                ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.RgbWithAlpha
            });
            memoryStream.Seek(0, SeekOrigin.Begin);
            var bitmap = BitmapFrame.Create(memoryStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            bitmap.Freeze(); // 冻结以提高性能并帮助垃圾回收
            return bitmap;
        }

        private bool _disposed = false;
        
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
                    // 释放托管资源 - 正确释放BitmapSource
                    if (_thumbnail != null)
                    {
                        try
                        {
                            // 对于BitmapImage，需要清除UriSource
                            if (_thumbnail is System.Windows.Media.Imaging.BitmapImage bitmapImage)
                            {
                                bitmapImage.UriSource = null;
                            }
                            // 对于其他类型的BitmapSource，确保缓存被清除
                            else if (_thumbnail is System.Windows.Media.Imaging.BitmapFrame bitmapFrame)
                            {
                                bitmapFrame.Freeze(); // 如果未冻结，先冻结
                            }
                        }
                        catch
                        {
                            // 忽略释放过程中的异常
                        }
                        finally
                        {
                            _thumbnail = null;
                            _isThumbnailCached = false;
                        }
                    }
                }
                
                // 释放非托管资源 - 先释放Image，避免触发PropertyChanged事件
                Image?.Dispose();
                _image = null;  // 直接设置字段，避免触发属性setter
                
                _disposed = true;
            }
        }
        
        ~ImageModel()
        {
            Dispose(false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 创建当前图像模型的深拷贝副本，用于撤销管理器
        /// </summary>
        public ImageModel Clone()
        {
            var clone = new ImageModel
            {
                FilePath = this.FilePath,
                SourceType = this.SourceType,
                OrderIndex = this.OrderIndex,
                Hash = this.Hash,
                IsSelected = this.IsSelected,
                IsLoading = this.IsLoading,
                LoadProgress = this.LoadProgress
            };

            // 如果当前有图像，创建图像的克隆
            if (this.Image != null)
            {
                clone.Image = this.Image.Clone();
            }

            return clone;
        }
    }
}