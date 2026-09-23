namespace NetworkStats.App.Views;

internal sealed record ThemeColor(Color Light, Color Dark)
{
    public ThemeColor(string light, string dark) : this(Color.FromArgb(light), Color.FromArgb(dark)) { }
    public Color Current => Application.Current?.RequestedTheme == AppTheme.Dark ? Dark : Light;
}

internal static partial class Ui
{
    public static readonly ThemeColor Background = new("#F5F7FB", "#111827");
    public static readonly ThemeColor Surface = new("#FFFFFF", "#1C2738");
    public static readonly ThemeColor Ink = new("#172B45", "#E6EDF7");
    public static readonly ThemeColor Muted = new("#7B889C", "#A1B0C5");
    public static readonly ThemeColor Line = new("#E7ECF3", "#35445A");
    public static readonly ThemeColor Accent = new("#496BE8", "#9AB5FF");
    public static readonly ThemeColor PrimaryButton = new("#496BE8", "#3558C5");
    public static readonly ThemeColor SecondaryButton = new("#EAF0FA", "#2A3950");
    public static readonly ThemeColor White = new("#FFFFFF", "#FFFFFF");
    public static readonly ThemeColor Green = new("#32B58B", "#53D3AB");
    public static readonly ThemeColor Yellow = new("#E7B64F", "#F1C86B");
    public static readonly ThemeColor Red = new("#E87575", "#FF9292");
    public static readonly ThemeColor Empty = new("#E8EDF4", "#35445A");

    public static void Bind(BindableObject target, BindableProperty property, ThemeColor color) =>
        target.SetAppThemeColor(property, color.Light, color.Dark);

    public static void TextColor(Label label, ThemeColor color) => Bind(label, Label.TextColorProperty, color);

    public static void ThemePicker(Picker picker)
    {
        Bind(picker, Picker.TextColorProperty, Ink);
        Bind(picker, Picker.TitleColorProperty, Muted);
        Bind(picker, VisualElement.BackgroundColorProperty, Background);
    }
}
