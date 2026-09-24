namespace NetworkStats.App.Views;

internal sealed class MetricCard : ContentView
{
    private readonly Label _value;
    private readonly Label _note;

    public MetricCard(string title, string value, string note)
    {
        _value = Ui.Text(value, 28, bold: true);
        _note = Ui.Text(note, 11, Ui.Muted);
        Content = Ui.Card(new VerticalStackLayout
        {
            Spacing = Ui.Space(7),
            Children = { Ui.Text(title, 12, Ui.Muted), _value, _note }
        }, 18);
    }

    public void Update(string value, string? note = null)
    {
        _value.Text = value;
        if (note is not null) _note.Text = note;
    }
}
