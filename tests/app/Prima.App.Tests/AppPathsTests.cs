using Prima.App;
using Xunit;

namespace Prima.App.Tests;

public class AppPathsTests
{
    [Fact]
    public void Root_LivesUnderLocalApplicationData()
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(localAppData, "Prima"), AppPaths.Root);
    }

    [Fact]
    public void Palettes_IsRootedUnderRoot()
    {
        Assert.Equal(Path.Combine(AppPaths.Root, "palettes"), AppPaths.Palettes);
    }

    [Fact]
    public void RecentJson_IsRootedUnderRoot()
    {
        Assert.Equal(Path.Combine(AppPaths.Root, "recent.json"), AppPaths.RecentJson);
    }

    [Fact]
    public void Properties_AreCachedStableReferences()
    {
        // Lazy-cached accessors must return the same value across calls.
        Assert.Same(AppPaths.Root, AppPaths.Root);
        Assert.Same(AppPaths.Palettes, AppPaths.Palettes);
        Assert.Same(AppPaths.RecentJson, AppPaths.RecentJson);
    }

    [Fact]
    public void EnsureDirectories_CreatesRootAndPalettes()
    {
        AppPaths.EnsureDirectories();

        Assert.True(Directory.Exists(AppPaths.Root));
        Assert.True(Directory.Exists(AppPaths.Palettes));

        // Idempotent: a second call on already-existing directories is a no-op.
        AppPaths.EnsureDirectories();
        Assert.True(Directory.Exists(AppPaths.Palettes));
    }
}
