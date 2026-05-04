using System.IO.Compression;
using VideoBatchCutter.Models;

namespace VideoBatchCutter.Services;

/// <summary>
/// 视频批量处理器，协调整个裁剪流程
/// </summary>
public class VideoProcessor
{
    private readonly FFmpegService _ffmpegService;
    private readonly SegmentGenerator _segmentGenerator;
    private int _globalSegmentIndex;
    private readonly Random _random = new Random();

    /// <summary>
    /// 进度更新事件
    /// </summary>
    public event EventHandler<ProcessingProgress>? ProgressChanged;

    /// <summary>
    /// 日志消息事件
    /// </summary>
    public event EventHandler<string>? LogMessage;

    /// <summary>
    /// 处理完成事件
    /// </summary>
    public event EventHandler<ProcessingResult>? ProcessingCompleted;

    public VideoProcessor()
    {
        _ffmpegService = new FFmpegService();
        _segmentGenerator = new SegmentGenerator();
    }

    /// <summary>
    /// 验证FFmpeg环境
    /// </summary>
    public async Task<bool> ValidateEnvironmentAsync()
    {
        return await _ffmpegService.ValidateAsync();
    }

    /// <summary>
    /// 获取单个视频的信息（时长等）
    /// </summary>
    public async Task<VideoFileInfo?> GetVideoInfoAsync(string filePath)
    {
        return await _ffmpegService.GetVideoInfoAsync(filePath);
    }

    /// <summary>
    /// 执行批量视频裁剪处理
    /// </summary>
    public async Task<ProcessingResult> ProcessAsync(
        List<VideoFileInfo> videos,
        ProcessingConfig config,
        IProgress<ProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProcessingResult();
        var overallProgress = new ProcessingProgress
        {
            TotalFiles = videos.Count,
            IsProcessing = true
        };

        _globalSegmentIndex = 0;

        try
        {
            LogMessage?.Invoke(this, $"开始处理 {videos.Count} 个视频文件...");

            Directory.CreateDirectory(config.OutputDirectory);

            _segmentGenerator.GenerateAllSegments(videos, config);
            int totalSegments = videos.Sum(v => v.Segments.Count);
            overallProgress.TotalSegments = totalSegments;

            LogMessage?.Invoke(this, $"共生成 {totalSegments} 个片段待裁剪");

            int completedSegments = 0;

            for (int i = 0; i < videos.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    LogMessage?.Invoke(this, "处理已取消");
                    break;
                }

                var video = videos[i];
                overallProgress.CurrentFileIndex = i + 1;
                overallProgress.CurrentFileName = video.FileNameWithoutExtension;
                overallProgress.CurrentOperation = $"正在处理: {video.FileNameWithoutExtension}";

                if (video.Status == ProcessingStatus.Failed || video.Duration <= 0)
                {
                    LogMessage?.Invoke(this, $"跳过无效视频: {video.FileNameWithoutExtension}");
                    continue;
                }

                video.Status = ProcessingStatus.Processing;

                for (int j = 0; j < video.Segments.Count; j++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var segment = video.Segments[j];
                    segment.Status = SegmentStatus.Processing;

                    _globalSegmentIndex++;
                    string outputFileName = BuildOutputFileName(video, segment, config, _globalSegmentIndex);
                    string outputPath = Path.Combine(config.OutputDirectory, outputFileName);
                    segment.OutputFileName = outputFileName;
                    segment.OutputPath = outputPath;

                    LogMessage?.Invoke(this, $"裁剪片段 {segment.SegmentIndex}/{video.Segments.Count}: {video.FileNameWithoutExtension} [{segment.StartTimeText}]");

                    bool success = await _ffmpegService.CutSegmentAsync(segment, video.FilePath, outputPath, cancellationToken);

                    if (success)
                    {
                        segment.Status = SegmentStatus.Completed;
                        result.SuccessfulSegments++;
                        LogMessage?.Invoke(this, $"✓ 片段裁剪完成: {outputFileName}");
                    }
                    else
                    {
                        segment.Status = SegmentStatus.Failed;
                        result.FailedSegments++;
                        LogMessage?.Invoke(this, $"✗ 片段裁剪失败: {outputFileName}");
                    }

                    completedSegments++;
                    overallProgress.CompletedSegments = completedSegments;
                    overallProgress.CurrentFileProgress = (int)((double)(j + 1) / video.Segments.Count * 100);
                    overallProgress.OverallProgress = (int)((double)completedSegments / totalSegments * 100);

                    progress?.Report(overallProgress);
                    ProgressChanged?.Invoke(this, overallProgress);
                }

                video.Status = video.Segments.All(s => s.Status == SegmentStatus.Completed)
                    ? ProcessingStatus.Completed
                    : ProcessingStatus.Failed;
            }

            if (config.CreateZipArchive && !cancellationToken.IsCancellationRequested)
            {
                overallProgress.CurrentOperation = "正在打包ZIP文件...";
                progress?.Report(overallProgress);
                ProgressChanged?.Invoke(this, overallProgress);

                string zipPath = await CreateZipArchiveAsync(config, videos, cancellationToken);
                result.ZipFilePath = zipPath;

                if (!string.IsNullOrEmpty(zipPath))
                {
                    LogMessage?.Invoke(this, $"ZIP打包完成: {zipPath}");

                    if (config.DeleteOriginalAfterZip)
                    {
                        LogMessage?.Invoke(this, "正在删除原始剪辑文件...");
                        int deletedCount = DeleteOriginalSegments(videos);
                        LogMessage?.Invoke(this, $"已删除 {deletedCount} 个原始剪辑文件");
                    }
                }
            }

            overallProgress.IsProcessing = false;
            overallProgress.IsCompleted = true;
            overallProgress.CurrentOperation = "处理完成";
            progress?.Report(overallProgress);
            ProgressChanged?.Invoke(this, overallProgress);

            LogMessage?.Invoke(this, $"处理完成！成功: {result.SuccessfulSegments}, 失败: {result.FailedSegments}");
        }
        catch (OperationCanceledException)
        {
            overallProgress.IsProcessing = false;
            LogMessage?.Invoke(this, "处理已取消");
            throw;
        }
        catch (Exception ex)
        {
            overallProgress.IsProcessing = false;
            LogMessage?.Invoke(this, $"处理出错: {ex.Message}");
            result.ErrorMessage = ex.Message;
        }

