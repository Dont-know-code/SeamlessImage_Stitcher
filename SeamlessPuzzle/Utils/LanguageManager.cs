using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SeamlessPuzzle.Utils
{
    public class LanguageManager : INotifyPropertyChanged
    {
        private static LanguageManager _instance;
        private Language _currentLanguage = Language.Chinese;

        public static LanguageManager Instance => _instance ?? (_instance = new LanguageManager());

        public Language CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    OnPropertyChanged();
                    UpdateLanguage();
                }
            }
        }

        public Dictionary<Language, Dictionary<string, string>> Resources { get; } = new Dictionary<Language, Dictionary<string, string>>
        {
            {
                Language.Chinese, new Dictionary<string, string>
                {
                    // 主窗口标题和基本文本
                    {"WindowTitle", "无缝拼图"},
                    {"Title", "无缝拼图"},
                    {"ImageList", "图片列表"},
                    {"DragDropArea", "拖放图片到此处"},
                    {"OrPressCtrlV", "或按 Ctrl+V 粘贴图片"},
                    {"PuzzleMode", "拼接模式"},
                    {"AddedImages", "已添加图片"},
                    {"PuzzlePreview", "拼图预览"},
                    {"PreviewNote", "这是预览图，为了加载更快做了压缩，但"},
                    {"but the image you save will be as clear as the imported ones without compression", "你保存的图片和导入时一样清晰，不会被压缩"},
                    {"CtrlScrollZoom", "Ctrl+滚轮 可以缩放图片"},
                    {"Clear", "清空"},
                    {"CurrentMode", "当前模式: "},
                    {"SavePuzzle", "保存拼图"},
                    {"Ready", "就绪"},
                    {"ReorderWarning", "拼图之后调整顺序，请重新选择拼图模式！"},
                    {"SaveWarning", "正在保存中，在收到成功保存的弹窗之前，请勿关闭软件！！！"},
                    
                    // 拼接模式
                    {"HorizontalPuzzle", "水平拼接"},
                    {"VerticalPuzzle", "垂直拼接"},
                    {"Grid4Puzzle", "4宫格拼接"},
                    {"Grid9Puzzle", "9宫格拼接"},
                    
                    // 设置按钮和菜单
                    {"Settings", "设置"},
                    {"Language", "语言"},
                    {"Chinese", "中文"},
                    {"English", "English"},
                }
            },
            {
                Language.English, new Dictionary<string, string>
                {
                    // Main window title and basic text
                    {"WindowTitle", "Seamless Puzzle"},
                    {"Title", "Seamless Puzzle"},
                    {"ImageList", "Image List"},
                    {"DragDropArea", "Drag and drop images here"},
                    {"OrPressCtrlV", "or press Ctrl+V to paste images"},
                    {"PuzzleMode", "Puzzle Mode"},
                    {"AddedImages", "Added Images"},
                    {"PuzzlePreview", "Puzzle Preview"},
                    {"PreviewNote", "This is a preview image. It's compressed for faster loading, but "},
                    {"but the image you save will be as clear as the imported ones without compression", "the image you save will be as clear as the imported ones without compression"},
                    {"CtrlScrollZoom", "Ctrl+Scroll to zoom the image"},
                    {"Clear", "Clear"},
                    {"CurrentMode", "Current Mode: "},
                    {"SavePuzzle", "Save Puzzle"},
                    {"Ready", "Ready"},
                    {"ReorderWarning", "Order adjusted after puzzle creation, please reselect puzzle mode!"},
                    {"SaveWarning", "Saving... Don’t close the app until you see the success message!!!"},
                    
                    // Puzzle modes
                    {"HorizontalPuzzle", "Horizontal"},
                    {"VerticalPuzzle", "Vertical"},
                    {"Grid4Puzzle", "4-Grid"},
                    {"Grid9Puzzle", "9-Grid"},
                    
                    // Settings button and menu
                    {"Settings", "Settings"},
                    {"Language", "Language"},
                    {"Chinese", "中文"},
                    {"English", "English"},
                }
            }
        };

        public string GetString(string key)
        {
            if (Resources.ContainsKey(CurrentLanguage) && Resources[CurrentLanguage].ContainsKey(key))
            {
                return Resources[CurrentLanguage][key];
            }
            return key; // 如果找不到对应的语言文本，返回键名作为默认值
        }

        private void UpdateLanguage()
        {
            // 更新应用程序的语言设置
            switch (CurrentLanguage)
            {
                case Language.Chinese:
                    CultureInfo.CurrentUICulture = new CultureInfo("zh-CN");
                    break;
                case Language.English:
                    CultureInfo.CurrentUICulture = new CultureInfo("en-US");
                    break;
            }
            
            // 触发属性变更通知
            OnPropertyChanged(nameof(CurrentLanguage));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum Language
    {
        Chinese,
        English
    }
}