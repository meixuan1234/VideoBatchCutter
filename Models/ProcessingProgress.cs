namespace VideoBatchCutter.Models;

/// <summary>
/// 处理进度信息，用于UI实时更新
/// </summary>
public class ProcessingProgress
{
    /// <summary>
    /// 当前处理的视频文件索引
    /// </summary>
    public int CurrentFileIndex { get; set; }

    /// <summary>
    /// 视频文件总数
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// 当前视频文件名
    /// </summary>
    public string CurrentFileName { get; set; } = string.Empty;

    /// <summary>
    /// 当前视频处理进度（0-100）
    /// </summary>
    public int CurrentFileProgress { get; set; }

    /// <summary>
    /// 总体进度（0-100）
    /// </summary>
    public int OverallProgress { get; set; }

    /// <summary>
    /// 当前操作描述
    /// </summary>
    public string CurrentOperation { get; set; } = string.Empty;

    /// <summary>
    /// 已完成的片段数
    /// </summary>
    public int CompletedSegments { get; set; }

    /// <summary>
    /// 总片段数
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// 是否正在处理中
    /// </summary>
    public bool IsProcessing { get; set; }

    /// <summary>
    /// 处理是否已完成
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// 进度文本描述
    /// </summary>
    public string ProgressText => $"{CurrentFileIndex}/{TotalFiles} - {CurrentFileName} ({CurrentFileProgress}%)";
}
