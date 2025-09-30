using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32; // 添加注册表访问命名空间

class Program
{
    // 定义常量，便于维护
    private const string REGISTRY_PATH = @"Software\无缝拼图\SeamlessPuzzle\Settings";
    private const string APP_PATH_KEY = "AppPath";
    private const string MAIN_EXE_NAME = "SeamlessPuzzle.exe";

    [STAThread]
    static void Main()
    {
        try
        {
            string targetPath = null;
            
            // 1. 尝试从注册表读取应用程序路径
            targetPath = GetPathFromRegistry();
            
            // 2. 如果注册表读取失败或路径无效，使用备用的相对路径
            if (targetPath == null)
            {
                targetPath = GetPathFromRelativeLocation();
            }
            
            // 3. 作为最后的备选方案，尝试从当前目录直接查找
            if (targetPath == null || !File.Exists(targetPath))
            {
                string launcherDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                targetPath = Path.Combine(launcherDir, MAIN_EXE_NAME);
            }
            
            // 检查目标文件是否存在
            if (!File.Exists(targetPath))
            {
                MessageBox.Show("找不到主程序文件！\n请确保" + MAIN_EXE_NAME + "存在于正确的位置。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
            
            // 启动目标程序
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = targetPath,
                WorkingDirectory = Path.GetDirectoryName(targetPath),
                UseShellExecute = false
            };
            
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动程序失败！\n错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// 尝试从注册表读取应用程序路径
    /// </summary>
    /// <returns>如果成功读取并验证通过则返回完整路径，否则返回null</returns>
    private static string GetPathFromRegistry()
    {
        try
        {
            // 尝试从CurrentUser读取
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH))
            {
                if (key != null)
                {
                    string appPath = key.GetValue(APP_PATH_KEY) as string;
                    if (!string.IsNullOrEmpty(appPath))
                    {
                        string fullPath = Path.Combine(appPath, MAIN_EXE_NAME);
                        // 验证注册表中的路径是否有效
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }
            }

            // 如果CurrentUser读取失败，尝试从LocalMachine读取
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(REGISTRY_PATH))
            {
                if (key != null)
                {
                    string appPath = key.GetValue(APP_PATH_KEY) as string;
                    if (!string.IsNullOrEmpty(appPath))
                    {
                        string fullPath = Path.Combine(appPath, MAIN_EXE_NAME);
                        // 验证注册表中的路径是否有效
                        if (File.Exists(fullPath))
                        {
                            return fullPath;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 注册表读取失败，可以选择记录日志或忽略
            // 在实际应用中，可能需要添加日志记录
            Console.WriteLine("注册表读取失败: " + ex.Message);
        }

        return null;
    }

    /// <summary>
    /// 从相对位置获取应用程序路径
    /// </summary>
    /// <returns>构建的相对路径</returns>
    private static string GetPathFromRelativeLocation()
    {
        try
        {
            // 获取当前exe文件的路径
            string launcherPath = Process.GetCurrentProcess().MainModule.FileName;
            string launcherDir = Path.GetDirectoryName(launcherPath);
            
            // 构建目标程序路径
            return Path.Combine(launcherDir, "SeamlessPuzzle", "bin", "Release", "net8.0-windows", "win-x64", MAIN_EXE_NAME);
        }
        catch (Exception ex)
        {
            Console.WriteLine("相对路径构建失败: " + ex.Message);
            return null;
        }
    }
}