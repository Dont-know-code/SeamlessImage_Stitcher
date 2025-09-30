# SeamlessImage Stitcher - 无缝拼图

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4?logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

一款轻量级、高性能的无损无缝图像拼接软件，专为创建高质量、无边框的连续图像作品而设计。

## ✨ 特性亮点

### 🚀 高性能处理
- **多线程并发**：充分利用多核CPU，快速完成图像拼接
- **智能缓存机制**：采用LRU缓存策略，优化内存使用效率
- **实时预览**：即时显示拼接效果，所见即所得

### 🎯 专业拼接模式
- **水平拼接**：创建宽幅全景图像
- **垂直拼接**：制作纵向连续图像
- **4宫格布局**：2×2网格拼图模式
- **9宫格布局**：3×3网格拼图模式

### 🛡️ 无损图像处理
- **无缝过渡**：先进的边缘融合算法，消除拼接痕迹
- **格式兼容**：支持PNG、JPEG、BMP、GIF、WebP、TIFF等主流格式
- **质量保持**：保持原始图像质量，无压缩损失

### 💻 现代化界面
- **WPF技术**：基于.NET 8.0的现代化用户界面
- **主题切换**：支持深色/浅色主题
- **直观操作**：拖拽式文件添加，简单易用

## 📋 系统要求

| 组件 | 最低要求 | 推荐配置 |
|------|----------|----------|
| 操作系统 | Windows 10 (64位) | Windows 11 (64位) |
| .NET运行时 | .NET 8.0 Desktop Runtime | .NET 8.0 Desktop Runtime |
| 处理器 | 双核CPU | 四核或以上CPU |
| 内存 | 4GB RAM | 8GB RAM或以上 |
| 硬盘空间 | 100MB可用空间 | 500MB可用空间 |

## 🚀 快速开始

### 下载安装

1. 从 [Releases页面](https://github.com/your-repo/releases) 下载最新版本
2. 运行安装程序或解压便携版
3. 确保系统已安装 [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### 使用方法

1. **添加图像**
   - 拖拽图像文件到软件窗口
   - 或点击"添加图像"按钮选择文件

2. **选择拼接模式**
   - 水平拼接：适合全景照片
   - 垂直拼接：适合纵向图像
   - 4宫格/9宫格：适合社交媒体拼图

3. **预览与调整**
   - 实时查看拼接效果
   - 调整图像顺序（拖拽排序）
   - 使用撤销/重做功能

4. **保存结果**
   - 选择输出格式和质量
   - 指定保存路径
   - 点击"保存"完成

## 🛠️ 开发构建

### 环境要求
- Visual Studio 2022 或更高版本
- .NET 8.0 SDK
- Git

### 构建步骤

```bash
# 克隆仓库
git clone https://github.com/your-repo/seamless-image-stitcher.git
cd seamless-image-stitcher

# 还原NuGet包
dotnet restore

# 构建项目
dotnet build --configuration Release

# 运行测试
dotnet test
```

### 项目结构

```
SeamlessImage_Stitcher/
├── Launcher/                 # 启动器项目
│   ├── Launcher.cs           # 启动器主程序
│   └── Launcher.csproj       # 项目配置
├── SeamlessPuzzle/           # 主应用程序
│   ├── Models/               # 数据模型
│   │   ├── ImageModel.cs     # 图像模型
│   │   └── PuzzleSession.cs  # 拼图会话
│   ├── Services/             # 服务层
│   │   └── ImageProcessingService.cs  # 图像处理服务
│   ├── ViewModels/           # 视图模型
│   │   └── MainViewModel.cs  # 主视图模型
│   ├── Views/                # 用户界面
│   │   ├── MainWindow.xaml   # 主窗口
│   │   └── Converters/       # 转换器
│   ├── Utils/                # 工具类
│   │   ├── LanguageManager.cs # 语言管理
│   │   └── UndoManager.cs    # 撤销管理
│   └── Resources/            # 资源文件
├── SeamlessPuzzle.sln        # 解决方案文件
└── README.md                 # 项目说明
```

## 🔧 技术架构

### 核心技术栈
- **.NET 8.0**: 现代化、高性能的应用程序框架
- **WPF (Windows Presentation Foundation)**: 丰富的桌面应用程序界面
- **SixLabors.ImageSharp**: 高性能的图像处理库
- **MVVM模式**: 清晰的架构分离

### 架构特点
- **模块化设计**: 各功能模块独立，便于维护和扩展
- **异步处理**: 所有耗时操作均采用异步模式，保证界面流畅
- **内存管理**: 智能的资源释放和缓存策略
- **错误处理**: 完善的异常处理机制

## 📖 使用场景

### 摄影爱好者
- 创建全景照片
- 拼接多张连续拍摄的照片
- 制作摄影作品集

### 设计师
- 制作社交媒体拼图
- 创建产品展示图
- 设计宣传材料

### 普通用户
- 家庭照片拼接
- 旅行照片整理
- 创意图像制作

## 🤝 贡献指南

我们欢迎各种形式的贡献！

### 报告问题
- 使用 [Issues页面](https://github.com/your-repo/issues) 报告bug或建议
- 提供详细的问题描述和复现步骤

### 提交代码
1. Fork 本项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

### 开发规范
- 遵循C#编码规范
- 添加必要的注释和文档
- 确保所有测试通过

## 📄 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 🙏 致谢

感谢以下开源项目的支持：
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) - 强大的图像处理库
- [.NET Foundation](https://dotnetfoundation.org/) - 优秀的开发平台

## 📞 联系方式

- 项目主页: [GitHub Repository](https://github.com/your-repo)
- 问题反馈: [Issues](https://github.com/your-repo/issues)
- 邮箱: your-email@example.com

---

**SeamlessImage Stitcher** - 让图像拼接变得简单而专业！ 🎨
