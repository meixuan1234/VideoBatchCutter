namespace VideoBatchCutter.Models;

/// <summary>
/// 视频批量处理配置参数
/// </summary>
public class ProcessingConfig
{
    /// <summary>
    /// 每个片段的截取时长（秒），默认10秒
    /// </summary>
    public int SegmentDuration { get; set; } = 10;

    /// <summary>
    /// 每个视频要截取的段数，默认3段
    /// </summary>
    public int SegmentsPerVideo { get; set; } = 3;

    /// <summary>
    /// 输出文件名前缀，为空则使用原文件名
    /// </summary>
    public string OutputPrefix { get; set; } = string.Empty;

    /// <summary>
    /// 是否保留原文件名（true：原文件名_序号；false：前缀_序号）
    /// </summary>
    public bool KeepOriginalFileName { get; set; } = true;

    /// <summary>
    /// 输出目录
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "VideoBatchCutter");

    /// <summary>
    /// 是否打包为ZIP
    /// </summary>
    public bool CreateZipArchive { get; set; } = true;

    /// <summary>
    /// ZIP文件名
    /// </summary>
    public string ZipFileName { get; set; } = "VideoClips.zip";

    /// <summary>
    /// 是否打包为ZIP后删除原始剪辑文件
    /// </summary>
    public bool DeleteOriginalAfterZip { get; set; } = false;

    /// <summary>
    /// 是否随机排序（随机数字放在文件名开头）
    /// </summary>
    public bool RandomSort { get; set; } = false;

    /// <summary>
    /// 验证配置参数是否合法
    /// </summary>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (SegmentDuration <= 0)
            return (false, "片段时长必须大于0秒");

        if (SegmentsPerVideo <= 0)
            return (false, "每个视频的截取段数必须大于0");

        if (SegmentDuration < 1)
            return (false, "片段时长至少为1秒");

        if (SegmentsPerVideo > 100)
            return (false, "每个视频最多截取100段");

        return (true, null);
    }
}
