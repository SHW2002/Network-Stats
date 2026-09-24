namespace NetworkStats.App.Views;

internal static partial class Ui
{
#if WINDOWS || MACCATALYST
    public static bool IsDesktop => true;
#else
    public static bool IsDesktop => false;
#endif

    // 桌面减少留白和大标题，保留小字号说明及移动端触控尺寸。
    public static double Space(double value) => IsDesktop ? Math.Round(value * 2 / 3) : value;
    public static Thickness Space(Thickness value) => new(
        Space(value.Left), Space(value.Top), Space(value.Right), Space(value.Bottom));
    public static double Font(double value) => !IsDesktop ? value : value switch
    {
        >= 20 => Math.Round(value * 0.8),
        >= 14 => value - 1,
        _ => value
    };

    public static double SiteRowHeight => IsDesktop ? 64 : 80;
    public static double ControlHeight => IsDesktop ? 32 : 44;
}
