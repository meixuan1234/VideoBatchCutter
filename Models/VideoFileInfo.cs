using System.ComponentModel;

namespace VideoBatchCutter.Models;

/// <summary>
/// 视频文件信息模型，包含文件路径、时长、处理状态等元数据
/// 实现 INotifyPropertyChanged 接口，使 DataGridView 等绑定控件能够自动刷新 UI
/// </summary>
public class VideoFileInfo : INotifyPropertyChanged
{
    /// <summary>
    /// 当前处理状态的后备字段
    /// </summary>
    private ProcessingStatus _status = ProcessingStatus.Pending;

    /// <summary>
    /// 处理进度百分比（0-100）的后备字段
    /// </summary>
    private int _progress;

    /// <summary>
    /// 属性变更事件，用于通知绑定控件刷新界面
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 触发属性变更通知
    /// </summary>
    /// <param name="propertyName">发生变化的属性名称</param>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 唯一标识符
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 原始文件完整路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 原始文件名（不含扩展名）
    /// </summary>
    public string FileNameWithoutExtension { get; set; } = string.Empty;

    /// <summary>
    /// 文件扩展名
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// 视频总时长（秒）
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// 格式化后的时长字符串
    /// </summary>
    public string DurationText => TimeSpan.FromSeconds(Duration).ToString(@"hh\:mm\:ss");

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 格式化后的文件大小
    /// </summary>
    public string FileSizeText => FileSize > 1024 * 1024 * 1024
        ? $"{FileSize / (1024.0 * 1024 * 1024):F2} GB"
        : $"{FileSize / (1024.0 * 1024):F2} MB";

    /// <summary>
    /// 当前处理状态
    /// 设置新值时触发 Status 与 StatusText 的属性变更通知，确保 UI 状态列实时刷新
    /// </summary>
    public ProcessingStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    /// <summary>
    /// 状态描述文本
    /// </summary>
    public string StatusText => Status switch
    {
        ProcessingStatus.Pending => "等待处理",
        ProcessingStatus.Analyzing => "分析中...",
        ProcessingStatus.Ready => "准备就绪",
        ProcessingStatus.Processing => "裁剪中...",
        ProcessingStatus.Completed => "已完成",
        ProcessingStatus.Failed => "处理失败",
        _ => "未知状态"
    };

    /// <summary>
    /// 处理进度百分比（0-100）
    /// 设置新值时触发 Progress 的属性变更通知，确保 DataGridView 进度列实时刷新
    /// </summary>
    public int Progress
    {
        get => _progress;
        set
        {
            if (_progress != value)
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }
    }

    /// <summary>
    /// 生成的片段列表
    /// </summary>
    public List<VideoSegment> Segments { get; set; } = new();

    /// <summary>
    /// 错误信息（处理失败时）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 视频处理状态枚举
/// </summary>
public enum ProcessingStatus
{
    Pending,
    Analyzing,
    Ready,
    Processing,
    Completed,
    Failed
}
