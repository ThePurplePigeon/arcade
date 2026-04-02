using Arcade.Games.Minesweeper;
using Xunit;

namespace Arcade.Tests;

public class MinesweeperCustomSettingsNormalizerTests
{
    [Fact]
    public void Normalize_ClampsWidthHeightAndMinesToValidRange()
    {
        var settings = MinesweeperCustomSettingsNormalizer.Normalize(-10, 999, int.MaxValue);

        Assert.Equal(MinesweeperCustomSettingsNormalizer.MinBoardSide, settings.Width);
        Assert.Equal(MinesweeperCustomSettingsNormalizer.MaxBoardHeight, settings.Height);
        Assert.Equal(settings.MaxMineCount, settings.MineCount);
    }

    [Fact]
    public void Normalize_ClampsMinesToZero_WhenNegative()
    {
        var settings = MinesweeperCustomSettingsNormalizer.Normalize(16, 16, -5);

        Assert.Equal(0, settings.MineCount);
        Assert.Equal(16, settings.Width);
        Assert.Equal(16, settings.Height);
    }

    [Fact]
    public void Normalize_ClampsMinesAfterBoardShrinks()
    {
        var largeBoard = MinesweeperCustomSettingsNormalizer.Normalize(60, 40, 2399);
        Assert.Equal(2399, largeBoard.MineCount);

        var shrunkBoard = MinesweeperCustomSettingsNormalizer.Normalize(5, 5, largeBoard.MineCount);
        Assert.Equal(24, shrunkBoard.MaxMineCount);
        Assert.Equal(24, shrunkBoard.MineCount);
    }
}
