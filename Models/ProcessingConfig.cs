namespace VideoBatchCutter.Models;

/// <summary>
/// 输出编码器选项
/// </summary>
public enum OutputEncoder
{
    /// <summary>CPU H.264（libx264，兼容性最好）</summary>
    CpuH264,
    /// <summary>CPU H.265（libx265，体积更小）</summary>
    CpuH265,
    /// <summary>NVIDIA NVENC H.264</summary>
    NvidiaH264,
    /// <summary>NVIDIA NVENC H.265 / HEVC</summary>
    NvidiaH265,
    /// <summary>Intel QuickSync H.264</summary>
    IntelH264,
    /// <summary>Intel QuickSync H.265 / HEVC</summary>
    IntelH265,
    /// <summary>AMD AMF H.264</summary>
    AmdH264,
    /// <summary>AMD AMF H.265 / HEVC</summary>
    AmdH265
}

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
    /// 是否使用高质量模式（重新编码，避免卡顿；关闭则快速复制）
    /// </summary>
    public bool HighQualityMode { get; set; } = false;

    /// <summary>
    /// 是否去除视频前5秒（true：片段起始时间从第5秒之后开始计算；false：从视频开头开始计算）
    /// </summary>
    /// <remarks>默认值 false，保持原有行为不变</remarks>
    public bool SkipFirstFiveSeconds { get; set; } = false;

    /// <summary>
    /// 输出编码器，默认 CPU H.264（兼容性最好）
    /// </summary>
    public OutputEncoder OutputEncoder { get; set; } = OutputEncoder.CpuH264;

    /// <summary>
    /// 验证配置参数是否合法
    /// </summary>
    /// <returns>
    /// 一个元组：
    /// <list type="bullet">
    /// <item>IsValid：配置是否合法</item>
    /// <item>ErrorMessage：非法时的错误提示，合法时为 null</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// 注意：启用 <see cref="SkipFirstFiveSeconds"/> 后，具体每个视频的可用区间是否合法
    /// （如视频时长是否大于5秒、去除前5秒后是否仍能容纳片段）由 <see cref="Services.SegmentGenerator"/>
    /// 在按视频生成片段时处理并记录日志。
    /// </remarks>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (SegmentDuration <= 0)
            return (false, "片段时长必须大于0秒");

        if (SegmentsPerVideo <= 0)
            return (false, "每个视频的截取段数必须大于0");

        if (SegmentsPerVideo > 512)
            return (false, "每个视频最多截取512段");

        // 启用去除前5秒时，片段起始点需 >= 5 秒，因此需要保证去除前5秒后仍有可用区间。
        // 由于 Validate 不持有具体视频时长，配置层面仅校验片段时长至少为1秒；
        // 实际每个视频的可用区间是否合法，由 SegmentGenerator 在生成片段时处理并记录日志。
        if (SkipFirstFiveSeconds && SegmentDuration < 1)
            return (false, "启用去除前5秒时，片段时长至少为1秒，否则去除后无可用区间");

        return (true, null);
    }
}
