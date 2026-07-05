using Prima.App.Commands;
using Xunit;

namespace Prima.App.Tests;

public class FuzzyMatcherTests
{
    [Fact]
    public void Score_ExactMatch_ReturnsPositive()
    {
        var score = FuzzyMatcher.Score("Open File", "Open File");
        Assert.NotNull(score);
        Assert.True(score > 0);
    }

    [Fact]
    public void Score_NonSubsequence_ReturnsNull()
    {
        var score = FuzzyMatcher.Score("Open File", "xyz");
        Assert.Null(score);
    }

    [Fact]
    public void Score_ContiguousMatch_OutranksScattered()
    {
        var contiguous = FuzzyMatcher.Score("Export", "exp");
        var scattered = FuzzyMatcher.Score("Export", "ept");
        Assert.NotNull(contiguous);
        Assert.NotNull(scattered);
        Assert.True(contiguous > scattered);
    }

    [Fact]
    public void Score_StartOfWord_Bonus()
    {
        var startWord = FuzzyMatcher.Score("Open File", "of");
        var middle = FuzzyMatcher.Score("Open File", "pf");
        Assert.NotNull(startWord);
        Assert.NotNull(middle);
        Assert.True(startWord > middle);
    }

    [Fact]
    public void Score_CaseInsensitive()
    {
        var lower = FuzzyMatcher.Score("Open File", "open");
        var upper = FuzzyMatcher.Score("Open File", "OPEN");
        Assert.Equal(lower, upper);
    }

    [Fact]
    public void Score_EmptyQuery_ReturnsZero()
    {
        var score = FuzzyMatcher.Score("Anything", "");
        Assert.Equal(0, score);
    }

    [Fact]
    public void Score_WhitespaceQuery_ReturnsZero()
    {
        var score = FuzzyMatcher.Score("Anything", "   ");
        Assert.Equal(0, score);
    }
}
