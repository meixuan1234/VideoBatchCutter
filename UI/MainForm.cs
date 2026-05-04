using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Text.Json;
using VideoBatchCutter.Models;
using VideoBatchCutter.Services;

namespace VideoBatchCutter.UI;

public class MainForm : Form
{
    private readonly VideoProcessor _processor;
    private readonly BindingList<VideoFileInfo> _videoList;
    private readonly ProcessingConfig _config;
    private CancellationTokenSource? _cancellationTokenSource;

    // 控件声明 - 使用可空类型
    private NumericUpDown? _durationNumeric;
    private NumericUpDown? _segmentsNumeric;
    private TextBox? _prefixTextBox;
    private CheckBox? _keepNameCheckBox;
    private CheckBox? _zipCheckBox;
    private CheckBox? _deleteOriginalCheckBox;
    private CheckBox? _randomSortCheckBox;
    private TextBox? _outputPathTextBox;
    private ProgressBar? _overallProgressBar;
    private TextBox? _logTextBox;
    private DataGridView? _dataGridView;
    private Button? _startButton;
    private Button? _cancelButton;
    private Label? _progressLabel;

    // 常量定义
    private const int SettingsPanelWidth = 520; // 进一步加宽设置面板
    private const int OutputPathRowHeight = 55;
    private const int DropRowHeight = 50;
    private const int LogRowHeight = 180;
    private const int DefaultFormWidth = 1280; // 进一步加宽主窗口
    private const int DefaultFormHeight = 750;
    private const int MinFormWidth = 1100; // 调整最小宽度
    private const int MinFormHeight = 650;
    private const int BrowseButtonWidth = 130; // 浏览按钮宽度
    private const int ClearButtonWidth = 130; // 清空按钮宽度
    private const int OutputPathLabelWidth = 95; // 输出路径标签宽度（加宽）

    private readonly string _settingsPath = Path.Combine(
        AppContext.BaseDirectory,
        "VideoBatchCutter_settings.json");

