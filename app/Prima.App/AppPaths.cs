namespace Prima.App;

public static class AppPaths
{
    private static string? _root;
    private static string? _palettes;
    private static string? _recentJson;

    public static string Root =>
        _root ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Prima");

    public static string Palettes =>
        _palettes ??= Path.Combine(Root, "palettes");

    public static string RecentJson =>
        _recentJson ??= Path.Combine(Root, "recent.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Palettes);
    }
}
