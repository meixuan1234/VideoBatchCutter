using System.Diagnostics;
using System.Text.RegularExpressions;
using VideoBatchCutter.Models;

namespace VideoBatchCutter.Services;

/// <summary>
/// FFmpeg视频处理服务，负责视频信息提取与裁剪操作
/// </summary>
public class FFmpegService
{
    private readonly string? _ffmpegPath;
    private readonly string? _ffprobePath;

    /// <summary>
    /// 初始化FFmpeg服务，自动查找系统PATH中的FFmpeg
    /// </summary>
    public FFmpegService()
    {
        _ffmpegPath = FindExecutable("ffmpeg.exe");
        _ffprobePath = FindExecutable("ffprobe.exe");
    }

    /// <summary>
    /// 检查FFmpeg是否可用
    /// </summary>
    public bool IsAvailable => !string.IsNullOrEmpty(_ffmpegPath) && !string.IsNullOrEmpty(_ffprobePath);

    /// <summary>
    /// 在系统PATH中查找可执行文件
    /// </summary>
    private static string? FindExecutable(string fileName)
    {
        // 首先检查当前目录
        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        if (File.Exists(localPath))
            return localPath;

        // 检查系统PATH
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (string path in pathEnv.Split(Path.PathSeparator))
        {
            string fullPath = Path.Combine(path.Trim(), fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    /// <summary>
    /// 获取视频文件的详细信息（时长、大小等）
    /// </summary>
    public async Task<VideoFileInfo?> GetVideoInfoAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var fileInfo = new FileInfo(filePath);
        var videoInfo = new VideoFileInfo
        {
            FilePath = filePath,
            FileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath),
            Extension = Path.GetExtension(filePath).ToLower(),
            FileSize = fileInfo.Length,
            Status = ProcessingStatus.Analyzing
        };

        // 检查FFmpeg是否可用
        if (!IsAvailable)
        {
            videoInfo.Status = ProcessingStatus.Failed;
            videoInfo.ErrorMessage = "FFmpeg环境未配置，请安装FFmpeg";
            return videoInfo;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath!,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("无法启动ffprobe进程");

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"ffprobe执行失败: {error}");

            if (double.TryParse(output.Trim(), out double duration) && duration > 0)
            {
                videoInfo.Duration = duration;
                videoInfo.Status = ProcessingStatus.Ready;
                return videoInfo;
            }

            throw new InvalidOperationException("无法解析视频时长");
        }
        catch (Exception ex)
        {
            videoInfo.Status = ProcessingStatus.Failed;
            videoInfo.ErrorMessage = $"分析视频失败: {ex.Message}";
            return videoInfo;
        }
    }

    /// <summary>
    /// 裁剪视频片段
    /// </summary>
    public async Task<bool> CutSegmentAsync(VideoSegment segment, string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        // 检查FFmpeg是否可用
        if (!IsAvailable)
            return false;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath!,
                Arguments = $"-y -ss {segment.StartTime:F3} -t {segment.Duration:F3} -i \"{inputPath}\" -c copy -avoid_negative_ts make_zero \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            // 读取错误输出（FFmpeg将进度输出到stderr）
            string errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            return process.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// 验证FFmpeg是否可用
    /// </summary>
    public async Task<bool> ValidateAsync()
    {
        // 首先检查是否找到了FFmpeg可执行文件
        if (!IsAvailable)
            return false;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath!,
                Arguments = "-version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
