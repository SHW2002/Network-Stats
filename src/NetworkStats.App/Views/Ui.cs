using Microsoft.Maui.Controls.Shapes;
using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal static class Ui
{
    public static readonly Color Background = Color.FromArgb("#F5F7FB");
    public static readonly Color Ink = Color.FromArgb("#172B45");
    public static readonly Color Muted = Color.FromArgb("#7B889C");
    public static readonly Color Line = Color.FromArgb("#E7ECF3");
    public static readonly Color Accent = Color.FromArgb("#496BE8");
    public static readonly Color Green = Color.FromArgb("#32B58B");
    public static readonly Color Yellow = Color.FromArgb("#E7B64F");
    public static readonly Color Red = Color.FromArgb("#E87575");
    public static readonly Color Empty = Color.FromArgb("#E8EDF4");

    public static Color StatusColor(ProbeStatus? status) => status switch
    {
        ProbeStatus.Healthy => Green,
        ProbeStatus.Slow => Yellow,
        ProbeStatus.Unreachable => Red,
        _ => Muted
    };

    public static string StatusText(ProbeStatus? status) => status switch
    {
        ProbeStatus.Healthy => "可访问",
        ProbeStatus.Slow => "响应较慢",
        ProbeStatus.Unreachable => "不可访问",
        _ => "无数据"
    };

    public static Label Text(string value, double size = 14, Color? color = null, bool bold = false) => new()
    {
        Text = value,
        FontSize = size,
        TextColor = color ?? Ink,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        VerticalTextAlignment = TextAlignment.Center
    };

    public static Border Card(View content, double padding = 20) => new()
    {
        Content = content,
        Padding = padding,
        BackgroundColor = Colors.White,
        Stroke = Line,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 16 }
    };

    public static Button Button(string text, bool primary = false) => new()
    {
        Text = text,
        TextColor = primary ? Colors.White : Ink,
        BackgroundColor = primary ? Accent : Color.FromArgb("#EAF0FA"),
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        Padding = new Thickness(16, 10),
        CornerRadius = 10,
        MinimumHeightRequest = 42
    };

    public static Entry Input(string value, string placeholder, Keyboard? keyboard = null) => new()
    {
        Text = value,
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Default,
        TextColor = Ink,
        PlaceholderColor = Muted,
        BackgroundColor = Color.FromArgb("#F5F7FB"),
        FontSize = 14,
        MinimumHeightRequest = 44,
        ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };
}
