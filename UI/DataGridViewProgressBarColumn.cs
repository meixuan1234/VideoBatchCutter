namespace VideoBatchCutter.UI;

/// <summary>
/// DataGridView进度条列，用于在表格中直观显示处理进度
/// </summary>
public class DataGridViewProgressBarColumn : DataGridViewColumn
{
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
    public DataGridViewProgressBarCell()
    {
    }

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
        // 先绘制背景
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

        // 解析进度值
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

        using (var backBrush = new SolidBrush(Color.FromArgb(230, 230, 230)))
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

            Color fillColor = progress >= 100 ? Color.FromArgb(76, 175, 80) : Color.FromArgb(33, 150, 243);
            using (var fillBrush = new SolidBrush(fillColor))
            {
                graphics.FillRectangle(fillBrush, fillRect);
            }
        }

        // 绘制进度文本
        string progressText = $"{progress}%";
        using (var textBrush = new SolidBrush(Color.Black))
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
