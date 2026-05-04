namespace VideoBatchCutter.Models;

/// <summary>
/// 视频片段信息，表示从原视频中裁剪出的一段
/// </summary>
public class VideoSegment
{
    /// <summary>
    /// 片段唯一标识
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 所属视频文件ID
    /// </summary>
    public Guid VideoFileId { get; set; }

    /// <summary>
    /// 片段序号（用于生成文件名）
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// 在原视频中的起始时间（秒）
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// 片段时长（秒）
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// 格式化后的起始时间
    /// </summary>
    public string StartTimeText => TimeSpan.FromSeconds(StartTime).ToString(@"hh\:mm\:ss");

    /// <summary>
    /// 输出文件路径
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// 输出文件名
    /// </summary>
    public string OutputFileName { get; set; } = string.Empty;

    /// <summary>
    /// 处理状态
    /// </summary>
    public SegmentStatus Status { get; set; } = SegmentStatus.Pending;

    /// <summary>
    /// 状态描述文本
    /// </summary>
    public string StatusText => Status switch
    {
        SegmentStatus.Pending => "等待裁剪",
        SegmentStatus.Processing => "裁剪中...",
        SegmentStatus.Completed => "裁剪完成",
        SegmentStatus.Failed => "裁剪失败",
        _ => "未知"
    };
}

/// <summary>
/// 片段处理状态
/// </summary>
public enum SegmentStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}
