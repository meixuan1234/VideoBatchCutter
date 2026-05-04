namespace VideoBatchCutter;

using System;
using System.Windows.Forms;
using VideoBatchCutter.UI;

/// <summary>
/// 程序入口类
/// </summary>
internal static class Program
{
    /// <summary>
    /// 应用程序主入口点
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        
        // 创建并显示主窗口
        Application.Run(new MainForm());
    }
}
