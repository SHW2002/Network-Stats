namespace NetworkStats.App.Views;

internal sealed class RouteSpeedSettingsView : ContentView
{
    private readonly Switch _enabled = new();

    internal bool Enabled { get => _enabled.IsToggled; set => _enabled.IsToggled = value; }
    internal event EventHandler? Changed;

    public RouteSpeedSettingsView(string title, bool enabled, string note)
    {
        Enabled = enabled;
        _enabled.Toggled += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Ui.Bind(_enabled, Switch.OnColorProperty, Ui.PrimaryButton);
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = Ui.Space(12) };
        row.Add(Ui.Text(title, 13, bold: true));
        row.Add(_enabled, 1);
        Content = Ui.Card(new VerticalStackLayout { Spacing = Ui.Space(8), Children = { row, Ui.Text(note, 11, Ui.Muted) } }, 12);
    }
}
