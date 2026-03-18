using System;

namespace Arcade.Games.Minesweeper;

public sealed class MinesweeperGameSettings
{
    public static MinesweeperGameSettings Beginner { get; } = new(9, 9, 10);
    public static MinesweeperGameSettings Intermediate { get; } = new(16, 16, 40);
    public static MinesweeperGameSettings Expert { get; } = new(30, 16, 99);

    public MinesweeperGameSettings(int width, int height, int mineCount)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Board width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Board height must be greater than zero.");
        }

        if (mineCount < 0 || mineCount >= width * height)
        {
            throw new ArgumentOutOfRangeException(nameof(mineCount), "Mine count must be between 0 and board area - 1.");
        }

        Width = width;
        Height = height;
        MineCount = mineCount;
    }

    public int Width { get; }
    public int Height { get; }
    public int MineCount { get; }
}
