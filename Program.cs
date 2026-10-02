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

        try
        {
            // 创建并显示主窗口
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            var logPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory,
                "startup_error.log");
            System.IO.File.WriteAllText(logPath, $"启动异常: {ex}\r\n");
            MessageBox.Show($"启动失败: {ex.Message}\r\n详细信息已保存到: {logPath}", "启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
