namespace Arcade.Games.Minesweeper;

public sealed class MinesweeperTile
{
    public bool HasMine { get; internal set; }
    public int AdjacentMineCount { get; internal set; }
    public MinesweeperTileState State { get; private set; } = MinesweeperTileState.Hidden;
    public bool WasExploded { get; private set; }
    public bool IsWrongFlag { get; private set; }

    public bool IsHidden => State == MinesweeperTileState.Hidden;
    public bool IsRevealed => State == MinesweeperTileState.Revealed;
    public bool IsFlagged => State == MinesweeperTileState.Flagged;

    public bool TryReveal()
    {
        if (!IsHidden)
        {
            return false;
        }

        State = MinesweeperTileState.Revealed;
        return true;
    }

    public bool ToggleFlag()
    {
        if (IsRevealed)
        {
            return false;
        }

        State = IsFlagged ? MinesweeperTileState.Hidden : MinesweeperTileState.Flagged;
        return true;
    }

    internal void MarkExploded()
    {
        State = MinesweeperTileState.Revealed;
        WasExploded = true;
    }

    internal void MarkWrongFlag()
    {
        IsWrongFlag = true;
    }

    internal void Reset()
    {
        HasMine = false;
        AdjacentMineCount = 0;
        State = MinesweeperTileState.Hidden;
        WasExploded = false;
        IsWrongFlag = false;
    }
}
