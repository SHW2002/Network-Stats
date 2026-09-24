using NetworkStats.Models;

namespace NetworkStats.App.Views;

internal sealed class AppearanceSettingsView : ContentView
{
    private readonly Picker _theme = new() { Title = "深色模式", ItemsSource = new[] { "跟随系统", "浅色", "深色" } };
    private readonly Switch _minimize = new();

    internal ThemeMode SelectedTheme { get => (ThemeMode)_theme.SelectedIndex; set => _theme.SelectedIndex = (int)value; }
    internal bool MinimizeOnClose { get => _minimize.IsToggled; set => _minimize.IsToggled = value; }

    public AppearanceSettingsView(MonitorSettings settings)
    {
        SelectedTheme = settings.Theme;
        MinimizeOnClose = settings.MinimizeOnClose;
        Ui.ThemePicker(_theme);
        Ui.Bind(_minimize, Switch.OnColorProperty, Ui.PrimaryButton);
        var body = new VerticalStackLayout { Spacing = Ui.Space(10), Children =
        {
            Ui.Text("外观与窗口", 17, bold: true),
            Ui.Text("深色模式", 13, bold: true), _theme,
            Ui.Text("保存后生效；跟随系统会随设备的浅色／深色外观自动切换。", 11, Ui.Muted)
        } };
#if WINDOWS
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        row.Add(Ui.Text("关闭时改为缩放到托盘", 13, bold: true));
        row.Add(_minimize, 1);
        body.Add(row);
        body.Add(Ui.Text("点击窗口关闭按钮后继续探测，并最小化到托盘；需要退出时，使用托盘右键菜单的“退出”。", 11, Ui.Muted));
#endif
        Content = Ui.Card(body);
    }
}
