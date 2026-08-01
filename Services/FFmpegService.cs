using System.Diagnostics;
using System.Text.RegularExpressions;
using VideoBatchCutter.Models;

namespace VideoBatchCutter.Services;

/// <summary>
/// FFmpeg执行结果，包含成功与否、错误输出、是否发生编码器回退以及最终使用的编码器
/// </summary>
/// <param name="Success">是否执行成功</param>
/// <param name="ErrorOutput">FFmpeg stderr 输出内容</param>
/// <param name="WasFallbackUsed">是否因硬件编码器失败而回退到 CPU 编码器</param>
/// <param name="FinalEncoder">最终实际使用的输出编码器</param>
public record FFmpegResult(bool Success, string ErrorOutput, bool WasFallbackUsed, OutputEncoder FinalEncoder);

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
    /// 根据输出编码器构建 FFmpeg 视频编码参数
    /// </summary>
    /// <param name="encoder">输出编码器</param>
    /// <returns>编码参数字符串数组</returns>
    /// <remarks>
    /// 返回数组可直接拼接到 ProcessStartInfo.ArgumentList，
    /// 需要字符串拼接时可用 <see cref="string.Join(string?, string[])"/> 组合。
    /// </remarks>
    public static string[] BuildVideoCodecArgs(OutputEncoder encoder)
    {
        return encoder switch
        {
            OutputEncoder.CpuH264 => new[] { "-c:v", "libx264", "-preset", "fast", "-crf", "23" },
            OutputEncoder.CpuH265 => new[] { "-c:v", "libx265", "-preset", "fast", "-crf", "28" },
            OutputEncoder.NvidiaH264 => new[] { "-c:v", "h264_nvenc", "-preset", "p4", "-cq", "23" },
            OutputEncoder.NvidiaH265 => new[] { "-c:v", "hevc_nvenc", "-preset", "p4", "-cq", "28" },
            OutputEncoder.IntelH264 => new[] { "-c:v", "h264_qsv", "-preset", "balanced", "-global_quality", "23" },
            OutputEncoder.IntelH265 => new[] { "-c:v", "hevc_qsv", "-preset", "balanced", "-global_quality", "28" },
            OutputEncoder.AmdH264 => new[] { "-c:v", "h264_amf", "-quality", "balanced", "-qp_p", "23", "-qp_i", "23" },
            OutputEncoder.AmdH265 => new[] { "-c:v", "hevc_amf", "-quality", "balanced", "-qp_p", "28", "-qp_i", "28" },
            _ => new[] { "-c:v", "libx264", "-preset", "fast", "-crf", "23" }
        };
    }

    /// <summary>
    /// 获取编码器的友好显示名称（用于日志输出）
    /// </summary>
    /// <param name="encoder">输出编码器</param>
    /// <returns>编码器友好名称</returns>
    public static string GetEncoderDisplayName(OutputEncoder encoder)
    {
        return encoder switch
        {
            OutputEncoder.CpuH264 => "CPU H.264",
            OutputEncoder.CpuH265 => "CPU H.265",
            OutputEncoder.NvidiaH264 => "NVIDIA NVENC H.264",
            OutputEncoder.NvidiaH265 => "NVIDIA NVENC H.265",
            OutputEncoder.IntelH264 => "Intel QuickSync H.264",
            OutputEncoder.IntelH265 => "Intel QuickSync H.265",
            OutputEncoder.AmdH264 => "AMD AMF H.264",
            OutputEncoder.AmdH265 => "AMD AMF H.265",
            _ => encoder.ToString()
        };
    }

    /// <summary>
    /// 判断 FFmpeg 错误输出是否为硬件编码器不可用导致的失败
    /// </summary>
    /// <param name="errorOutput">FFmpeg stderr 输出</param>
    /// <returns>是否为硬件编码器失败</returns>
    public static bool IsHardwareEncoderFailure(string errorOutput)
    {
        if (string.IsNullOrWhiteSpace(errorOutput))
            return false;

        // 统一转小写后再匹配，实现不区分大小写的关键字检测
        string lower = errorOutput.ToLowerInvariant();

        string[] hardwareFailureKeywords = new[]
        {
            "unknown encoder",
            "no nvenc capable devices found",
            "cannot load nvencodeapi64.dll",
            "codec not currently supported in container",
            "failed to initialize encoder",
            "encoder not found",
            "not supported"
        };

        foreach (string keyword in hardwareFailureKeywords)
        {
            if (lower.Contains(keyword))
                return true;
        }

        // 针对 QSV / AMF 的额外组合判断
        bool mentionsQsvOrAmf = lower.Contains("qsv") || lower.Contains("amf");
        bool indicatesMissingOrUnsupported = lower.Contains("not found") || lower.Contains("unsupported");

        if (mentionsQsvOrAmf && indicatesMissingOrUnsupported)
            return true;

        return false;
    }

    /// <summary>
    /// 获取硬件编码器失败后的回退编码器
    /// </summary>
    /// <param name="encoder">当前失败的编码器</param>
    /// <returns>回退编码器；若已是 CPU 编码器或无可回退目标则返回 null</returns>
    /// <remarks>
    /// 回退链：
    /// <list type="bullet">
    /// <item>NvidiaH264 / IntelH264 / AmdH264 → CpuH264</item>
    /// <item>NvidiaH265 / IntelH265 / AmdH265 → CpuH265 → CpuH264</item>
    /// <item>CpuH265 → CpuH264</item>
    /// </list>
    /// </remarks>
    public static OutputEncoder? GetFallbackEncoder(OutputEncoder encoder)
    {
        return encoder switch
        {
            OutputEncoder.NvidiaH264 => OutputEncoder.CpuH264,
            OutputEncoder.IntelH264 => OutputEncoder.CpuH264,
            OutputEncoder.AmdH264 => OutputEncoder.CpuH264,
            OutputEncoder.NvidiaH265 => OutputEncoder.CpuH265,
            OutputEncoder.IntelH265 => OutputEncoder.CpuH265,
            OutputEncoder.AmdH265 => OutputEncoder.CpuH265,
            OutputEncoder.CpuH265 => OutputEncoder.CpuH264,
            _ => null
        };
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
    /// <param name="segment">待裁剪的片段信息</param>
    /// <param name="inputPath">输入视频路径</param>
    /// <param name="outputPath">输出视频路径</param>
    /// <param name="encoder">输出编码器</param>
    /// <param name="highQualityMode">是否使用高质量重新编码模式；false 时使用流复制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>包含执行结果、回退信息以及最终编码器的 <see cref="FFmpegResult"/></returns>
    /// <remarks>
    /// 当硬件编码器失败且检测到可回退目标时，会自动尝试回退。
    /// H.265 硬件编码器最多可能经历两次回退（例如 NVENC H.265 → CPU H.265 → CPU H.264）。
    /// </remarks>
    public async Task<FFmpegResult> CutSegmentAsync(
        VideoSegment segment,
        string inputPath,
        string outputPath,
        OutputEncoder encoder,
        bool highQualityMode = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(inputPath) || string.IsNullOrEmpty(outputPath))
            return new FFmpegResult(false, "输入或输出路径为空", false, encoder);

        // 清理可能存在的失败输出文件，避免影响后续回退尝试的文件存在性判断
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch
        {
            // 若文件无法删除（如被占用），继续执行，后续会以文件大小是否大于 0 作为成功判断
        }

        OutputEncoder currentEncoder = encoder;
        OutputEncoder? previousEncoder = null;
        bool wasFallbackUsed = false;
        string lastErrorOutput = string.Empty;

        // 最大回退深度：H.265 硬件 → CPU H.265 → CPU H.264 需要两次回退
        const int MaxFallbackDepth = 2;

        for (int attempt = 0; attempt <= MaxFallbackDepth; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 非首次尝试时，基于上一次失败的编码器计算回退目标
            if (attempt > 0)
            {
                if (!previousEncoder.HasValue)
                    break;

                OutputEncoder? fallback = GetFallbackEncoder(previousEncoder.Value);
                if (!fallback.HasValue)
                    break;

                currentEncoder = fallback.Value;
                wasFallbackUsed = true;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

                string args;
                if (highQualityMode)
                {
                    // 使用配置的编码器参数替换原先硬编码的 libx264
                    string codecArgs = string.Join(" ", BuildVideoCodecArgs(currentEncoder));
                    args = $"-y -ss {segment.StartTime:F3} -t {segment.Duration:F3} -i \"{inputPath}\" {codecArgs} -c:a aac -movflags +faststart \"{outputPath}\"";
                }
                else
                {
                    // 非高质量模式采用流复制，不使用视频编码器参数
                    args = $"-y -ss {segment.StartTime:F3} -t {segment.Duration:F3} -i \"{inputPath}\" -c copy -avoid_negative_ts make_zero \"{outputPath}\"";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath!,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    lastErrorOutput = "无法启动FFmpeg进程";
                    previousEncoder = currentEncoder;
                    continue;
                }

                // 读取错误输出（FFmpeg将进度输出到stderr）
                string errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                lastErrorOutput = errorOutput;

                if (process.ExitCode == 0 && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                {
                    return new FFmpegResult(true, errorOutput, wasFallbackUsed, currentEncoder);
                }

                // 失败且不属于硬件编码器相关错误时，直接结束，不再尝试回退
                if (!IsHardwareEncoderFailure(errorOutput))
                    break;

                // 记录本次失败的编码器，用于下一次回退计算
                previousEncoder = currentEncoder;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastErrorOutput = ex.Message;
                break;
            }
        }

        return new FFmpegResult(false, lastErrorOutput, wasFallbackUsed, currentEncoder);
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
