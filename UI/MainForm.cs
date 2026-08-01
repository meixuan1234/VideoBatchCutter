using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text.Json;
using VideoBatchCutter.Models;
using VideoBatchCutter.Services;

namespace VideoBatchCutter.UI;

/// <summary>
/// 主窗口，提供视频批量剪辑的图形界面
/// </summary>
public class MainForm : Form
{
    private readonly VideoProcessor _processor;
    private readonly BindingList<VideoFileInfo> _videoList;
    private readonly ProcessingConfig _config;
    private CancellationTokenSource? _cancellationTokenSource;

    #region 界面控件声明

    private NumericUpDown? _durationNumeric;
    private NumericUpDown? _segmentsNumeric;
    private TextBox? _prefixTextBox;
    private CheckBox? _keepNameCheckBox;
    private CheckBox? _zipCheckBox;
    private CheckBox? _deleteOriginalCheckBox;
    private CheckBox? _randomSortCheckBox;
    private CheckBox? _highQualityCheckBox;
    private CheckBox? _skipFirstFiveSecondsCheckBox;
    private ComboBox? _encoderComboBox;
    private Label? _estimateLabel;
    private TextBox? _outputPathTextBox;
    private ProgressBar? _overallProgressBar;
    private TextBox? _logTextBox;
    private DataGridView? _dataGridView;
    private Button? _startButton;
    private Button? _cancelButton;
    private Label? _progressLabel;
    private Panel? _dropPanel;

    #endregion

    #region TRAE 设计 Token（暗色优先）

    // ----- 背景层级 -----
    /// <summary>基底背景 #1A1B1D</summary>
    private static readonly Color BackgroundBaseColor = Color.FromArgb(26, 27, 29);
    /// <summary>次级面板/卡片背景 #222427</summary>
    private static readonly Color BackgroundSecondaryColor = Color.FromArgb(34, 36, 39);
    /// <summary>三级表面背景 #2A2D31</summary>
    private static readonly Color BackgroundTertiaryColor = Color.FromArgb(42, 45, 49);
    /// <summary>交替行背景 #1F2124</summary>
    private static readonly Color BackgroundAlternateRowColor = Color.FromArgb(31, 33, 36);

    // ----- 品牌强调色 -----
    /// <summary>品牌色 #32F08C</summary>
    private static readonly Color BrandColor = Color.FromArgb(50, 240, 140);
    /// <summary>品牌色悬停 #2CD67D</summary>
    private static readonly Color BrandHoverColor = Color.FromArgb(44, 214, 125);
    /// <summary>品牌色按下 #25BC6E</summary>
    private static readonly Color BrandPressedColor = Color.FromArgb(37, 188, 110);
    /// <summary>半透明品牌色（选中行/悬停背景）#2850F08C</summary>
    private static readonly Color BrandTransparentColor = Color.FromArgb(40, 50, 240, 140);
    /// <summary>半透明品牌色（拖放区虚线边框）#5050F08C</summary>
    private static readonly Color BrandBorderTransparentColor = Color.FromArgb(80, 50, 240, 140);

    // ----- 文字色 -----
    /// <summary>默认正文 #D1D3DB</summary>
    private static readonly Color TextPrimaryColor = Color.FromArgb(209, 211, 219);
    /// <summary>次要文字 #9599A6</summary>
    private static readonly Color TextSecondaryColor = Color.FromArgb(149, 153, 166);
    /// <summary>三级文字/禁用 #666B75</summary>
    private static readonly Color TextDisabledColor = Color.FromArgb(102, 107, 117);
    /// <summary>品牌色上的反白文字 #0C0C0D</summary>
    private static readonly Color TextOnBrandColor = Color.FromArgb(12, 12, 13);

    // ----- 半透明边框（叠加在深色背景上） -----
    /// <summary>4% 白色 #0AFFFFFF</summary>
    private static readonly Color BorderWhite4 = Color.FromArgb(10, 255, 255, 255);
    /// <summary>6% 白色 #0FFFFFFF</summary>
    private static readonly Color BorderWhite6 = Color.FromArgb(15, 255, 255, 255);
    /// <summary>8% 白色 #14FFFFFF</summary>
    private static readonly Color BorderWhite8 = Color.FromArgb(20, 255, 255, 255);
    /// <summary>12% 白色 #1FFFFFFF</summary>
    private static readonly Color BorderWhite12 = Color.FromArgb(31, 255, 255, 255);
    /// <summary>DataGridView 网格线颜色（必须不透明，近似 6% 白色叠加深色背景）</summary>
    private static readonly Color GridLineColor = Color.FromArgb(40, 42, 45);

    // ----- 字体 -----
    /// <summary>界面正文字体，Windows 优先 Segoe UI，中文回退 Microsoft YaHei</summary>
    private static readonly Font UIFont = CreateFontWithFallback("Segoe UI", "Microsoft YaHei", 9F);
    /// <summary>界面标题/加粗字体</summary>
    private static readonly Font UIFontBold = CreateFontWithFallback("Segoe UI", "Microsoft YaHei", 9F, FontStyle.Bold);
    /// <summary>拖放区标题字体</summary>
    private static readonly Font DropZoneTitleFont = CreateFontWithFallback("Segoe UI", "Microsoft YaHei", 11F);
    /// <summary>日志/代码字体</summary>
    private static readonly Font LogFont = CreateFontWithFallback("Consolas", "Microsoft YaHei", 9F);

    /// <summary>
    /// 按优先顺序创建字体，主字体不可用时回退到备用字体，最终回退到系统默认无衬线字体
    /// </summary>
    /// <param name="primaryFamily">首选字体名称</param>
    /// <param name="fallbackFamily">备用字体名称</param>
    /// <param name="emSize">字体大小（磅）</param>
    /// <param name="style">字体样式</param>
    /// <returns>创建成功的字体对象</returns>
    private static Font CreateFontWithFallback(string primaryFamily, string fallbackFamily, float emSize, FontStyle style = FontStyle.Regular)
    {
        try
        {
            return new Font(primaryFamily, emSize, style, GraphicsUnit.Point);
        }
        catch
        {
            try
            {
                return new Font(fallbackFamily, emSize, style, GraphicsUnit.Point);
            }
            catch
            {
                return new Font(FontFamily.GenericSansSerif, emSize, style, GraphicsUnit.Point);
            }
        }
    }

    // ----- 间距（基于 4px 网格） -----
    private const int Spacing4 = 4;
    private const int Spacing8 = 8;
    private const int Spacing12 = 12;
    private const int Spacing16 = 16;
    private const int Spacing24 = 24;

    // ----- 圆角 -----
    /// <summary>小圆角 4px</summary>
    private const int CornerRadiusSmall = 4;
    /// <summary>中圆角 6px</summary>
    private const int CornerRadiusMedium = 6;

    // ----- 尺寸 -----
    private const int SettingsPanelWidth = 520;
    private const int OutputPathRowHeight = 55;
    private const int DropRowHeight = 90;
    private const int LogRowHeight = 180;
    private const int DefaultFormWidth = 1280;
    private const int DefaultFormHeight = 750;
    private const int MinFormWidth = 1100;
    private const int MinFormHeight = 650;
    private const int BrowseButtonWidth = 130;
    private const int BrowseButtonHeight = 34;
    private const int ClearButtonWidth = 130;
    private const int ClearButtonHeight = 34;
    private const int OutputPathLabelWidth = 95;
    private const int LabelColumnWidth = 110;
    private const int SettingsRowHeight = 40;
    private const int ButtonRowHeight = 40;