    public MainForm()
    {
        _processor = new VideoProcessor();
        _videoList = new BindingList<VideoFileInfo>();
        _config = new ProcessingConfig();

        _processor.ProgressChanged += OnProgressChanged;
        _processor.LogMessage += OnLogMessage;
        _processor.ProcessingCompleted += OnProcessingCompleted;

        LoadSettings();
        SetupUI();
        LoadVideoListFromSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (settings != null)
                {
                    if (settings.TryGetValue("SegmentDuration", out var duration) && duration.ValueKind == JsonValueKind.Number)
                        _config.SegmentDuration = duration.GetInt32();
                    if (settings.TryGetValue("SegmentsPerVideo", out var segments) && segments.ValueKind == JsonValueKind.Number)
                        _config.SegmentsPerVideo = segments.GetInt32();
                    if (settings.TryGetValue("OutputPrefix", out var prefix))
                        _config.OutputPrefix = prefix.GetString() ?? "";
                    if (settings.TryGetValue("KeepOriginalFileName", out var keep) && (keep.ValueKind == JsonValueKind.True || keep.ValueKind == JsonValueKind.False))
                        _config.KeepOriginalFileName = keep.GetBoolean();
                    if (settings.TryGetValue("CreateZipArchive", out var zip) && (zip.ValueKind == JsonValueKind.True || zip.ValueKind == JsonValueKind.False))
                        _config.CreateZipArchive = zip.GetBoolean();
                    if (settings.TryGetValue("DeleteOriginalAfterZip", out var del) && (del.ValueKind == JsonValueKind.True || del.ValueKind == JsonValueKind.False))
                        _config.DeleteOriginalAfterZip = del.GetBoolean();
                    if (settings.TryGetValue("RandomSort", out var randomSort) && (randomSort.ValueKind == JsonValueKind.True || randomSort.ValueKind == JsonValueKind.False))
                        _config.RandomSort = randomSort.GetBoolean();
                    if (settings.TryGetValue("OutputDirectory", out var dir))
                        _config.OutputDirectory = dir.GetString() ?? "";
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}"); }
    }

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
                ["OutputDirectory"] = _config.OutputDirectory,
                ["LastVideoFiles"] = _videoList.Select(v => v.FilePath).ToList()
            };

            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    private async void LoadVideoListFromSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (settings != null && settings.TryGetValue("LastVideoFiles", out var files) && files.ValueKind == JsonValueKind.Array)
                {
                    var fileList = files.Deserialize<List<string>>();
                    if (fileList != null && fileList.Count > 0)
                    {
                        await AddFilesAsync(fileList.ToArray(), true);
                    }
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"加载视频列表失败: {ex.Message}"); }
    }

    private void SetupUI()
    {
        this.Text = "视频批量剪辑工具";
        this.Size = new Size(DefaultFormWidth, DefaultFormHeight);
        this.MinimumSize = new Size(MinFormWidth, MinFormHeight);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(245, 247, 250);

        // 主布局：上中下四部分
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(8),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, OutputPathRowHeight)); // 输出路径行
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, DropRowHeight)); // 拖拽区域
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 主内容（设置+列表）
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, LogRowHeight)); // 日志

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

        _ = CheckFFmpegAsync();
    }

    private Panel CreateOutputPathRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8) };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, OutputPathLabelWidth)); // 标签（加宽）
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // 路径输入框（自动填充）
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, BrowseButtonWidth)); // 浏览按钮（加宽）
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ClearButtonWidth)); // 清空按钮（加宽）

        var lblPath = new Label
        {
            Text = "输出路径:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("微软雅黑", 9)
        };

        _outputPathTextBox = new TextBox
        {
            PlaceholderText = "请选择输出目录...",
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(5, 0, 5, 0)
        };

        var browseBtn = CreateButton("浏览目录", Color.FromArgb(33, 150, 243));
        browseBtn.Click += (s, e) =>
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
        };

        var clearBtn = CreateButton("清空列表", Color.FromArgb(120, 120, 120));
        clearBtn.Click += (s, e) =>
        {
            _videoList.Clear();
            AppendLog("已清空列表");
            SaveSettings();
        };

        layout.Controls.Add(lblPath, 0, 0);
        layout.Controls.Add(_outputPathTextBox, 1, 0);
        layout.Controls.Add(browseBtn, 2, 0);
        layout.Controls.Add(clearBtn, 3, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateDropRow()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(240, 248, 255),
            AllowDrop = true
        };

        var dropLabel = new Label
        {
            Text = "📁 拖拽视频文件到此处 或 点击选择文件",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("微软雅黑", 10),
            ForeColor = Color.FromArgb(70, 130, 180),
            Cursor = Cursors.Hand
        };
        panel.Controls.Add(dropLabel);

        panel.Click += async (s, e) => await SelectFilesAsync();
        dropLabel.Click += async (s, e) => await SelectFilesAsync();

        panel.DragEnter += (s, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        panel.DragDrop += async (s, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
                await AddFilesAsync(files, false);
        };

        return panel;
    }

    private Panel CreateContentRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill };

        // 左边：设置面板
        var settingsPanel = CreateSettingsPanel();

        // 右边：视频列表
        var listPanel = CreateVideoListPanel();

        panel.Controls.Add(listPanel);
        panel.Controls.Add(settingsPanel);

        return panel;
    }

    private Panel CreateSettingsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Left,
            Width = SettingsPanelWidth,
            BackColor = Color.White,
            Padding = new Padding(15)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 9,
            ColumnCount = 2,
            Padding = new Padding(5)
        };

        // 设置行高
        for (int i = 0; i < 7; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // 开始按钮
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // 取消按钮

        // 设置列宽 - 标签列加宽以显示完整文字
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // 标签列（大幅加宽）
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // 控件列

        int row = 0;

        // 片段时长
        layout.Controls.Add(CreateLabel("片段时长:"), 0, row);
        _durationNumeric = CreateNumericUpDown(_config.SegmentDuration, 1, 300);
        _durationNumeric.ValueChanged += (s, e) => 
        { 
            if (_durationNumeric != null) 
                _config.SegmentDuration = (int)_durationNumeric.Value; 
        };
        layout.Controls.Add(_durationNumeric, 1, row);
        row++;

        // 每视频段数
        layout.Controls.Add(CreateLabel("每视频段数:"), 0, row);
        _segmentsNumeric = CreateNumericUpDown(_config.SegmentsPerVideo, 1, 100);
        _segmentsNumeric.ValueChanged += (s, e) => 
        { 
            if (_segmentsNumeric != null) 
                _config.SegmentsPerVideo = (int)_segmentsNumeric.Value; 
        };
        layout.Controls.Add(_segmentsNumeric, 1, row);
        row++;

        // 文件名前缀
        layout.Controls.Add(CreateLabel("文件名前缀:"), 0, row);
        _prefixTextBox = new TextBox
        {
            PlaceholderText = "留空用原名",
            Dock = DockStyle.Fill,
            Font = new Font("微软雅黑", 9),
            Text = _config.OutputPrefix
        };
        _prefixTextBox.TextChanged += (s, e) => 
        { 
            if (_prefixTextBox != null) 
                _config.OutputPrefix = _prefixTextBox.Text; 
        };
        layout.Controls.Add(_prefixTextBox, 1, row);
        row++;

        // 保留原文件名（跨两列）
        _keepNameCheckBox = new CheckBox
        {
            Text = "保留原文件名",
            Checked = _config.KeepOriginalFileName,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(0, 5, 0, 0),
            AutoSize = true
        };
        _keepNameCheckBox.CheckedChanged += (s, e) => 
        { 
            if (_keepNameCheckBox != null) 
                _config.KeepOriginalFileName = _keepNameCheckBox.Checked; 
        };
        layout.Controls.Add(_keepNameCheckBox, 0, row);
        layout.SetColumnSpan(_keepNameCheckBox, 2);
        row++;

        // 打包ZIP（跨两列）
        _zipCheckBox = new CheckBox
        {
            Text = "打包ZIP",
            Checked = _config.CreateZipArchive,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(0, 5, 0, 0),
            AutoSize = true
        };
        _zipCheckBox.CheckedChanged += (s, e) => 
        { 
            if (_zipCheckBox != null) 
            {
                _config.CreateZipArchive = _zipCheckBox.Checked;
                if (_deleteOriginalCheckBox != null)
                    _deleteOriginalCheckBox.Enabled = _zipCheckBox.Checked;
            }
        };
        layout.Controls.Add(_zipCheckBox, 0, row);
        layout.SetColumnSpan(_zipCheckBox, 2);
        row++;

        // 打包后删除原文件（跨两列）
        _deleteOriginalCheckBox = new CheckBox
        {
            Text = "打包后删除原文件",
            Checked = _config.DeleteOriginalAfterZip,
            Enabled = _config.CreateZipArchive,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(0, 5, 0, 0),
            AutoSize = true
        };
        _deleteOriginalCheckBox.CheckedChanged += (s, e) => 
        { 
            if (_deleteOriginalCheckBox != null) 
                _config.DeleteOriginalAfterZip = _deleteOriginalCheckBox.Checked; 
        };
        layout.Controls.Add(_deleteOriginalCheckBox, 0, row);
        layout.SetColumnSpan(_deleteOriginalCheckBox, 2);
        row++;

        // 随机排序（跨两列）
        _randomSortCheckBox = new CheckBox
        {
            Text = "随机排序（数字放开头）",
            Checked = _config.RandomSort,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(0, 5, 0, 0),
            AutoSize = true
        };
        _randomSortCheckBox.CheckedChanged += (s, e) => 
        { 
            if (_randomSortCheckBox != null) 
                _config.RandomSort = _randomSortCheckBox.Checked; 
        };
        layout.Controls.Add(_randomSortCheckBox, 0, row);
        layout.SetColumnSpan(_randomSortCheckBox, 2);
        row++;

        // 开始按钮（跨两列，居中）
        _startButton = CreateButton("开始处理", Color.FromArgb(76, 175, 80), true);
        _startButton.Click += async (s, e) => await StartProcessingAsync();
        layout.Controls.Add(_startButton, 0, row);
        layout.SetColumnSpan(_startButton, 2);
        row++;

        // 取消按钮（跨两列，居中）
        _cancelButton = CreateButton("取消", Color.FromArgb(244, 67, 54), true);
        _cancelButton.Enabled = false;
        _cancelButton.Click += (s, e) => _cancellationTokenSource?.Cancel();
        layout.Controls.Add(_cancelButton, 0, row);
        layout.SetColumnSpan(_cancelButton, 2);

        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateVideoListPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

        _dataGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = true,
            ReadOnly = true,
            BackgroundColor = Color.White,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
            ColumnHeadersHeight = 28,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(0)
        };

        // 添加列
        _dataGridView.Columns.Add(new DataGridViewTextBoxColumn 
        { 
            Name = "FileName", 
            DataPropertyName = "FileNameWithoutExtension", 
            HeaderText = "文件名", 
            Width = 250 
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
        _videoList.ListChanged += (s, e) => SaveSettings();

        panel.Controls.Add(_dataGridView);
        return panel;
    }

    private Panel CreateLogRow()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(5) };

        _progressLabel = new Label 
        { 
            Text = "总体进度: 0%", 
            Dock = DockStyle.Top, 
            Height = 22, 
            Font = new Font("微软雅黑", 9, FontStyle.Bold),
            Margin = new Padding(0, 5, 0, 0)
        };
        
        _overallProgressBar = new ProgressBar 
        { 
            Dock = DockStyle.Top, 
            Height = 18, 
            Maximum = 100,
            Margin = new Padding(0, 2, 0, 0)
        };
        
        _logTextBox = new TextBox 
        { 
            Dock = DockStyle.Fill, 
            Multiline = true, 
            ReadOnly = true, 
            ScrollBars = ScrollBars.Vertical, 
            BackColor = Color.FromArgb(30, 30, 30), 
            ForeColor = Color.LightGray, 
            Font = new Font("Consolas", 9),
            Margin = new Padding(0, 5, 0, 0)
        };

        panel.Controls.Add(_logTextBox);
        panel.Controls.Add(_overallProgressBar);
        panel.Controls.Add(_progressLabel);

        return panel;
    }

    #region 辅助方法
    private Button CreateButton(string text, Color backColor, bool center = false)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("微软雅黑", 10, FontStyle.Bold),
            Size = new Size(120, 35)
        };

        if (center)
        {
            btn.Dock = DockStyle.Fill;
            btn.Anchor = AnchorStyles.None;
        }

        return btn;
    }

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(0, 5, 0, 0)
        };
    }

    private NumericUpDown CreateNumericUpDown(int value, int min, int max)
    {
        return new NumericUpDown
        {
            Value = value,
            Minimum = min,
            Maximum = max,
            Dock = DockStyle.Fill,
            Font = new Font("微软雅黑", 9),
            Margin = new Padding(0, 5, 0, 0)
        };
    }
    #endregion

    private async Task CheckFFmpegAsync()
    {
        try
        {
            bool isValid = await _processor.ValidateEnvironmentAsync();
            AppendLog(isValid ? "✓ FFmpeg 环境正常" : "⚠ 未检测到 FFmpeg 环境");
        }
        catch { AppendLog("⚠ FFmpeg 环境检查失败"); }
    }

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
        SaveSettings();
    }

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
        }
    }

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
    }

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

    private void OnLogMessage(object? sender, string message) => AppendLog(message);
    private void OnProgressChanged(object? sender, ProcessingProgress progress) => UpdateProgressUI(progress);
    private void OnProcessingCompleted(object? sender, ProcessingResult result) => AppendLog($"处理完成！成功: {result.SuccessfulSegments}, 失败: {result.FailedSegments}");

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        base.OnFormClosing(e);
    }
}