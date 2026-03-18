using System;

namespace Arcade.Games.Minesweeper;

public readonly record struct MinesweeperCoordinate(int X, int Y);

public enum MinesweeperGameState
{
    Ready,
    InProgress,
    Won,
    Lost,
}

public enum MinesweeperMoveResult
{
    NoChange,
    Revealed,
    Exploded,
    Won,
    InvalidMove,
}

public enum MinesweeperTileState
{
    Hidden,
    Revealed,
    Flagged,
}

public readonly record struct MinesweeperMatchSummary(
    MinesweeperGameState Result,
    int Width,
    int Height,
    int MineCount,
    int RevealedSafeTileCount,
    int SafeTileCount,
    int FlaggedTileCount,
    TimeSpan Elapsed);
