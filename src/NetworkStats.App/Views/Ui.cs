using Microsoft.Maui.Controls.Shapes;
using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal static partial class Ui
{
    public static ThemeColor StatusColor(ProbeStatus? status) => status switch
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

    public static Label Text(string value, double size = 14, ThemeColor? color = null, bool bold = false)
    {
        var label = new Label { Text = value, FontSize = Font(size),
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, VerticalTextAlignment = TextAlignment.Center };
        TextColor(label, color ?? Ink);
        return label;
    }

    public static Border Card(View content, double padding = 20)
    {
        var card = new Border { Content = content, Padding = Space(padding), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = IsDesktop ? 10 : 16 } };
        Bind(card, VisualElement.BackgroundColorProperty, Surface);
        card.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Line.Light), new SolidColorBrush(Line.Dark));
        return card;
    }

    public static Button Button(string text, bool primary = false)
    {
        var button = new Button { Text = text, FontSize = 13, FontAttributes = FontAttributes.Bold,
            Padding = Space(new Thickness(16, 10)), CornerRadius = IsDesktop ? 6 : 10,
            MinimumHeightRequest = IsDesktop ? ControlHeight : 42 };
        Bind(button, Microsoft.Maui.Controls.Button.TextColorProperty, primary ? White : Ink);
        Bind(button, VisualElement.BackgroundColorProperty, primary ? PrimaryButton : SecondaryButton);
        return button;
    }

    public static Entry Input(string value, string placeholder, Keyboard? keyboard = null)
    {
        var entry = new Entry { Text = value, Placeholder = placeholder, Keyboard = keyboard ?? Keyboard.Default,
            FontSize = Font(14), MinimumHeightRequest = ControlHeight, ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
        Bind(entry, Entry.TextColorProperty, Ink);
        Bind(entry, Entry.PlaceholderColorProperty, Muted);
        Bind(entry, VisualElement.BackgroundColorProperty, Background);
        return entry;
    }
}
