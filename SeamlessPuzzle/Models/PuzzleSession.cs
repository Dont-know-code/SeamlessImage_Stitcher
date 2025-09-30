using System;
using System.Collections.Generic;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SeamlessPuzzle.Models;
using System.Windows.Media.Imaging;

namespace SeamlessPuzzle.Models
{
    /// <summary>
    /// 拼图会话类，用于管理单次拼图操作的所有资源
    /// 实现会话隔离模型和三重释放协议
    /// </summary>
    public class PuzzleSession : IDisposable
    {
        // 拼图块集合
        public List<ImageModel> Tiles { get; private set; } = new List<ImageModel>();
        
        // 合成的大图
        public Image<Rgba32>? CompositeImage { get; set; }
        
        // 预览图
        public Image<Rgba32>? PreviewImage { get; set; }
        
        // WPF预览图
        public BitmapSource? Preview { get; set; }
        
        // 取消令牌源，用于取消异步操作
        public CancellationTokenSource CancellationTokenSource { get; private set; } = new CancellationTokenSource();
        
        // 会话是否已被释放
        private bool _disposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        public PuzzleSession()
        {
        }

        /// <summary>
        /// 添加图像到会话中
        /// </summary>
        /// <param name="imageModel">要添加的图像</param>
        public void AddImage(ImageModel imageModel)
        {
            Tiles.Add(imageModel);
        }

        /// <summary>
        /// 实现 IDisposable 接口，确保资源被正确释放
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的核心方法
        /// 实现三重释放协议：
        /// 1. 取消所有异步操作
        /// 2. 显式释放所有图像资源
        /// 3. 切断所有强引用
        /// </summary>
        /// <param name="disposing">是否正在显式释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 1. 取消所有异步操作
                    CancellationTokenSource?.Cancel();
                    
                    // 等待一小段时间让任务完成
                    // 注意：实际应用中可能需要更好的机制来确保任务完成
                    Thread.Sleep(10);
                    
                    // 2. 显式释放所有图像资源
                    // 释放拼图块
                    foreach (var tile in Tiles)
                    {
                        try
                        {
                            tile?.Dispose();
                        }
                        catch
                        {
                            // 忽略单个图像释放中的异常
                        }
                    }
                    
                    // 释放合成的大图
                    try
                    {
                        CompositeImage?.Dispose();
                    }
                    catch
                    {
                        // 忽略异常
                    }
                    
                    // 释放预览图
                    try
                    {
                        PreviewImage?.Dispose();
                    }
                    catch
                    {
                        // 忽略异常
                    }
                    
                    // 释放WPF预览图引用
                    if (Preview != null)
                    {
                        try
                        {
                            // 特殊处理BitmapImage的释放
                            if (Preview is BitmapImage bitmapImage)
                            {
                                bitmapImage.UriSource = null;
                            }
                            // 特殊处理BitmapFrame的释放
                            if (Preview is BitmapFrame bitmapFrame)
                            {
                                bitmapFrame.Freeze();
                            }
                        }
                        catch
                        {
                            // 忽略释放过程中的异常
                        }
                        Preview = null;
                    }
                    
                    // 3. 切断所有强引用
                    Tiles.Clear();
                    Tiles = null!;
                    CompositeImage = null;
                    PreviewImage = null;
                    CancellationTokenSource?.Dispose();
                    CancellationTokenSource = null!;
                }
                
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数，作为安全网
        /// </summary>
        ~PuzzleSession()
        {
            Dispose(false);
        }
    }
}