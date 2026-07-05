using Prima.App;
using Xunit;

namespace Prima.App.Tests;

public class RecentPalettesTests
{
    [Fact]
    public void Add_MostRecentIsFirst()
    {
        var recent = new RecentPalettes();
        recent.Add("a");
        recent.Add("b");

        Assert.Equal(["b", "a"], recent.Paths);
    }

    [Fact]
    public void Add_ExistingPath_MovesToFrontWithoutDuplicating()
    {
        var recent = new RecentPalettes();
        recent.Add("a");
        recent.Add("b");
        recent.Add("a");

        Assert.Equal(["a", "b"], recent.Paths);
    }

    [Fact]
    public void Add_BeyondCapacity_EvictsOldest()
    {
        var recent = new RecentPalettes(capacity: 2);
        recent.Add("a");
        recent.Add("b");
        recent.Add("c");

        Assert.Equal(["c", "b"], recent.Paths);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecentPalettes(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecentPalettes(-1));
    }

    [Fact]
    public void SaveLoad_RoundTrip_KeepsExistingPathsInOrder()
    {
        string file1 = Path.GetTempFileName();
        string file2 = Path.GetTempFileName();
        string store = Path.GetTempFileName() + ".json";
        try
        {
            var recent = new RecentPalettes();
            recent.Add(file1);
            recent.Add(file2); // file2 is now most-recent

            recent.Save(store);
            var loaded = RecentPalettes.Load(store);

            Assert.Equal([file2, file1], loaded.Paths);
        }
        finally
        {
            foreach (var p in new[] { file1, file2, store })
                if (File.Exists(p)) File.Delete(p);
        }
    }

    [Fact]
    public void Load_DropsPathsThatNoLongerExist()
    {
        string existing = Path.GetTempFileName();
        string missing = Path.GetTempFileName();
        File.Delete(missing); // recorded but gone by load time
        string store = Path.GetTempFileName() + ".json";
        try
        {
            var recent = new RecentPalettes();
            recent.Add(existing);
            recent.Add(missing);
            recent.Save(store);

            var loaded = RecentPalettes.Load(store);

            Assert.Equal([existing], loaded.Paths);
        }
        finally
        {
            foreach (var p in new[] { existing, store })
                if (File.Exists(p)) File.Delete(p);
        }
    }

    [Fact]
    public void Load_NonExistentFile_ReturnsEmpty()
    {
        var loaded = RecentPalettes.Load(Path.GetTempFileName() + "_absent.json");

        Assert.Empty(loaded.Paths);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsEmptyInsteadOfThrowing()
    {
        string store = Path.GetTempFileName() + ".json";
        try
        {
            File.WriteAllText(store, "{ this is not valid json ]");

            var loaded = RecentPalettes.Load(store);

            Assert.Empty(loaded.Paths);
        }
        finally
        {
            if (File.Exists(store)) File.Delete(store);
        }
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        string root = Path.GetTempFileName() + "_d";
        string store = Path.Combine(root, "nested", "recent.json");
        try
        {
            new RecentPalettes().Save(store);

            Assert.True(File.Exists(store));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