    #endregion

    /// <summary>
    /// 设置文件保存路径
    /// </summary>
    private readonly string _settingsPath = Path.Combine(
        AppContext.BaseDirectory,
        "VideoBatchCutter_settings.json");

    /// <summary>
    /// 初始化主窗口
    /// </summary>
    public MainForm()
    {
        _processor = new VideoProcessor();
        _videoList = new BindingList<VideoFileInfo>();
        _config = new ProcessingConfig();

        _processor.ProgressChanged += OnProcessorProgressChanged;
        _processor.LogMessage += OnProcessorLogMessage;
        _processor.ProcessingCompleted += OnProcessorProcessingCompleted;

        LoadSettings();
        SetupUI();
        LoadVideoListFromSettings();
    }

    #region 设置加载与保存

    /// <summary>
    /// 从 JSON 文件加载用户设置到配置对象
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (settings == null)
                return;

            if (settings.TryGetValue("SegmentDuration", out var duration) && duration.ValueKind == JsonValueKind.Number)
                _config.SegmentDuration = duration.GetInt32();

            if (settings.TryGetValue("SegmentsPerVideo", out var segments) && segments.ValueKind == JsonValueKind.Number)
                _config.SegmentsPerVideo = segments.GetInt32();

            if (settings.TryGetValue("OutputPrefix", out var prefix))
                _config.OutputPrefix = prefix.GetString() ?? string.Empty;

            if (settings.TryGetValue("KeepOriginalFileName", out var keep) && IsBooleanElement(keep))
                _config.KeepOriginalFileName = keep.GetBoolean();

            if (settings.TryGetValue("CreateZipArchive", out var zip) && IsBooleanElement(zip))
                _config.CreateZipArchive = zip.GetBoolean();

            if (settings.TryGetValue("DeleteOriginalAfterZip", out var del) && IsBooleanElement(del))
                _config.DeleteOriginalAfterZip = del.GetBoolean();

            if (settings.TryGetValue("RandomSort", out var randomSort) && IsBooleanElement(randomSort))
                _config.RandomSort = randomSort.GetBoolean();

            if (settings.TryGetValue("HighQualityMode", out var highQuality) && IsBooleanElement(highQuality))
                _config.HighQualityMode = highQuality.GetBoolean();

            if (settings.TryGetValue("SkipFirstFiveSeconds", out var skipFirst) && IsBooleanElement(skipFirst))
                _config.SkipFirstFiveSeconds = skipFirst.GetBoolean();

            if (settings.TryGetValue("OutputDirectory", out var dir))
                _config.OutputDirectory = dir.GetString() ?? string.Empty;

            if (settings.TryGetValue("OutputEncoder", out var encoder) && encoder.ValueKind == JsonValueKind.String &&
                Enum.TryParse<OutputEncoder>(encoder.GetString(), out var parsedEncoder))
                _config.OutputEncoder = parsedEncoder;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 将当前配置保存到 JSON 文件
    /// </summary>
    private void SaveSettings()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var settings = new Dictionary<string, object>
            {
                ["SegmentDuration"] = _config.SegmentDuration,
                ["SegmentsPerVideo"] = _config.SegmentsPerVideo,
                ["OutputPrefix"] = _config.OutputPrefix,
                ["KeepOriginalFileName"] = _config.KeepOriginalFileName,
                ["CreateZipArchive"] = _config.CreateZipArchive,
                ["DeleteOriginalAfterZip"] = _config.DeleteOriginalAfterZip,
                ["RandomSort"] = _config.RandomSort,
                ["HighQualityMode"] = _config.HighQualityMode,
                ["SkipFirstFiveSeconds"] = _config.SkipFirstFiveSeconds,
                ["OutputDirectory"] = _config.OutputDirectory,
                ["OutputEncoder"] = _config.OutputEncoder.ToString(),
                ["LastVideoFiles"] = _videoList.Select(v => v.FilePath).ToList()
            };

            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从设置文件中恢复上次添加的视频列表
    /// </summary>
    private async void LoadVideoListFromSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (settings == null || !settings.TryGetValue("LastVideoFiles", out var files) || files.ValueKind != JsonValueKind.Array)
                return;

