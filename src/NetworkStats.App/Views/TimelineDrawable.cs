using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal sealed class TimelineDrawable : IDrawable
{
    public ProbeResult?[] Samples { get; set; } = new ProbeResult?[60];
    public int SelectedIndex { get; set; } = -1;
    internal bool HasDrawn { get; private set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || Samples.Length == 0) return;
        var step = dirtyRect.Width / Samples.Length;
        var gap = Math.Min(3, step * 0.25f);
        for (var index = 0; index < Samples.Length; index++)
        {
            var sample = Samples[index];
            canvas.FillColor = sample is null ? Ui.Empty : Ui.StatusColor(sample.Status);
            var rectangle = new RectF(index * step + gap / 2, (dirtyRect.Height - 22) / 2, step - gap, 22);
            canvas.FillRoundedRectangle(rectangle, Math.Min(3, (step - gap) / 2));
            if (SelectedIndex == index)
            {
                canvas.StrokeColor = Ui.Ink;
                canvas.StrokeSize = 1.5f;
                canvas.DrawRoundedRectangle(new RectF(rectangle.X, rectangle.Y - 3, rectangle.Width, rectangle.Height + 6), 2);
            }
        }
        HasDrawn = true;
    }
}

internal sealed class TimeAxisDrawable : IDrawable
{
    public DateTimeOffset Start { get; set; }
    public int Minutes { get; set; } = 60;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FontColor = Ui.Muted;
        canvas.FontSize = 10;
        for (var index = 0; index <= 4; index++)
        {
            var minute = (Minutes - 1) * index / 4;
            var text = Start.AddMinutes(minute).ToLocalTime().ToString(Minutes > 720 ? "dd日 HH:mm" : "HH:mm");
            var x = (dirtyRect.Width - 72) * index / 4;
            canvas.DrawString(text, x, 0, 72, 20,
                index == 4 ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment.Center);
        }
    }
}

internal sealed record CellSelection(SiteDefinition Site, RouteDefinition Route, DateTimeOffset Minute, ProbeResult? Sample);
