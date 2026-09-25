using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal sealed class TimelineDrawable : IDrawable
{
    public ProbeResult?[] Samples { get; set; } = [];
    public DateTimeOffset[] SampleTimes { get; set; } = [];
    public int SelectedIndex { get; set; } = -1;
    internal bool HasDrawn { get; private set; }
    internal AppTheme DrawnTheme { get; private set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0) return;
        if (Samples.Length == 0)
        {
            HasDrawn = true;
            DrawnTheme = Application.Current!.RequestedTheme;
            return;
        }
        var step = dirtyRect.Width / Samples.Length;
        var gap = Math.Min(3, step * 0.25f);
        for (var index = 0; index < Samples.Length; index++)
        {
            var sample = Samples[index];
            canvas.FillColor = (sample is null ? Ui.Empty : Ui.StatusColor(sample.Status)).Current;
            var rectangle = new RectF(index * step + gap / 2, (dirtyRect.Height - 22) / 2, step - gap, 22);
            canvas.FillRoundedRectangle(rectangle, Math.Min(3, (step - gap) / 2));
            if (SelectedIndex == index)
            {
                canvas.StrokeColor = Ui.Ink.Current;
                canvas.StrokeSize = 1.5f;
                canvas.DrawRoundedRectangle(new RectF(rectangle.X, rectangle.Y - 3, rectangle.Width, rectangle.Height + 6), 2);
            }
        }
        HasDrawn = true;
        DrawnTheme = Application.Current!.RequestedTheme;
    }
}

internal sealed class TimeAxisDrawable : IDrawable
{
    public DateTimeOffset? Now { get; set; }
    public int RangeMinutes { get; set; } = 60;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || Now is not { } now) return;
        canvas.FontColor = Ui.Muted.Current;
        canvas.FontSize = 10;
        canvas.StrokeColor = Ui.Muted.Current;
        canvas.StrokeSize = 1;
        var firstLocal = now.AddMinutes(-RangeMinutes).ToLocalTime();
        var lastLocal = now.ToLocalTime();
        var format = firstLocal.Date == lastLocal.Date ? "HH:mm" : "MM-dd HH:mm";
        var labelWidth = TimeAxisScale.TickLabelWidth(dirtyRect.Width);
        foreach (var tick in TimeAxisScale.Create(now, RangeMinutes, dirtyRect.Width))
        {
            var text = tick.Time.ToLocalTime().ToString(format);
            canvas.DrawString(text, tick.X - labelWidth / 2, 0, labelWidth, 18,
                HorizontalAlignment.Center, VerticalAlignment.Center);
            canvas.DrawLine(tick.X, 19, tick.X, dirtyRect.Height);
        }
        var nowLabelWidth = TimeAxisScale.NowLabelWidth(dirtyRect.Width);
        var nowX = TimeAxisScale.NowPosition(dirtyRect.Width);
        canvas.DrawString(TimeAxisScale.NowLabel, dirtyRect.Width - nowLabelWidth, 0, nowLabelWidth, 18,
            HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.DrawLine(nowX, 19, nowX, dirtyRect.Height);
    }
}

internal sealed record CellSelection(SiteDefinition Site, RouteDefinition Route, DateTimeOffset SampleTime, ProbeResult? Sample,
    bool SpeedMeasurementEnabled = true);
