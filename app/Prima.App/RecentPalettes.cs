using System.Text.Json;

namespace Prima.App;

public sealed class RecentPalettes
{
    public const int DefaultCapacity = 10;

    private readonly List<string> _paths = new();

    public int Capacity { get; }
    public IReadOnlyList<string> Paths => _paths.AsReadOnly();

    public RecentPalettes(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public void Add(string path)
    {
        _paths.Remove(path);
        _paths.Insert(0, path);
        while (_paths.Count > Capacity)
            _paths.RemoveAt(_paths.Count - 1);
    }

    public void Save(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(new RecentJson { Paths = _paths });
        File.WriteAllText(filePath, json);
    }

    public static RecentPalettes Load(string filePath)
    {
        var result = new RecentPalettes();
        if (!File.Exists(filePath)) return result;
        try
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<RecentJson>(json);
            if (data?.Paths is not null)
            {
                foreach (var path in data.Paths)
                {
                    if (File.Exists(path))
                        result._paths.Add(path);
                }
            }
        }
        catch
        {
            // Corrupt file — start fresh
        }
        return result;
    }

    private sealed class RecentJson
    {
        public List<string>? Paths { get; set; }
    }
}