            var fileList = files.Deserialize<List<string>>();
            if (fileList != null && fileList.Count > 0)
            {
                await AddFilesAsync(fileList.ToArray(), true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载视频列表失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 判断 JsonElement 是否为布尔类型
    /// </summary>
    /// <param name="element">要判断的 JsonElement</param>
    /// <returns>是否为布尔类型</returns>
    private static bool IsBooleanElement(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False;
    }

    #endregion

    #region 界面搭建

    /// <summary>
    /// 初始化主界面布局与控件
    /// </summary>
    private void SetupUI()
    {
        this.Text = "视频批量剪辑工具";
        this.Size = new Size(DefaultFormWidth, DefaultFormHeight);
        this.MinimumSize = new Size(MinFormWidth, MinFormHeight);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = BackgroundBaseColor;
        this.Font = UIFont;
        this.ForeColor = TextPrimaryColor;

        // 主布局：上中下四部分
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(Spacing12),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            BackColor = BackgroundBaseColor
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, OutputPathRowHeight)); // 输出路径行
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DropRowHeight));       // 拖拽区域
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));                  // 主内容（设置+列表）
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, LogRowHeight));        // 日志

        // 1. 输出路径行
        var pathRow = CreateOutputPathRow();
        mainLayout.Controls.Add(pathRow, 0, 0);

        // 2. 拖拽区域
        var dropRow = CreateDropRow();
        mainLayout.Controls.Add(dropRow, 0, 1);

        // 3. 主内容区：左边设置 + 右边列表
        var contentRow = CreateContentRow();
        mainLayout.Controls.Add(contentRow, 0, 2);

        // 4. 日志区
        var logRow = CreateLogRow();
        mainLayout.Controls.Add(logRow, 0, 3);

        this.Controls.Add(mainLayout);

        // 加载输出路径记忆
        if (_outputPathTextBox != null)
            _outputPathTextBox.Text = _config.OutputDirectory;

        // 同步输出编码器选中项与估算信息
        if (_encoderComboBox != null)
            _encoderComboBox.SelectedValue = _config.OutputEncoder;
        UpdateEstimate();

        _ = CheckFFmpegAsync();
    }

    /// <summary>
    /// 创建输出路径选择行
    /// </summary>
    /// <returns>输出路径行面板</returns>
    private Panel CreateOutputPathRow()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundSecondaryColor,
            Padding = new Padding(Spacing8),
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, 0, 0, Spacing12)
        };
        // 绘制底部半透明分隔线，形成卡片层次
        panel.Paint += OnCardPanelPaint;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = BackgroundSecondaryColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, OutputPathLabelWidth)); // 标签
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));                   // 路径输入框
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BrowseButtonWidth));    // 浏览按钮
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ClearButtonWidth));     // 清空按钮

        var lblPath = new Label
        {
            Text = "输出路径:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = UIFont,
            ForeColor = TextPrimaryColor,
            BackColor = Color.Transparent
        };

        _outputPathTextBox = new TextBox
        {
            PlaceholderText = "请选择输出目录...",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = UIFont,
            Margin = new Padding(Spacing8, 0, Spacing8, 0),
            BackColor = BackgroundTertiaryColor,
            ForeColor = TextPrimaryColor,
            BorderStyle = BorderStyle.None,
            Height = 34
        };
        // 为只读文本框绘制底部半透明下划线
        _outputPathTextBox.Paint += OnTextBoxBottomBorderPaint;

        var browseBtn = CreateSecondaryButton("浏览目录");
        browseBtn.Size = new Size(BrowseButtonWidth, BrowseButtonHeight);
        browseBtn.Click += OnBrowseButtonClick;

        var clearBtn = CreateSecondaryButton("清空列表");
        clearBtn.Size = new Size(ClearButtonWidth, ClearButtonHeight);
        clearBtn.Click += OnClearButtonClick;

        layout.Controls.Add(lblPath, 0, 0);
        layout.Controls.Add(_outputPathTextBox, 1, 0);
        layout.Controls.Add(browseBtn, 2, 0);
        layout.Controls.Add(clearBtn, 3, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    /// <summary>
    /// 创建文件拖放区域
    /// </summary>
    /// <returns>拖放区域面板</returns>
    private Panel CreateDropRow()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = BackgroundSecondaryColor,
            AllowDrop = true,
            Padding = new Padding(Spacing8),
            Margin = new Padding(0, 0, 0, Spacing12)
        };
        _dropPanel = panel;

        var iconLabel = new Label
        {
            Text = "📁",
            Dock = DockStyle.Top,
            Height = 34,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Emoji", 20F),
            ForeColor = BrandColor,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        var titleLabel = new Label
        {
            Text = "拖拽视频文件到此处",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = DropZoneTitleFont,
            ForeColor = TextPrimaryColor,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        var hintLabel = new Label
        {
            Text = "支持 MP4、AVI、MKV、MOV 等常见格式，或点击此处选择文件",
            Dock = DockStyle.Top,
            Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = UIFont,
            ForeColor = TextSecondaryColor,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        panel.Controls.Add(hintLabel);
        panel.Controls.Add(titleLabel);
        panel.Controls.Add(iconLabel);

        // 点击任意子控件均可选择文件
        panel.Click += OnDropPanelClick;
        iconLabel.Click += OnDropPanelClick;
        titleLabel.Click += OnDropPanelClick;
        hintLabel.Click += OnDropPanelClick;

        panel.DragEnter += OnDropPanelDragEnter;
        panel.DragLeave += OnDropPanelDragLeave;
        panel.DragDrop += OnDropPanelDragDrop;
        panel.Paint += OnDropPanelPaint;

        return panel;
    }

    /// <summary>
    /// 创建主内容区（设置面板 + 视频列表）
    /// </summary>
    /// <returns>主内容区面板</returns>
    private Panel CreateContentRow()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundBaseColor,
            Padding = new Padding(0, 0, 0, 0)
        };

        // 左边：设置面板
        var settingsPanel = CreateSettingsPanel();

        // 右边：视频列表
        var listPanel = CreateVideoListPanel();

        panel.Controls.Add(listPanel);
        panel.Controls.Add(settingsPanel);

        return panel;
    }

    /// <summary>
    /// 创建设置面板
    /// 外层 Panel 固定宽度并填充高度，内部嵌套可滚动 Panel 承载设置内容，
    /// 确保窗口高度不足时可以通过竖直滚动条访问全部设置项与按钮
    /// </summary>
    /// <returns>设置面板</returns>
    private Panel CreateSettingsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = SettingsPanelWidth,
            BackColor = BackgroundSecondaryColor,
            Padding = new Padding(Spacing12),
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, 0, Spacing12, 0)
        };
        // 绘制底部半透明分隔线
        panel.Paint += OnCardPanelPaint;

        // 滚动容器：填充外层 Panel，当内部 TableLayoutPanel 高度超出时自动显示竖直滚动条
        var scrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = BackgroundSecondaryColor,
            Padding = new Padding(0),
            BorderStyle = BorderStyle.None
        };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            RowCount = 13,
            ColumnCount = 2,
            Padding = new Padding(Spacing4),
            BackColor = BackgroundSecondaryColor
        };

        // 设置行高
        for (int i = 0; i < 7; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsRowHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsRowHeight)); // 去除前5秒
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsRowHeight)); // 高质量模式
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsRowHeight)); // 输出编码
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsRowHeight)); // 估算信息
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ButtonRowHeight));   // 开始按钮
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ButtonRowHeight));   // 取消按钮

        // 设置列宽
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelColumnWidth)); // 标签列
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));               // 控件列

        int row = 0;

        // 片段时长
        layout.Controls.Add(CreateLabel("片段时长:"), 0, row);
        _durationNumeric = CreateNumericUpDown(_config.SegmentDuration, 1, 300);
        _durationNumeric.ValueChanged += OnDurationNumericValueChanged;
        layout.Controls.Add(_durationNumeric, 1, row);
        row++;

        // 每视频段数
        layout.Controls.Add(CreateLabel("每视频段数:"), 0, row);
        _segmentsNumeric = CreateNumericUpDown(_config.SegmentsPerVideo, 1, 512);
        _segmentsNumeric.ValueChanged += OnSegmentsNumericValueChanged;
        layout.Controls.Add(_segmentsNumeric, 1, row);
        row++;

        // 文件名前缀
        layout.Controls.Add(CreateLabel("文件名前缀:"), 0, row);
        _prefixTextBox = new TextBox
        {
            PlaceholderText = "留空用原名",
            Dock = DockStyle.Fill,
            Font = UIFont,
            Text = _config.OutputPrefix,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, Spacing4, 0, Spacing4),
            BackColor = BackgroundTertiaryColor,
            ForeColor = TextPrimaryColor,
            Height = 32
        };
        _prefixTextBox.TextChanged += OnPrefixTextBoxTextChanged;
        _prefixTextBox.Paint += OnTextBoxBottomBorderPaint;
        layout.Controls.Add(_prefixTextBox, 1, row);
        row++;

        // 保留原文件名
        _keepNameCheckBox = CreateCheckBox("保留原文件名", _config.KeepOriginalFileName, OnKeepNameCheckBoxCheckedChanged);
        layout.Controls.Add(_keepNameCheckBox, 0, row);
        layout.SetColumnSpan(_keepNameCheckBox, 2);
        row++;

        // 打包ZIP
        _zipCheckBox = CreateCheckBox("打包ZIP", _config.CreateZipArchive, OnZipCheckBoxCheckedChanged);
        layout.Controls.Add(_zipCheckBox, 0, row);
        layout.SetColumnSpan(_zipCheckBox, 2);
        row++;

        // 打包后删除原文件
        _deleteOriginalCheckBox = CreateCheckBox("打包后删除原文件", _config.DeleteOriginalAfterZip, OnDeleteOriginalCheckBoxCheckedChanged);
        _deleteOriginalCheckBox.Enabled = _config.CreateZipArchive;
        layout.Controls.Add(_deleteOriginalCheckBox, 0, row);
        layout.SetColumnSpan(_deleteOriginalCheckBox, 2);
        row++;

        // 随机排序
        _randomSortCheckBox = CreateCheckBox("随机排序（数字放开头）", _config.RandomSort, OnRandomSortCheckBoxCheckedChanged);
        layout.Controls.Add(_randomSortCheckBox, 0, row);
        layout.SetColumnSpan(_randomSortCheckBox, 2);
        row++;

        // 去除前5秒
        _skipFirstFiveSecondsCheckBox = CreateCheckBox("去除前5秒（片段从第5秒后开始）", _config.SkipFirstFiveSeconds, OnSkipFirstFiveSecondsCheckBoxCheckedChanged);
        layout.Controls.Add(_skipFirstFiveSecondsCheckBox, 0, row);
        layout.SetColumnSpan(_skipFirstFiveSecondsCheckBox, 2);
        row++;

        // 高质量模式
        _highQualityCheckBox = CreateCheckBox("高质量模式（重新编码，避免卡顿）", _config.HighQualityMode, OnHighQualityCheckBoxCheckedChanged);
        layout.Controls.Add(_highQualityCheckBox, 0, row);
        layout.SetColumnSpan(_highQualityCheckBox, 2);
        row++;

        // 输出编码
        layout.Controls.Add(CreateLabel("输出编码："), 0, row);
        _encoderComboBox = CreateEncoderComboBox();
        _encoderComboBox.SelectedIndexChanged += OnEncoderComboBoxSelectedIndexChanged;
        layout.Controls.Add(_encoderComboBox, 1, row);
        row++;

        // 估算信息
        _estimateLabel = new Label
        {
            Text = "请先添加视频文件",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = UIFontBold,
            ForeColor = TextPrimaryColor,
            BackColor = BackgroundSecondaryColor,
            Margin = new Padding(0, Spacing4, 0, 0)
        };
        layout.Controls.Add(_estimateLabel, 0, row);
        layout.SetColumnSpan(_estimateLabel, 2);
        row++;

        // 开始按钮：固定高度，不随父容器竖直拉伸而变大
        _startButton = CreatePrimaryButton("开始处理");
        _startButton.Dock = DockStyle.Top;
        _startButton.Height = ButtonRowHeight;
        _startButton.Click += OnStartButtonClick;
        layout.Controls.Add(_startButton, 0, row);
        layout.SetColumnSpan(_startButton, 2);
        row++;

        // 取消按钮：固定高度，不随父容器竖直拉伸而变大
        _cancelButton = CreateSecondaryButton("取消");
        _cancelButton.Dock = DockStyle.Top;
        _cancelButton.Height = ButtonRowHeight;
        _cancelButton.Enabled = false;
        _cancelButton.Click += OnCancelButtonClick;
        layout.Controls.Add(_cancelButton, 0, row);
        layout.SetColumnSpan(_cancelButton, 2);

        // 将设置布局放入滚动容器，再将滚动容器放入外层面板
        scrollPanel.Controls.Add(layout);
        panel.Controls.Add(scrollPanel);
        return panel;
    }

    /// <summary>
    /// 创建视频列表面板
    /// </summary>
    /// <returns>视频列表面板</returns>
    private Panel CreateVideoListPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundSecondaryColor,
            Padding = new Padding(Spacing8),
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0)
        };
        panel.Paint += OnCardPanelPaint;

        _dataGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = true,
            ReadOnly = true,
            BackgroundColor = BackgroundSecondaryColor,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            ColumnHeadersHeight = 36,
            BorderStyle = BorderStyle.None,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            Font = UIFont,
            Margin = new Padding(0),
            EnableHeadersVisualStyles = false,
            GridColor = GridLineColor,
            ForeColor = TextPrimaryColor
        };
        _dataGridView.RowTemplate.Height = 32;
        _dataGridView.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = BackgroundAlternateRowColor,
            ForeColor = TextPrimaryColor,
            SelectionBackColor = BrandTransparentColor,
            SelectionForeColor = TextPrimaryColor
        };
        _dataGridView.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = BackgroundSecondaryColor,
            ForeColor = TextPrimaryColor,
            Font = UIFontBold,
            Alignment = DataGridViewContentAlignment.MiddleCenter,
            SelectionBackColor = BackgroundSecondaryColor
        };
        _dataGridView.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = BackgroundBaseColor,
            ForeColor = TextPrimaryColor,
            SelectionBackColor = BrandTransparentColor,
            SelectionForeColor = TextPrimaryColor,
            Alignment = DataGridViewContentAlignment.MiddleCenter
        };

        // 添加列
        _dataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "FileName",
            DataPropertyName = "FileNameWithoutExtension",
            HeaderText = "文件名",
            Width = 250,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                ForeColor = TextPrimaryColor,
                SelectionBackColor = BrandTransparentColor,
                SelectionForeColor = TextPrimaryColor
            }
        });
        _dataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Duration",
            DataPropertyName = "DurationText",
            HeaderText = "时长",
            Width = 80
        });
        _dataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Size",
            DataPropertyName = "FileSizeText",
            HeaderText = "大小",
            Width = 90
        });
        _dataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Status",
            DataPropertyName = "StatusText",
            HeaderText = "状态",
            Width = 80
        });
        _dataGridView.Columns.Add(new DataGridViewProgressBarColumn
        {
            Name = "Progress",
            DataPropertyName = "Progress",
            HeaderText = "进度",
            Width = 120
        });

        _dataGridView.DataSource = _videoList;
        _videoList.ListChanged += (s, e) =>
        {
            SaveSettings();
            UpdateEstimate();
        };

        panel.Controls.Add(_dataGridView);
        return panel;
    }

    /// <summary>
    /// 创建日志与进度区域
    /// </summary>
    /// <returns>日志区域面板</returns>
    private Panel CreateLogRow()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BackgroundSecondaryColor,
            Padding = new Padding(Spacing8),
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0, Spacing12, 0, 0)
        };
        panel.Paint += OnCardPanelPaint;

        _progressLabel = new Label
        {
            Text = "总体进度: 0%",
            Dock = DockStyle.Top,
            Height = 22,
            Font = UIFontBold,
            ForeColor = TextPrimaryColor,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, Spacing4)
        };

        _overallProgressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 18,
            Maximum = 100,
            Margin = new Padding(0, 0, 0, Spacing8)
        };

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = BackgroundBaseColor,
            ForeColor = TextSecondaryColor,
            Font = LogFont,
            BorderStyle = BorderStyle.None,
            Margin = new Padding(0)
        };
        // 日志时间戳颜色通过文本中嵌入 RTF 或分段绘制较复杂，
        // 这里通过 AppendLog 在文本中保持 [时间戳] 前缀，并用更暗的颜色无法直接实现；
        // 因此采用在日志区下方叠加一个只读 RichTextBox 的想法改为保留 TextBox，
        // 但用括号颜色在 TextBox 中不可分色。后续可考虑将 _logTextBox 类型改为 RichTextBox 实现分色。
        // 为遵守“保持原有布局结构与交互逻辑不变”的要求，仍使用 TextBox，时间戳颜色使用 TextSecondaryColor。

        panel.Controls.Add(_logTextBox);
        panel.Controls.Add(_overallProgressBar);
        panel.Controls.Add(_progressLabel);

        return panel;
    }

    #endregion

    #region 控件创建辅助方法

    /// <summary>
    /// 创建 TRAE 主按钮（品牌色背景 + 深色文字）
    /// </summary>
    /// <param name="text">按钮文本</param>
    /// <returns>样式化主按钮</returns>
    private Button CreatePrimaryButton(string text)
    {
        var btn = new StyledButton(
            text,
            BrandColor,
            BrandHoverColor,
            BrandPressedColor,
            TextOnBrandColor,
            BorderWhite8);
        return btn;
    }

    /// <summary>
    /// 创建 TRAE 次要按钮（深灰背景 + 半透明白色边框）
    /// </summary>
    /// <param name="text">按钮文本</param>
    /// <returns>样式化次要按钮</returns>
    private Button CreateSecondaryButton(string text)
    {
        var btn = new StyledButton(
            text,
            BackgroundTertiaryColor,
            BackgroundSecondaryColor,
            BackgroundTertiaryColor,
            TextPrimaryColor,
            BorderWhite8);
        return btn;
    }

    /// <summary>
    /// 创建标签控件
    /// </summary>
    /// <param name="text">标签文本</param>
    /// <returns>标签控件</returns>
    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = UIFont,
            ForeColor = TextPrimaryColor,
            BackColor = Color.Transparent,
            Margin = new Padding(0, Spacing4, 0, 0)
        };
    }

    /// <summary>
    /// 创建数值选择控件
    /// </summary>
    /// <param name="value">初始值</param>
    /// <param name="min">最小值</param>
    /// <param name="max">最大值</param>
    /// <returns>数值选择控件</returns>
    private NumericUpDown CreateNumericUpDown(int value, int min, int max)
    {
        var numeric = new NumericUpDown
        {
            Value = value,
            Minimum = min,
            Maximum = max,
            Dock = DockStyle.Fill,
            Font = UIFont,
            Margin = new Padding(0, Spacing4, 0, Spacing4),
            BorderStyle = BorderStyle.None,
            BackColor = BackgroundTertiaryColor,
            ForeColor = TextPrimaryColor,
            Height = 32
        };
        // NumericUpDown 由文本子控件和按钮子控件组成，直接修改子控件颜色
        foreach (Control child in numeric.Controls)
        {
            child.BackColor = BackgroundTertiaryColor;
            child.ForeColor = TextPrimaryColor;
        }
        numeric.Paint += OnNumericUpDownPaint;
        return numeric;
    }

    /// <summary>
    /// 创建复选框控件
    /// </summary>
    /// <param name="text">复选框文本</param>
    /// <param name="isChecked">是否默认选中</param>
    /// <param name="checkedChanged">选中状态变化事件处理器</param>
    /// <returns>复选框控件</returns>
    private CheckBox CreateCheckBox(string text, bool isChecked, EventHandler checkedChanged)
    {
        var checkBox = new CheckBox
        {
            Text = text,
            Checked = isChecked,
            Font = UIFont,
            Margin = new Padding(0, Spacing4, 0, 0),
            AutoSize = true,
            ForeColor = TextPrimaryColor,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat
        };
        checkBox.FlatAppearance.BorderColor = BorderWhite8;
        checkBox.FlatAppearance.CheckedBackColor = BrandColor;
        checkBox.FlatAppearance.MouseOverBackColor = BackgroundTertiaryColor;
        checkBox.FlatAppearance.MouseDownBackColor = BrandPressedColor;
        checkBox.CheckedChanged += checkedChanged;
        checkBox.EnabledChanged += OnCheckBoxEnabledChanged;
        return checkBox;
    }

    /// <summary>
    /// 创建输出编码器下拉框控件
    /// 使用 { Value, Display } 匿名对象列表作为数据源，实现枚举值与中文显示名称的绑定
    /// </summary>
    /// <returns>样式化下拉框控件</returns>
    private ComboBox CreateEncoderComboBox()
    {
        var comboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            Font = UIFont,
            Margin = new Padding(0, Spacing4, 0, Spacing4),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = BackgroundTertiaryColor,
            ForeColor = TextPrimaryColor,
            FlatStyle = FlatStyle.Flat
        };

        var encoderItems = Enum.GetValues<OutputEncoder>()
            .Select(e => new { Value = e, Display = GetEncoderDisplayName(e) })
            .ToList();
        comboBox.DataSource = encoderItems;
        comboBox.DisplayMember = "Display";
        comboBox.ValueMember = "Value";

        return comboBox;
    }

    /// <summary>
    /// 获取编码器在 UI 中显示的中文名称
    /// </summary>
    /// <param name="encoder">编码器枚举值</param>
    /// <returns>中文显示名称</returns>
    private static string GetEncoderDisplayName(OutputEncoder encoder)
    {
        return encoder switch
        {
            OutputEncoder.CpuH264 => "CPU H.264",
            OutputEncoder.CpuH265 => "CPU H.265",
            OutputEncoder.NvidiaH264 => "NVIDIA NVENC H.264",
            OutputEncoder.NvidiaH265 => "NVIDIA NVENC H.265",
            OutputEncoder.IntelH264 => "Intel QSV H.264",
            OutputEncoder.IntelH265 => "Intel QSV H.265",
            OutputEncoder.AmdH264 => "AMD AMF H.264",
            OutputEncoder.AmdH265 => "AMD AMF H.265",
            _ => encoder.ToString()
        };
    }

    #endregion

    #region 估算辅助方法

    /// <summary>
    /// 根据当前配置和视频列表更新估算信息
    /// 触发时机：视频列表变化、片段时长变化、每视频段数变化、输出编码器变化、程序启动加载设置后
    /// </summary>
    private void UpdateEstimate()
    {
        int videoCount = _videoList.Count;

        if (videoCount == 0)
        {
            if (_estimateLabel != null)
                _estimateLabel.Text = "请先添加视频文件";
            return;
        }

        int totalSegments = videoCount * _config.SegmentsPerVideo;
        int totalSeconds = totalSegments * _config.SegmentDuration;
        double mbPerMinute = GetEncoderBitrateFactor(_config.OutputEncoder);
        double estimatedSizeMb = (totalSeconds / 60.0) * mbPerMinute;

        string durationText = FormatDuration(totalSeconds);
        string sizeText = FormatFileSize(estimatedSizeMb);

        if (_estimateLabel != null)
            _estimateLabel.Text = $"预计生成 {totalSegments} 个片段，总时长约 {durationText}，占用空间约 {sizeText}";
    }

    /// <summary>
    /// 获取编码器估算码率系数（MB/分钟）
    /// 用于根据总时长快速估算输出文件占用的磁盘空间
    /// </summary>
    /// <param name="encoder">输出编码器</param>
    /// <returns>估算码率系数</returns>
    private static double GetEncoderBitrateFactor(OutputEncoder encoder)
    {
        return encoder switch
        {
            OutputEncoder.CpuH264 => 5.0,
            OutputEncoder.CpuH265 => 3.0,
            OutputEncoder.NvidiaH264 => 3.5,
            OutputEncoder.NvidiaH265 => 2.5,
            OutputEncoder.IntelH264 => 3.5,
            OutputEncoder.IntelH265 => 2.5,
            OutputEncoder.AmdH264 => 3.5,
            OutputEncoder.AmdH265 => 2.5,
            _ => 5.0
        };
    }

    /// <summary>
    /// 将秒数格式化为“X 时 Y 分 Z 秒”或“Y 分 Z 秒”
    /// 当小时为 0 时省略小时部分，保持输出简洁
    /// </summary>
    /// <param name="totalSeconds">总秒数</param>
    /// <returns>人类可读的时长字符串</returns>
    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds <= 0)
            return "0 秒";

        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
            return $"{hours} 时 {minutes} 分 {seconds:D2} 秒";

        return $"{minutes} 分 {seconds:D2} 秒";
    }

    /// <summary>
    /// 将 MB 格式化为人类可读的大小（MB/GB）
    /// 当大小达到 1024 MB 时转换为 GB 显示
    /// </summary>
    /// <param name="sizeMb">大小（MB）</param>
    /// <returns>人类可读的大小字符串</returns>
    private static string FormatFileSize(double sizeMb)
    {
        if (sizeMb < 1024)
            return $"{sizeMb:0} MB";

        double sizeGb = sizeMb / 1024;
        return $"{sizeGb:0.0} GB";
    }

    #endregion

    #region 自定义绘制辅助方法

    /// <summary>
    /// 绘制卡片面板底部半透明分隔线，替代 FixedSingle 实线边框，营造层次
    /// </summary>
    /// <param name="sender">事件源面板</param>
    /// <param name="e">绘制参数</param>
    private void OnCardPanelPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel) return;

        var rect = new Rectangle(0, panel.ClientSize.Height - 1, panel.ClientSize.Width, 1);
        using var brush = new SolidBrush(BorderWhite6);
        e.Graphics.FillRectangle(brush, rect);
    }

    /// <summary>
    /// 绘制输入控件底部半透明下划线，作为无 BorderStyle 状态下的边框替代
    /// </summary>
    /// <param name="sender">事件源控件</param>
    /// <param name="e">绘制参数</param>
    private void OnTextBoxBottomBorderPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not Control control) return;

        var rect = new Rectangle(0, control.ClientSize.Height - 1, control.ClientSize.Width, 1);
        using var brush = new SolidBrush(control.Enabled ? BorderWhite8 : BorderWhite4);
        e.Graphics.FillRectangle(brush, rect);
    }

    /// <summary>
    /// 绘制 NumericUpDown 外框：底部下划线 + 右侧按钮区域竖线
    /// </summary>
    /// <param name="sender">事件源控件</param>
    /// <param name="e">绘制参数</param>
    private void OnNumericUpDownPaint(object? sender, PaintEventArgs e)
    {
        if (sender is not NumericUpDown numeric) return;

        // 底部下划线
        using var bottomBrush = new SolidBrush(numeric.Enabled ? BorderWhite8 : BorderWhite4);
        e.Graphics.FillRectangle(bottomBrush, new Rectangle(0, numeric.ClientSize.Height - 1, numeric.ClientSize.Width, 1));

        // 右侧按钮区域左侧竖线
        int buttonAreaLeft = numeric.ClientSize.Width - numeric.UpDownAlign switch
        {
            LeftRightAlignment.Left => 0,
            _ => 17
        };
        // 简单画一条竖线作为按钮区与文本区分隔
        e.Graphics.FillRectangle(bottomBrush, new Rectangle(buttonAreaLeft, 0, 1, numeric.ClientSize.Height - 1));
    }

    /// <summary>
    /// 复选框启用状态变化时同步文字颜色（禁用态使用 TextDisabledColor）
    /// </summary>
    /// <param name="sender">事件源复选框</param>
    /// <param name="e">事件参数</param>
    private void OnCheckBoxEnabledChanged(object? sender, EventArgs e)
    {
        if (sender is CheckBox checkBox)
            checkBox.ForeColor = checkBox.Enabled ? TextPrimaryColor : TextDisabledColor;
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 点击浏览按钮：选择输出目录
    /// </summary>
    private void OnBrowseButtonClick(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _config.OutputDirectory };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _config.OutputDirectory = dialog.SelectedPath;
            if (_outputPathTextBox != null)
                _outputPathTextBox.Text = _config.OutputDirectory;
            AppendLog($"输出目录已设置: {_config.OutputDirectory}");
            SaveSettings();
        }
    }

    /// <summary>
    /// 点击清空按钮：清空视频列表
    /// </summary>
    private void OnClearButtonClick(object? sender, EventArgs e)
    {
        _videoList.Clear();
        UpdateEstimate();
        AppendLog("已清空列表");
        SaveSettings();
    }

    /// <summary>
    /// 点击拖放区：打开文件选择对话框
    /// </summary>
    private async void OnDropPanelClick(object? sender, EventArgs e)
    {
        await SelectFilesAsync();
    }

    /// <summary>
    /// 文件拖入拖放区
    /// </summary>
    private void OnDropPanelDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
            if (_dropPanel != null)
                _dropPanel.BackColor = BackgroundTertiaryColor;
        }
    }

    /// <summary>
    /// 文件离开拖放区
    /// </summary>
    private void OnDropPanelDragLeave(object? sender, EventArgs e)
    {
        if (_dropPanel != null)
            _dropPanel.BackColor = BackgroundSecondaryColor;
    }

    /// <summary>
    /// 文件拖放完成
    /// </summary>
    private async void OnDropPanelDragDrop(object? sender, DragEventArgs e)
    {
        if (_dropPanel != null)
            _dropPanel.BackColor = BackgroundSecondaryColor;

        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
            await AddFilesAsync(files, false);
    }

    /// <summary>
    /// 绘制拖放区虚线边框
    /// </summary>
    private void OnDropPanelPaint(object? sender, PaintEventArgs e)
    {
        if (_dropPanel == null) return;

        var rect = new Rectangle(
            Spacing4,
            Spacing4,
            _dropPanel.ClientSize.Width - Spacing8,
            _dropPanel.ClientSize.Height - Spacing8);

        using var pen = new Pen(BrandBorderTransparentColor, 2)
        {
            DashStyle = DashStyle.Dash
        };
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawRectangle(pen, rect);
    }

    /// <summary>
    /// 片段时长变化
    /// </summary>
    private void OnDurationNumericValueChanged(object? sender, EventArgs e)
    {
        if (_durationNumeric != null)
            _config.SegmentDuration = (int)_durationNumeric.Value;
        UpdateEstimate();
        SaveSettings();
    }

    /// <summary>
    /// 每视频段数变化
    /// </summary>
    private void OnSegmentsNumericValueChanged(object? sender, EventArgs e)
    {
        if (_segmentsNumeric != null)
            _config.SegmentsPerVideo = (int)_segmentsNumeric.Value;
        UpdateEstimate();
        SaveSettings();
    }

    /// <summary>
    /// 文件名前缀变化
    /// </summary>
    private void OnPrefixTextBoxTextChanged(object? sender, EventArgs e)
    {
        if (_prefixTextBox != null)
            _config.OutputPrefix = _prefixTextBox.Text;
    }

    /// <summary>
    /// 保留原文件名选项变化
    /// </summary>
    private void OnKeepNameCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        if (_keepNameCheckBox != null)
            _config.KeepOriginalFileName = _keepNameCheckBox.Checked;
    }

    /// <summary>
    /// 打包ZIP选项变化
    /// </summary>
    private void OnZipCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        if (_zipCheckBox == null)
            return;

        _config.CreateZipArchive = _zipCheckBox.Checked;
        UpdateDeleteOriginalCheckBoxState();
    }

    /// <summary>
    /// 打包后删除原文件选项变化
    /// </summary>
    private void OnDeleteOriginalCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        if (_deleteOriginalCheckBox != null)
            _config.DeleteOriginalAfterZip = _deleteOriginalCheckBox.Checked;
    }

    /// <summary>
    /// 随机排序选项变化
    /// </summary>
    private void OnRandomSortCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        if (_randomSortCheckBox != null)
            _config.RandomSort = _randomSortCheckBox.Checked;
    }

    /// <summary>
    /// 去除前5秒选项变化
    /// </summary>
    private void OnSkipFirstFiveSecondsCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        if (_skipFirstFiveSecondsCheckBox != null)
            _config.SkipFirstFiveSeconds = _skipFirstFiveSecondsCheckBox.Checked;
    }

    /// <summary>
    /// 高质量模式选项变化
    /// </summary>
    private void OnHighQualityCheckBoxCheckedChanged(object? sender, EventArgs e)
    {
        if (_highQualityCheckBox != null)
            _config.HighQualityMode = _highQualityCheckBox.Checked;
    }

    /// <summary>
    /// 输出编码器选项变化
    /// </summary>
    private void OnEncoderComboBoxSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_encoderComboBox?.SelectedValue is OutputEncoder selectedEncoder)
        {
            _config.OutputEncoder = selectedEncoder;
            UpdateEstimate();
            SaveSettings();
        }
    }

    /// <summary>
    /// 更新"打包后删除原文件"复选框的可用状态
    /// </summary>
    private void UpdateDeleteOriginalCheckBoxState()
    {
        if (_deleteOriginalCheckBox == null || _zipCheckBox == null)
            return;

        _deleteOriginalCheckBox.Enabled = _zipCheckBox.Checked;
        if (!_zipCheckBox.Checked)
        {
            _deleteOriginalCheckBox.Checked = false;
            _config.DeleteOriginalAfterZip = false;
        }
    }

    /// <summary>
    /// 点击开始处理按钮
    /// </summary>
    private async void OnStartButtonClick(object? sender, EventArgs e)
    {
        await StartProcessingAsync();
    }

    /// <summary>
    /// 点击取消按钮
    /// </summary>
    private void OnCancelButtonClick(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    /// 处理器进度变化事件
    /// </summary>
    private void OnProcessorProgressChanged(object? sender, ProcessingProgress progress) => UpdateProgressUI(progress);

    /// <summary>
    /// 处理器日志消息事件
    /// </summary>
    private void OnProcessorLogMessage(object? sender, string message) => AppendLog(message);

    /// <summary>
    /// 处理器处理完成事件
    /// </summary>
    private void OnProcessorProcessingCompleted(object? sender, ProcessingResult result) => AppendLog($"处理完成！成功: {result.SuccessfulSegments}, 失败: {result.FailedSegments}");

    /// <summary>
    /// 窗口关闭时保存设置
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        base.OnFormClosing(e);
    }

    #endregion

    #region 业务逻辑

    /// <summary>
    /// 异步检查 FFmpeg 环境
    /// </summary>
    private async Task CheckFFmpegAsync()
    {
        try
        {
            bool isValid = await _processor.ValidateEnvironmentAsync();
            AppendLog(isValid ? "✓ FFmpeg 环境正常" : "⚠ 未检测到 FFmpeg 环境");
        }
        catch
        {
            AppendLog("⚠ FFmpeg 环境检查失败");
        }
    }

    /// <summary>
    /// 打开文件选择对话框并添加视频文件
    /// </summary>
    private async Task SelectFilesAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "视频文件|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.m4v;*.mpg;*.mpeg"
        };
        if (dialog.ShowDialog() == DialogResult.OK)
            await AddFilesAsync(dialog.FileNames, false);
    }

    /// <summary>
    /// 添加视频文件到列表
    /// </summary>
    /// <param name="filePaths">文件路径数组</param>
    /// <param name="isLoadingFromSettings">是否从设置文件加载</param>
    private async Task AddFilesAsync(string[] filePaths, bool isLoadingFromSettings)
    {
        var validExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpg", ".mpeg" };
        var videoFiles = filePaths.Where(f => validExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();

        if (videoFiles.Count == 0)
        {
            if (!isLoadingFromSettings)
                MessageBox.Show("未找到有效的视频文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // 重复文件检测
        var duplicates = videoFiles.Where(f => _videoList.Any(v => v.FilePath == f)).ToList();
        var newFiles = videoFiles.Where(f => !_videoList.Any(v => v.FilePath == f)).ToList();

        if (duplicates.Count > 0 && !isLoadingFromSettings)
        {
            var result = MessageBox.Show($"发现 {duplicates.Count} 个重复文件，是否跳过？", "重复文件", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
            {
                AppendLog($"跳过 {duplicates.Count} 个重复文件");
            }
            else
            {
                foreach (var dup in duplicates)
                {
                    var existing = _videoList.First(v => v.FilePath == dup);
                    _videoList.Remove(existing);
                    newFiles.Add(dup);
                }
            }
        }

        if (newFiles.Count == 0) return;

        AppendLog($"正在分析 {newFiles.Count} 个视频文件...");
        foreach (var filePath in newFiles)
        {
            var videoInfo = await _processor.GetVideoInfoAsync(filePath);
            if (videoInfo != null)
            {
                _videoList.Add(videoInfo);
                AppendLog($"已添加: {videoInfo.FileNameWithoutExtension}");
            }
        }

        AppendLog($"当前共 {_videoList.Count} 个视频待处理");
        UpdateEstimate();
        SaveSettings();
    }

    /// <summary>
    /// 开始批量处理视频
    /// </summary>
    private async Task StartProcessingAsync()
    {
        if (_videoList.Count == 0)
        {
            MessageBox.Show("请先添加视频文件", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (string.IsNullOrEmpty(_config.OutputDirectory))
        {
            MessageBox.Show("请先选择输出目录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var (isValid, errorMessage) = _config.Validate();
        if (!isValid)
        {
            MessageBox.Show(errorMessage ?? "配置错误", "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveSettings();
        SetProcessingState(true);
        _cancellationTokenSource = new CancellationTokenSource();

        AppendLog($"========== 开始处理 ==========");
        AppendLog($"输出目录: {_config.OutputDirectory}");

        try
        {
            var progress = new Progress<ProcessingProgress>(UpdateProgressUI);
            var result = await _processor.ProcessAsync(_videoList.ToList(), _config, progress, _cancellationTokenSource.Token);

            if (result.IsSuccess && !string.IsNullOrEmpty(result.ZipFilePath))
            {
                AppendLog($"ZIP文件已保存: {result.ZipFilePath}");
                var openResult = MessageBox.Show($"处理完成！\n成功: {result.SuccessfulSegments} 个片段\n失败: {result.FailedSegments} 个片段\n\n是否打开输出目录？", "完成", MessageBoxButtons.YesNo);
                if (openResult == DialogResult.Yes)
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{result.ZipFilePath}\"");
            }
            else if (result.IsSuccess)
            {
                AppendLog($"片段已保存到: {_config.OutputDirectory}");
                MessageBox.Show($"处理完成！\n成功: {result.SuccessfulSegments} 个片段\n失败: {result.FailedSegments} 个片段", "完成", MessageBoxButtons.OK);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("处理已取消");
            MessageBox.Show("处理已取消", "取消", MessageBoxButtons.OK);
        }
        catch (Exception ex)
        {
            AppendLog($"处理失败: {ex.Message}");
            MessageBox.Show($"处理失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetProcessingState(false);
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// 更新进度界面
    /// 根据处理器上报的进度同步总体进度条、日志标签以及当前视频行的进度与状态
    /// </summary>
    /// <param name="progress">处理进度</param>
    private void UpdateProgressUI(ProcessingProgress progress)
    {
        if (InvokeRequired) { Invoke(() => UpdateProgressUI(progress)); return; }

        if (_overallProgressBar != null)
            _overallProgressBar.Value = Math.Min(progress.OverallProgress, 100);

        if (_progressLabel != null)
            _progressLabel.Text = $"总体进度: {progress.OverallProgress}% - {progress.CurrentOperation}";

        if (progress.CurrentFileIndex > 0 && progress.CurrentFileIndex <= _videoList.Count)
        {
            var video = _videoList[progress.CurrentFileIndex - 1];
            video.Progress = progress.CurrentFileProgress;

            // 在 INotifyPropertyChanged 的基础上，对当前行进度列与状态列做显式重绘，
            // 确保自定义进度条列和状态文本能够流畅刷新
            int rowIndex = progress.CurrentFileIndex - 1;
            if (_dataGridView != null && rowIndex >= 0 && rowIndex < _dataGridView.RowCount)
            {
                var progressColumn = _dataGridView.Columns["Progress"];
                var statusColumn = _dataGridView.Columns["Status"];
                if (progressColumn != null)
                    _dataGridView.InvalidateCell(progressColumn.Index, rowIndex);
                if (statusColumn != null)
                    _dataGridView.InvalidateCell(statusColumn.Index, rowIndex);
            }
        }
    }

    /// <summary>
    /// 设置处理中状态，锁定或解锁界面控件
    /// </summary>
    /// <param name="isProcessing">是否正在处理</param>
    private void SetProcessingState(bool isProcessing)
    {
        if (InvokeRequired) { Invoke(() => SetProcessingState(isProcessing)); return; }

        if (_startButton != null) _startButton.Enabled = !isProcessing;
        if (_cancelButton != null) _cancelButton.Enabled = isProcessing;
        if (_durationNumeric != null) _durationNumeric.Enabled = !isProcessing;
        if (_segmentsNumeric != null) _segmentsNumeric.Enabled = !isProcessing;
        if (_prefixTextBox != null) _prefixTextBox.Enabled = !isProcessing;
        if (_keepNameCheckBox != null) _keepNameCheckBox.Enabled = !isProcessing;
        if (_zipCheckBox != null) _zipCheckBox.Enabled = !isProcessing;
        if (_deleteOriginalCheckBox != null)
            _deleteOriginalCheckBox.Enabled = !isProcessing && (_zipCheckBox?.Checked ?? false);
        if (_randomSortCheckBox != null) _randomSortCheckBox.Enabled = !isProcessing;
        if (_skipFirstFiveSecondsCheckBox != null) _skipFirstFiveSecondsCheckBox.Enabled = !isProcessing;
        if (_highQualityCheckBox != null) _highQualityCheckBox.Enabled = !isProcessing;
        if (_encoderComboBox != null) _encoderComboBox.Enabled = !isProcessing;
    }

    /// <summary>
    /// 追加日志到日志文本框
    /// </summary>
    /// <param name="message">日志消息</param>
    private void AppendLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AppendLog(message)); return; }

        if (_logTextBox != null)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _logTextBox.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
            _logTextBox.ScrollToCaret();
        }
    }

    #endregion

    #region 样式化按钮

    /// <summary>
    /// 带悬停/按下颜色反馈、圆角和半透明边框的按钮
    /// </summary>
    private class StyledButton : Button
    {
        private readonly Color _normalColor;
        private readonly Color _hoverColor;
        private readonly Color _pressedColor;
        private readonly Color _textColor;
        private readonly Color _borderColor;
        private bool _isHover;
        private bool _isPressed;

        /// <summary>
        /// 初始化样式化按钮
        /// </summary>
        /// <param name="text">按钮文本</param>
        /// <param name="normalColor">正常状态颜色</param>
        /// <param name="hoverColor">悬停状态颜色</param>
        /// <param name="pressedColor">按下状态颜色</param>
        /// <param name="textColor">文本颜色</param>
        /// <param name="borderColor">边框颜色</param>
        public StyledButton(string text, Color normalColor, Color hoverColor, Color pressedColor, Color textColor, Color borderColor)
        {
            _normalColor = normalColor;
            _hoverColor = hoverColor;
            _pressedColor = pressedColor;
            _textColor = textColor;
            _borderColor = borderColor;

            Text = text;
            BackColor = normalColor;
            ForeColor = textColor;
            FlatStyle = FlatStyle.Flat;
            Font = UIFont;
            Cursor = Cursors.Hand;
            FlatAppearance.BorderSize = 0;

            MouseEnter += (s, e) => { _isHover = true; Invalidate(); };
            MouseLeave += (s, e) => { _isHover = false; _isPressed = false; Invalidate(); };
            MouseDown += (s, e) => { _isPressed = true; Invalidate(); };
            MouseUp += (s, e) => { _isPressed = false; Invalidate(); };
        }

        /// <summary>
        /// 重写 OnPaint 实现圆角按钮绘制
        /// </summary>
        /// <param name="pevent">绘制事件参数</param>
        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // 清除默认背景
            pevent.Graphics.Clear(Parent?.BackColor ?? BackgroundBaseColor);

            // 确定当前背景色
            Color currentColor = _isPressed ? _pressedColor : (_isHover ? _hoverColor : _normalColor);

            // 绘制圆角背景
            using var path = GetRoundedRectanglePath(ClientRectangle, CornerRadiusSmall);
            using var brush = new SolidBrush(currentColor);
            pevent.Graphics.FillPath(brush, path);

            // 绘制边框
            using var pen = new Pen(_borderColor, 1);
            pevent.Graphics.DrawPath(pen, path);

            // 绘制文本
            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                ClientRectangle,
                Enabled ? _textColor : TextDisabledColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);
        }

        /// <summary>
        /// 生成圆角矩形路径
        /// </summary>
        /// <param name="bounds">外接矩形</param>
        /// <param name="radius">圆角半径</param>
        /// <returns>圆角矩形路径</returns>
        private static GraphicsPath GetRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();

            path.StartFigure();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }

    #endregion
}