        ProcessingCompleted?.Invoke(this, result);
        return result;
    }

    /// <summary>
    /// 构建输出文件名（全局唯一）
    /// </summary>
    private string BuildOutputFileName(VideoFileInfo video, VideoSegment segment, ProcessingConfig config, int globalIndex)
    {
        string baseName;

        if (config.KeepOriginalFileName || string.IsNullOrWhiteSpace(config.OutputPrefix))
        {
            baseName = video.FileNameWithoutExtension;
        }
        else
        {
            baseName = config.OutputPrefix;
        }

        int randomNum = _random.Next(1000, 10000);
        
        // 根据配置决定随机数字的位置
        string fileName;
        if (config.RandomSort)
        {
            // 随机排序：数字放在开头
            fileName = $"{randomNum:D4}_{baseName}_G{globalIndex:D4}_S{segment.SegmentIndex:D2}.mp4";
        }
        else
        {
            // 默认：数字放在末尾
            fileName = $"{baseName}_G{globalIndex:D4}_S{segment.SegmentIndex:D2}_{randomNum:D4}.mp4";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }

        return fileName;
    }

    /// <summary>
    /// 创建ZIP压缩包
    /// </summary>
    private async Task<string> CreateZipArchiveAsync(ProcessingConfig config, List<VideoFileInfo> videos, CancellationToken cancellationToken)
    {
        string zipPath = Path.Combine(config.OutputDirectory, config.ZipFileName);

        try
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            var completedSegments = videos
                .SelectMany(v => v.Segments)
                .Where(s => s.Status == SegmentStatus.Completed && File.Exists(s.OutputPath))
                .ToList();

            if (completedSegments.Count == 0)
            {
                LogMessage?.Invoke(this, "没有成功裁剪的片段，跳过ZIP打包");
                return string.Empty;
            }

            await Task.Run(() =>
            {
                using var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

                foreach (var segment in completedSegments)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    string entryName = segment.OutputFileName;
                    zipArchive.CreateEntryFromFile(segment.OutputPath, entryName, CompressionLevel.Optimal);
                }
            }, cancellationToken);

            return zipPath;
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke(this, $"ZIP打包失败: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 删除原始剪辑文件
    /// </summary>
    private int DeleteOriginalSegments(List<VideoFileInfo> videos)
    {
        int deletedCount = 0;

        var completedSegments = videos
            .SelectMany(v => v.Segments)
            .Where(s => s.Status == SegmentStatus.Completed && File.Exists(s.OutputPath))
            .ToList();

        foreach (var segment in completedSegments)
        {
            try
            {
                File.Delete(segment.OutputPath);
                deletedCount++;
            }
            catch
            {
            }
        }

        return deletedCount;
    }
}

/// <summary>
/// 处理结果
/// </summary>
public class ProcessingResult
{
    public int SuccessfulSegments { get; set; }
    public int FailedSegments { get; set; }
    public string ZipFilePath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
}
