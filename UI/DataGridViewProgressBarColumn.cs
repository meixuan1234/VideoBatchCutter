namespace VideoBatchCutter.UI;

/// <summary>
/// DataGridView进度条列，用于在表格中直观显示处理进度
/// </summary>
public class DataGridViewProgressBarColumn : DataGridViewColumn
{
    /// <summary>
    /// 初始化进度条列
    /// </summary>
    public DataGridViewProgressBarColumn()
    {
        CellTemplate = new DataGridViewProgressBarCell();
    }
}

/// <summary>
/// DataGridView进度条单元格
/// </summary>
public class DataGridViewProgressBarCell : DataGridViewTextBoxCell
{
    // TRAE 暗色优先设计 Token
    /// <summary>进度条背景色 #2A2D31</summary>
    private static readonly Color ProgressBackgroundColor = Color.FromArgb(42, 45, 49);
    /// <summary>进度条进行中/完成填充色 #32F08C</summary>
    private static readonly Color ProgressFillColor = Color.FromArgb(50, 240, 140);
    /// <summary>进度条完成时可选的更亮填充色 #2CD67D</summary>
    private static readonly Color ProgressCompleteColor = Color.FromArgb(44, 214, 125);
    /// <summary>进度条文本颜色 #D1D3DB</summary>
    private static readonly Color ProgressTextColor = Color.FromArgb(209, 211, 219);

    /// <summary>
    /// 初始化进度条单元格
    /// </summary>
    public DataGridViewProgressBarCell()
    {
    }

    /// <summary>
    /// 绘制进度条单元格
    /// </summary>
    /// <param name="graphics">绘图上下文</param>
    /// <param name="clipBounds">裁剪区域</param>
    /// <param name="cellBounds">单元格区域</param>
    /// <param name="rowIndex">行索引</param>
    /// <param name="cellState">单元格状态</param>
    /// <param name="value">进度值（0-100）</param>
    /// <param name="formattedValue">格式化后的值</param>
    /// <param name="errorText">错误文本</param>
    /// <param name="cellStyle">单元格样式</param>
    /// <param name="advancedBorderStyle">边框样式</param>
    /// <param name="paintParts">绘制部分</param>
    protected override void Paint(
        Graphics graphics,
        Rectangle clipBounds,
        Rectangle cellBounds,
        int rowIndex,
        DataGridViewElementStates cellState,
        object? value,
        object? formattedValue,
        string? errorText,
        DataGridViewCellStyle cellStyle,
        DataGridViewAdvancedBorderStyle advancedBorderStyle,
        DataGridViewPaintParts paintParts)
    {
        // 先绘制背景（不包含前景文本）
        base.Paint(
            graphics,
            clipBounds,
            cellBounds,
            rowIndex,
            cellState,
            value,
            formattedValue,
            errorText,
            cellStyle,
            advancedBorderStyle,
            paintParts & ~DataGridViewPaintParts.ContentForeground);

        // 解析进度值，限制在 0-100 范围内
        int progress = 0;
        if (value is int intValue)
            progress = intValue;
        else if (value != null && int.TryParse(value.ToString(), out int parsed))
            progress = parsed;

        progress = Math.Max(0, Math.Min(100, progress));

        // 绘制进度条背景
        var progressBarRect = new Rectangle(
            cellBounds.X + 2,
            cellBounds.Y + 2,
            cellBounds.Width - 4,
            cellBounds.Height - 4);

        using (var backBrush = new SolidBrush(ProgressBackgroundColor))
        {
            graphics.FillRectangle(backBrush, progressBarRect);
        }

        // 绘制进度条填充
        if (progress > 0)
        {
            int fillWidth = (int)(progressBarRect.Width * (progress / 100.0));
            var fillRect = new Rectangle(
                progressBarRect.X,
                progressBarRect.Y,
                fillWidth,
                progressBarRect.Height);

            // 100% 时使用完成色，否则使用品牌色填充
            Color fillColor = progress >= 100 ? ProgressCompleteColor : ProgressFillColor;
            using (var fillBrush = new SolidBrush(fillColor))
            {
                graphics.FillRectangle(fillBrush, fillRect);
            }
        }

        // 绘制进度文本
        string progressText = $"{progress}%";
        using (var textBrush = new SolidBrush(ProgressTextColor))
        {
            var textSize = graphics.MeasureString(progressText, cellStyle.Font);
            var textRect = new RectangleF(
                cellBounds.X + (cellBounds.Width - textSize.Width) / 2,
                cellBounds.Y + (cellBounds.Height - textSize.Height) / 2,
                textSize.Width,
                textSize.Height);

            graphics.DrawString(progressText, cellStyle.Font, textBrush, textRect);
        }
    }
}
