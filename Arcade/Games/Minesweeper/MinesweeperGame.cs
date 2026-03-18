using System;
using System.Collections.Generic;

namespace Arcade.Games.Minesweeper;

public sealed class MinesweeperGame
{
    private const int FirstRevealSafeRadius = 1;

    private readonly Random random;
    private bool minesPlaced;

    public MinesweeperGame(MinesweeperGameSettings settings, int? seed = null)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        random = seed.HasValue ? new Random(seed.Value) : new Random();

        Board = new MinesweeperBoard(settings);
        Reset();
    }

    public MinesweeperGameSettings Settings { get; }
    public MinesweeperBoard Board { get; }
    public MinesweeperGameState State { get; private set; }
    public int RevealedSafeTileCount { get; private set; }
    public int FlaggedTileCount { get; private set; }
    public int RemainingMinesEstimate => Settings.MineCount - FlaggedTileCount;
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public TimeSpan Elapsed
    {
        get
        {
            if (StartedAtUtc is null)
            {
                return TimeSpan.Zero;
            }

            var endedAt = EndedAtUtc ?? DateTime.UtcNow;
            return endedAt - StartedAtUtc.Value;
        }
    }

    public event Action<MinesweeperMatchSummary>? MatchCompleted;

    public void Reset()
    {
        Board.Clear();
        minesPlaced = false;
        State = MinesweeperGameState.Ready;
        RevealedSafeTileCount = 0;
        FlaggedTileCount = 0;
        StartedAtUtc = null;
        EndedAtUtc = null;
    }

    public MinesweeperMoveResult Reveal(MinesweeperCoordinate coordinate)
    {
        if (!Board.IsInBounds(coordinate))
        {
            return MinesweeperMoveResult.InvalidMove;
        }

        if (State is MinesweeperGameState.Won or MinesweeperGameState.Lost)
        {
            return MinesweeperMoveResult.InvalidMove;
        }

        var selectedTile = Board.GetTile(coordinate);
        if (selectedTile.IsRevealed || selectedTile.IsFlagged)
        {
            return MinesweeperMoveResult.NoChange;
        }

        EnsureBoardInitialized(coordinate);
        selectedTile = Board.GetTile(coordinate);

        if (selectedTile.HasMine)
        {
            LoseAtCoordinate(coordinate);
            return MinesweeperMoveResult.Exploded;
        }

        var changed = RevealOpenArea(coordinate);
        if (!changed)
        {
            return MinesweeperMoveResult.NoChange;
        }

        if (TryTransitionToWon())
        {
            return MinesweeperMoveResult.Won;
        }

        return MinesweeperMoveResult.Revealed;
    }

    public MinesweeperMoveResult ChordReveal(MinesweeperCoordinate coordinate)
    {
        if (!Board.IsInBounds(coordinate))
        {
            return MinesweeperMoveResult.InvalidMove;
        }

        if (State is MinesweeperGameState.Won or MinesweeperGameState.Lost)
        {
            return MinesweeperMoveResult.InvalidMove;
        }

        var centerTile = Board.GetTile(coordinate);
        if (!centerTile.IsRevealed || centerTile.AdjacentMineCount <= 0)
        {
            return MinesweeperMoveResult.NoChange;
        }

        if (!minesPlaced)
        {
            return MinesweeperMoveResult.NoChange;
        }

        if (CountFlaggedNeighbors(coordinate) != centerTile.AdjacentMineCount)
        {
            return MinesweeperMoveResult.NoChange;
        }

        var revealCandidates = new MinesweeperCoordinate[8];
        var revealCandidateCount = 0;
        MinesweeperCoordinate? explodedCoordinate = null;
        var hadHiddenNeighbor = false;

        GetNeighborBounds(coordinate, out var minX, out var maxX, out var minY, out var maxY);
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (x == coordinate.X && y == coordinate.Y)
                {
                    continue;
                }

                var neighborCoordinate = new MinesweeperCoordinate(x, y);
                var neighborTile = Board.GetTile(neighborCoordinate);
                if (!neighborTile.IsHidden)
                {
                    continue;
                }

                hadHiddenNeighbor = true;

                if (neighborTile.HasMine)
                {
                    explodedCoordinate = neighborCoordinate;
                    continue;
                }

                revealCandidates[revealCandidateCount++] = neighborCoordinate;
            }
        }

        if (!hadHiddenNeighbor)
        {
            return MinesweeperMoveResult.NoChange;
        }

        if (explodedCoordinate.HasValue)
        {
            LoseAtCoordinate(explodedCoordinate.Value);
            return MinesweeperMoveResult.Exploded;
        }

        var changed = false;
        for (var i = 0; i < revealCandidateCount; i++)
        {
            changed |= RevealOpenArea(revealCandidates[i]);
        }

        if (!changed)
        {
            return MinesweeperMoveResult.NoChange;
        }

        if (TryTransitionToWon())
        {
            return MinesweeperMoveResult.Won;
        }

        return MinesweeperMoveResult.Revealed;
    }

    public bool ToggleFlag(MinesweeperCoordinate coordinate)
    {
        if (!Board.IsInBounds(coordinate))
        {
            return false;
        }

        if (State is MinesweeperGameState.Won or MinesweeperGameState.Lost)
        {
            return false;
        }

        var tile = Board.GetTile(coordinate);
        var wasFlagged = tile.IsFlagged;
        if (!tile.ToggleFlag())
        {
            return false;
        }

        if (tile.IsFlagged && !wasFlagged)
        {
            FlaggedTileCount++;
        }
        else if (!tile.IsFlagged && wasFlagged)
        {
            FlaggedTileCount--;
        }

        return true;
    }

    private void EnsureBoardInitialized(MinesweeperCoordinate firstReveal)
    {
        if (minesPlaced)
        {
            return;
        }

        Board.PlaceMines(random, firstReveal, FirstRevealSafeRadius);
        minesPlaced = true;
        State = MinesweeperGameState.InProgress;
        StartedAtUtc ??= DateTime.UtcNow;
    }

    private bool RevealOpenArea(MinesweeperCoordinate start)
    {
        var board = Board;
        var queued = new bool[board.Width, board.Height];
        var queue = new Queue<MinesweeperCoordinate>();
        queue.Enqueue(start);
        queued[start.X, start.Y] = true;

        var changed = false;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var tile = board.GetTile(current);

            if (!tile.TryReveal())
            {
                continue;
            }

            changed = true;
            RevealedSafeTileCount++;
            if (tile.AdjacentMineCount != 0)
            {
                continue;
            }

            GetNeighborBounds(current, out var minX, out var maxX, out var minY, out var maxY);
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    if (x == current.X && y == current.Y)
                    {
                        continue;
                    }

                    if (queued[x, y])
                    {
                        continue;
                    }

                    var neighbor = new MinesweeperCoordinate(x, y);
                    var neighborTile = board.GetTile(neighbor);
                    if (neighborTile.IsHidden && !neighborTile.HasMine && !neighborTile.IsFlagged)
                    {
                        queued[x, y] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return changed;
    }

    private int CountFlaggedNeighbors(MinesweeperCoordinate coordinate)
    {
        var count = 0;
        GetNeighborBounds(coordinate, out var minX, out var maxX, out var minY, out var maxY);
        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (x == coordinate.X && y == coordinate.Y)
                {
                    continue;
                }

                if (Board.GetTile(new MinesweeperCoordinate(x, y)).IsFlagged)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void LoseAtCoordinate(MinesweeperCoordinate explodedCoordinate)
    {
        var explodedTile = Board.GetTile(explodedCoordinate);
        explodedTile.MarkExploded();

        foreach (var coordinate in Board.GetAllCoordinates())
        {
            var tile = Board.GetTile(coordinate);
            if (tile.IsFlagged && !tile.HasMine)
            {
                tile.MarkWrongFlag();
            }
        }

        State = MinesweeperGameState.Lost;
        EndedAtUtc ??= DateTime.UtcNow;
        MatchCompleted?.Invoke(BuildMatchSummary(MinesweeperGameState.Lost));
    }

    private bool TryTransitionToWon()
    {
        if (RevealedSafeTileCount < Board.SafeTileCount)
        {
            return false;
        }

        State = MinesweeperGameState.Won;
        EndedAtUtc ??= DateTime.UtcNow;
        MatchCompleted?.Invoke(BuildMatchSummary(MinesweeperGameState.Won));
        return true;
    }

    private void GetNeighborBounds(MinesweeperCoordinate coordinate, out int minX, out int maxX, out int minY, out int maxY)
    {
        minX = Math.Max(0, coordinate.X - 1);
        maxX = Math.Min(Board.Width - 1, coordinate.X + 1);
        minY = Math.Max(0, coordinate.Y - 1);
        maxY = Math.Min(Board.Height - 1, coordinate.Y + 1);
    }

    private MinesweeperMatchSummary BuildMatchSummary(MinesweeperGameState result)
    {
        return new MinesweeperMatchSummary(
            result,
            Board.Width,
            Board.Height,
            Settings.MineCount,
            RevealedSafeTileCount,
            Board.SafeTileCount,
            FlaggedTileCount,
            Elapsed);
    }
}
