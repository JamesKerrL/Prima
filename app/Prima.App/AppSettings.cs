using System.Text.Json;

namespace Prima.App;

public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(AppPaths.Root, "settings.json");
    private static AppSettings? _instance;

    public enum RendererBackend
    {
        Best,      // Try D3D11, fall back to software
        Direct3D11, // D3D11 only
        Software    // Software only
    }

    public RendererBackend Renderer { get; set; } = RendererBackend.Best;

    public static AppSettings Instance
    {
        get
        {
            _instance ??= Load();
            return _instance;
        }
    }

    private static AppSettings Load()
    {
        AppPaths.EnsureDirectories();

        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        AppPaths.EnsureDirectories();
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(SettingsPath, json);
    }
}
