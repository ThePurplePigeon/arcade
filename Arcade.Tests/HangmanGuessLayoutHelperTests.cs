using Arcade.Games.Hangman;
using Xunit;

namespace Arcade.Tests;

public class HangmanGuessLayoutHelperTests
{
    [Fact]
    public void Build_LongSingleTokenShrinksBeforeSplitting()
    {
        var layout = HangmanGuessLayoutHelper.Build("SUPERCALIFRAGILISTIC", 560.0f);

        Assert.True(layout.CellSize < HangmanGuessLayoutHelper.DefaultMaxCellSize);
        Assert.Single(layout.Lines);
        Assert.Equal("SUPERCALIFRAGILISTIC", layout.Lines[0]);
    }

    [Fact]
    public void Build_MultiWordPhraseUsesFewerLinesThanFixedMaxLayout()
    {
        const string display = "SMILE BETTER SUITS A HERO";
        const float width = 420.0f;

        var fixedLayout = HangmanGuessLayoutHelper.Build(
            display,
            width,
            HangmanGuessLayoutHelper.DefaultMaxCellSize,
            HangmanGuessLayoutHelper.DefaultMaxCellSize);
        var adaptiveLayout = HangmanGuessLayoutHelper.Build(display, width);

        Assert.True(adaptiveLayout.CellSize <= fixedLayout.CellSize);
        Assert.True(adaptiveLayout.Lines.Count <= fixedLayout.Lines.Count);
    }

    [Fact]
    public void Build_NarrowWidthStillReturnsUsableLayout()
    {
        var layout = HangmanGuessLayoutHelper.Build("WARRIOR OF LIGHT", 40.0f);

        Assert.NotEmpty(layout.Lines);
        Assert.True(layout.CellSize >= HangmanGuessLayoutHelper.DefaultMinCellSize);
        Assert.True(layout.ContentHeight > 0.0f);
    }

    [Fact]
    public void Build_WhitespaceDisplayReturnsSafeSingleLineLayout()
    {
        var layout = HangmanGuessLayoutHelper.Build("   ", 280.0f);

        Assert.Single(layout.Lines);
        Assert.Equal("   ", layout.Lines[0]);
        Assert.True(layout.ContentHeight > 0.0f);
    }

    [Fact]
    public void Build_NullDisplay_IsHandledSafely()
    {
        var layout = HangmanGuessLayoutHelper.Build(null!, 280.0f);

        Assert.Single(layout.Lines);
        Assert.Equal(string.Empty, layout.Lines[0]);
        Assert.True(layout.ContentHeight > 0.0f);
    }

    [Fact]
    public void MeasureLineWidth_AccountsForSpaceGap()
    {
        var widthWithoutSpace = HangmanGuessLayoutHelper.MeasureLineWidth("AB", 20.0f, 4.0f, 12.0f);
        var widthWithSpace = HangmanGuessLayoutHelper.MeasureLineWidth("A B", 20.0f, 4.0f, 12.0f);

        Assert.True(widthWithSpace > widthWithoutSpace);
    }

}
