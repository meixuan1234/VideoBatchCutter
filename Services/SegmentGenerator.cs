using VideoBatchCutter.Models;

namespace VideoBatchCutter.Services;

/// <summary>
/// 视频片段生成器，负责生成不重叠的随机起始点
/// </summary>
public class SegmentGenerator
{
    private readonly Random _random = new();

    /// <summary>
    /// 为单个视频生成随机不重叠的片段列表
    /// </summary>
    /// <param name="videoInfo">视频信息</param>
    /// <param name="config">处理配置</param>
    /// <returns>片段列表；若视频过短或配置导致无可用区间，则返回空列表</returns>
    /// <remarks>
    /// 当 <see cref="ProcessingConfig.SkipFirstFiveSeconds"/> 为 true 时，
    /// 片段起始时间将从第 5 秒之后开始计算；若视频时长小于等于 5 秒，则不生成片段。
    /// </remarks>
    public List<VideoSegment> GenerateSegments(VideoFileInfo videoInfo, ProcessingConfig config)
    {
        var segments = new List<VideoSegment>();

        if (videoInfo.Duration <= 0)
            return segments;

        // 启用去除前5秒时，视频时长必须大于5秒才有可用区间
        if (config.SkipFirstFiveSeconds && videoInfo.Duration <= 5)
            return segments;

        // 计算可用时间范围（留出片段时长+1秒余量）
        double maxStartTime = videoInfo.Duration - config.SegmentDuration - 1;
        if (maxStartTime <= 0)
            maxStartTime = 0;

        // 如果视频太短，只能生成1段从开头截取
        // 启用去除前5秒时，需保证去除前5秒后仍能容纳片段；否则不生成片段
        if (videoInfo.Duration <= config.SegmentDuration)
        {
            if (!config.SkipFirstFiveSeconds)
            {
                segments.Add(new VideoSegment
                {
                    VideoFileId = videoInfo.Id,
                    SegmentIndex = 1,
                    StartTime = 0,
                    Duration = Math.Min(videoInfo.Duration, config.SegmentDuration),
                    Status = SegmentStatus.Pending
                });
            }
            return segments;
        }

        // 确定可用起始区间的左边界：启用去除前5秒时为 5，否则为 0
        double rangeStart = config.SkipFirstFiveSeconds ? 5 : 0;

        // 可用区间右边界不能小于左边界，否则无可用区间
        if (maxStartTime <= rangeStart)
            return segments;

        // 使用区间排除法生成不重叠的随机起始点
        var availableRanges = new List<(double Start, double End)>
        {
            (rangeStart, maxStartTime)
        };

        int maxAttempts = config.SegmentsPerVideo * 100;
        int attempts = 0;

        for (int i = 0; i < config.SegmentsPerVideo && attempts < maxAttempts; i++)
        {
            attempts++;

            // 计算总可用长度
            double totalAvailable = availableRanges.Sum(r => r.End - r.Start);
            if (totalAvailable < config.SegmentDuration)
                break;

            // 随机选择一个区间
            double randomPoint = _random.NextDouble() * totalAvailable;
            double currentSum = 0;
            var selectedRange = availableRanges[0];
            int selectedIndex = 0;

            for (int j = 0; j < availableRanges.Count; j++)
            {
                var range = availableRanges[j];
                double rangeLength = range.End - range.Start;
                if (currentSum + rangeLength >= randomPoint)
                {
                    selectedRange = range;
                    selectedIndex = j;
                    break;
                }
                currentSum += rangeLength;
            }

            // 在选中的区间内随机选择起始点
            double maxStartInRange = selectedRange.End - selectedRange.Start - config.SegmentDuration;
            if (maxStartInRange < 0)
                continue;

            double startTime = selectedRange.Start + _random.NextDouble() * maxStartInRange;
            double endTime = startTime + config.SegmentDuration;

            // 创建片段
            segments.Add(new VideoSegment
            {
                VideoFileId = videoInfo.Id,
                SegmentIndex = i + 1,
                StartTime = startTime,
                Duration = config.SegmentDuration,
                Status = SegmentStatus.Pending
            });

            // 更新可用区间（排除已选区域及其前后1秒缓冲）
            availableRanges.RemoveAt(selectedIndex);

            double bufferBefore = Math.Max(rangeStart, startTime - 1);
            double bufferAfter = Math.Min(maxStartTime, endTime + 1);

            if (selectedRange.Start < bufferBefore - 0.1)
            {
                availableRanges.Insert(selectedIndex, (selectedRange.Start, bufferBefore));
                selectedIndex++;
            }

            if (bufferAfter < selectedRange.End - 0.1)
            {
                availableRanges.Insert(selectedIndex, (bufferAfter, selectedRange.End));
            }
        }

        // 按起始时间排序，便于查看
        segments.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        // 重新编号
        for (int i = 0; i < segments.Count; i++)
        {
            segments[i].SegmentIndex = i + 1;
        }

        return segments;
    }

    /// <summary>
    /// 为所有视频生成片段
    /// </summary>
    /// <param name="videos">视频信息列表</param>
    /// <param name="config">处理配置</param>
    public void GenerateAllSegments(List<VideoFileInfo> videos, ProcessingConfig config)
    {
        foreach (var video in videos)
        {
            video.Segments = GenerateSegments(video, config);
        }
    }
}
