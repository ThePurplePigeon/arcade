using System;

namespace Arcade.Games.Minesweeper;

internal readonly record struct MinesweeperCustomSettings(int Width, int Height, int MineCount)
{
    public int MaxMineCount => MinesweeperCustomSettingsNormalizer.GetMaxMineCount(Width, Height);
}

internal static class MinesweeperCustomSettingsNormalizer
{
    public const int MinBoardSide = 5;
    public const int MaxBoardWidth = 60;
    public const int MaxBoardHeight = 40;

    public static MinesweeperCustomSettings Normalize(int width, int height, int mineCount)
    {
        var normalizedWidth = Math.Clamp(width, MinBoardSide, MaxBoardWidth);
        var normalizedHeight = Math.Clamp(height, MinBoardSide, MaxBoardHeight);
        var normalizedMines = Math.Clamp(mineCount, 0, GetMaxMineCount(normalizedWidth, normalizedHeight));
        return new MinesweeperCustomSettings(normalizedWidth, normalizedHeight, normalizedMines);
    }

    public static int GetMaxMineCount(int width, int height)
    {
        var safeWidth = Math.Clamp(width, MinBoardSide, MaxBoardWidth);
        var safeHeight = Math.Clamp(height, MinBoardSide, MaxBoardHeight);
        var boardArea = (long)safeWidth * safeHeight;
        return (int)Math.Max(0L, boardArea - 1L);
    }
}
