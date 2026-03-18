using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arcade.Games.Minesweeper;
using Xunit;

namespace Arcade.Tests;

public class MinesweeperGameTests
{
    [Fact]
    public void FirstReveal_UsesSafeThreeByThreeArea_WhenDensityAllows()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed: 7);
        var firstClick = new MinesweeperCoordinate(4, 4);

        var result = game.Reveal(firstClick);

        Assert.NotEqual(MinesweeperMoveResult.InvalidMove, result);
        Assert.False(game.Board.GetTile(firstClick).HasMine);

        for (var x = firstClick.X - 1; x <= firstClick.X + 1; x++)
        {
            for (var y = firstClick.Y - 1; y <= firstClick.Y + 1; y++)
            {
                var coordinate = new MinesweeperCoordinate(x, y);
                if (!game.Board.IsInBounds(coordinate))
                {
                    continue;
                }

                Assert.False(game.Board.GetTile(coordinate).HasMine);
            }
        }
    }

    [Fact]
    public void FirstReveal_DegradesSafeRadius_WhenBoardIsVeryDense()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(3, 3, 7), seed: 11);
        var firstClick = new MinesweeperCoordinate(1, 1);

        var result = game.Reveal(firstClick);

        Assert.NotEqual(MinesweeperMoveResult.InvalidMove, result);
        Assert.False(game.Board.GetTile(firstClick).HasMine);
        Assert.Equal(7, game.Board.GetAllCoordinates().Count(c => game.Board.GetTile(c).HasMine));
    }

    [Fact]
    public void ChordReveal_RevealsNeighbors_WhenFlagsMatch()
    {
        Assert.True(TryFindChordSuccessScenario(out var scenario));
        var game = scenario.Game;

        foreach (var mine in scenario.HiddenMines)
        {
            Assert.True(game.ToggleFlag(mine));
        }

        var revealedBefore = game.RevealedSafeTileCount;
        var result = game.ChordReveal(scenario.Target);

        Assert.Contains(result, new[] { MinesweeperMoveResult.Revealed, MinesweeperMoveResult.Won });
        Assert.True(game.RevealedSafeTileCount > revealedBefore);
        Assert.NotEqual(MinesweeperGameState.Lost, game.State);
    }

    [Fact]
    public void ChordReveal_WithIncorrectFlags_CausesLossAndMarksWrongFlags()
    {
        Assert.True(TryFindChordFailureScenario(out var scenario));
        var game = scenario.Game;
        var targetTile = game.Board.GetTile(scenario.Target);
        var requiredFlags = targetTile.AdjacentMineCount;

        var wrongSafeFlagsNeeded = Math.Max(1, requiredFlags - (scenario.HiddenMines.Length - 1));
        var mineFlagsNeeded = requiredFlags - wrongSafeFlagsNeeded;

        var flaggedCoordinates = new List<MinesweeperCoordinate>();
        flaggedCoordinates.AddRange(scenario.HiddenSafe.Take(wrongSafeFlagsNeeded));
        flaggedCoordinates.AddRange(scenario.HiddenMines.Take(mineFlagsNeeded));

        foreach (var coordinate in flaggedCoordinates)
        {
            Assert.True(game.ToggleFlag(coordinate));
        }

        var result = game.ChordReveal(scenario.Target);

        Assert.Equal(MinesweeperMoveResult.Exploded, result);
        Assert.Equal(MinesweeperGameState.Lost, game.State);
        Assert.Contains(game.Board.GetAllCoordinates(), c => game.Board.GetTile(c).WasExploded);
        Assert.Contains(game.Board.GetAllCoordinates(), c => game.Board.GetTile(c).IsWrongFlag);
    }

    [Fact]
    public void Timer_StartsOnFirstReveal_FreezesAfterLoss_AndResets()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed: 42);

        Assert.Null(game.StartedAtUtc);
        Assert.Null(game.EndedAtUtc);
        Assert.Equal(TimeSpan.Zero, game.Elapsed);

        game.Reveal(new MinesweeperCoordinate(0, 0));
        Assert.NotNull(game.StartedAtUtc);
        Assert.Null(game.EndedAtUtc);

        var mine = game.Board.GetAllCoordinates().First(c => game.Board.GetTile(c).HasMine);
        var loseResult = game.Reveal(mine);
        Assert.Equal(MinesweeperMoveResult.Exploded, loseResult);
        Assert.NotNull(game.EndedAtUtc);
        Assert.False(game.ToggleFlag(new MinesweeperCoordinate(0, 1)));
        Assert.Equal(MinesweeperMoveResult.InvalidMove, game.Reveal(new MinesweeperCoordinate(0, 1)));
        Assert.Equal(MinesweeperMoveResult.InvalidMove, game.ChordReveal(new MinesweeperCoordinate(0, 1)));

        var elapsedAfterLoss = game.Elapsed;
        Thread.Sleep(20);
        Assert.Equal(elapsedAfterLoss, game.Elapsed);

        game.Reset();
        Assert.Null(game.StartedAtUtc);
        Assert.Null(game.EndedAtUtc);
        Assert.Equal(TimeSpan.Zero, game.Elapsed);
    }

    [Fact]
    public void RemainingMinesEstimate_CanGoNegative_WhenOverFlagged()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed: 99);

        var flagsPlaced = 0;
        foreach (var coordinate in game.Board.GetAllCoordinates())
        {
            if (flagsPlaced >= 11)
            {
                break;
            }

            if (game.ToggleFlag(coordinate))
            {
                flagsPlaced++;
            }
        }

        Assert.Equal(11, flagsPlaced);
        Assert.Equal(-1, game.RemainingMinesEstimate);
    }

    [Fact]
    public void WonOrLostState_BlocksFurtherMoves()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed: 23);
        game.Reveal(new MinesweeperCoordinate(0, 0));

        foreach (var coordinate in game.Board.GetAllCoordinates())
        {
            if (!game.Board.GetTile(coordinate).HasMine)
            {
                game.Reveal(coordinate);
            }
        }

        Assert.Equal(MinesweeperGameState.Won, game.State);
        var wonElapsed = game.Elapsed;
        Thread.Sleep(20);
        Assert.Equal(wonElapsed, game.Elapsed);
        Assert.False(game.ToggleFlag(new MinesweeperCoordinate(0, 1)));
        Assert.Equal(MinesweeperMoveResult.InvalidMove, game.Reveal(new MinesweeperCoordinate(0, 1)));
        Assert.Equal(MinesweeperMoveResult.InvalidMove, game.ChordReveal(new MinesweeperCoordinate(0, 1)));
    }

    [Fact]
    public void MatchCompleted_FiresOnce_OnLoss()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed: 24);
        var eventCount = 0;
        MinesweeperMatchSummary? summary = null;
        game.MatchCompleted += value =>
        {
            eventCount++;
            summary = value;
        };

        game.Reveal(new MinesweeperCoordinate(0, 0));
        var mine = game.Board.GetAllCoordinates().First(c => game.Board.GetTile(c).HasMine);
        game.Reveal(mine);
        game.Reveal(new MinesweeperCoordinate(0, 0));

        Assert.Equal(1, eventCount);
        Assert.NotNull(summary);
        Assert.Equal(MinesweeperGameState.Lost, summary.Value.Result);
    }


    [Fact]
    public void ToggleFlag_OnRevealedTile_ReturnsFalseAndDoesNotChangeCount()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed: 25);
        var first = new MinesweeperCoordinate(0, 0);
        game.Reveal(first);

        var before = game.FlaggedTileCount;
        var changed = game.ToggleFlag(first);

        Assert.False(changed);
        Assert.Equal(before, game.FlaggedTileCount);
    }

    [Fact]
    public void ChordReveal_OnHiddenOrZeroTile_IsNoOp()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed: 26);

        var hiddenResult = game.ChordReveal(new MinesweeperCoordinate(0, 0));
        Assert.Equal(MinesweeperMoveResult.NoChange, hiddenResult);

        var foundZeroScenario = false;
        var zeroTile = default(MinesweeperCoordinate);
        MinesweeperGame? zeroGame = null;

        for (var seed = 0; seed < 1000 && !foundZeroScenario; seed++)
        {
            var candidate = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed);
            candidate.Reveal(new MinesweeperCoordinate(0, 0));

            foreach (var coordinate in candidate.Board.GetAllCoordinates())
            {
                var tile = candidate.Board.GetTile(coordinate);
                if (tile.IsRevealed && tile.AdjacentMineCount == 0 && candidate.State == MinesweeperGameState.InProgress)
                {
                    zeroGame = candidate;
                    zeroTile = coordinate;
                    foundZeroScenario = true;
                    break;
                }
            }
        }

        Assert.True(foundZeroScenario);
        Assert.NotNull(zeroGame);

        var zeroResult = zeroGame!.ChordReveal(zeroTile);
        Assert.Equal(MinesweeperMoveResult.NoChange, zeroResult);
    }

    [Fact]
    public void MatchCompleted_FiresOnce_OnWinAndSummaryMatches()
    {
        var settings = new MinesweeperGameSettings(5, 5, 0);
        var game = new MinesweeperGame(settings, seed: 27);
        var eventCount = 0;
        MinesweeperMatchSummary? summary = null;
        game.MatchCompleted += value =>
        {
            eventCount++;
            summary = value;
        };

        var result = game.Reveal(new MinesweeperCoordinate(0, 0));

        Assert.Equal(MinesweeperMoveResult.Won, result);
        Assert.Equal(MinesweeperGameState.Won, game.State);
        Assert.Equal(1, eventCount);
        Assert.NotNull(summary);
        Assert.Equal(MinesweeperGameState.Won, summary.Value.Result);
        Assert.Equal(settings.Width, summary.Value.Width);
        Assert.Equal(settings.Height, summary.Value.Height);
        Assert.Equal(settings.MineCount, summary.Value.MineCount);
        Assert.Equal(summary.Value.SafeTileCount, summary.Value.RevealedSafeTileCount);

        Assert.Equal(MinesweeperMoveResult.InvalidMove, game.Reveal(new MinesweeperCoordinate(0, 1)));
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void OutOfBoundsMoves_AreRejected()
    {
        var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed: 28);
        var outside = new MinesweeperCoordinate(-1, 0);

        Assert.Equal(MinesweeperMoveResult.InvalidMove, game.Reveal(outside));
        Assert.Equal(MinesweeperMoveResult.InvalidMove, game.ChordReveal(outside));
        Assert.False(game.ToggleFlag(outside));
    }

    [Fact]
    public void Reset_ClearsExplodedAndWrongFlagMarkers()
    {
        Assert.True(TryFindChordFailureScenario(out var scenario));
        var game = scenario.Game;
        var targetTile = game.Board.GetTile(scenario.Target);
        var requiredFlags = targetTile.AdjacentMineCount;

        var wrongSafeFlagsNeeded = Math.Max(1, requiredFlags - (scenario.HiddenMines.Length - 1));
        var mineFlagsNeeded = requiredFlags - wrongSafeFlagsNeeded;

        foreach (var coordinate in scenario.HiddenSafe.Take(wrongSafeFlagsNeeded))
        {
            game.ToggleFlag(coordinate);
        }

        foreach (var coordinate in scenario.HiddenMines.Take(mineFlagsNeeded))
        {
            game.ToggleFlag(coordinate);
        }

        Assert.Equal(MinesweeperMoveResult.Exploded, game.ChordReveal(scenario.Target));
        Assert.Contains(game.Board.GetAllCoordinates(), c => game.Board.GetTile(c).WasExploded);
        Assert.Contains(game.Board.GetAllCoordinates(), c => game.Board.GetTile(c).IsWrongFlag);

        game.Reset();

        Assert.DoesNotContain(game.Board.GetAllCoordinates(), c => game.Board.GetTile(c).WasExploded);
        Assert.DoesNotContain(game.Board.GetAllCoordinates(), c => game.Board.GetTile(c).IsWrongFlag);
        Assert.Equal(MinesweeperGameState.Ready, game.State);
    }

    private static bool TryFindChordSuccessScenario(out ChordScenario scenario)
    {
        for (var seed = 0; seed < 5000; seed++)
        {
            var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed);
            game.Reveal(new MinesweeperCoordinate(0, 0));

            foreach (var coordinate in game.Board.GetAllCoordinates())
            {
                var tile = game.Board.GetTile(coordinate);
                if (!tile.IsRevealed || tile.AdjacentMineCount <= 0)
                {
                    continue;
                }

                var hiddenNeighbors = game.Board.GetNeighbors(coordinate)
                    .Where(n => game.Board.GetTile(n).IsHidden)
                    .ToArray();
                if (hiddenNeighbors.Length == 0)
                {
                    continue;
                }

                var hiddenMines = hiddenNeighbors.Where(n => game.Board.GetTile(n).HasMine).ToArray();
                var hiddenSafe = hiddenNeighbors.Where(n => !game.Board.GetTile(n).HasMine).ToArray();
                if (hiddenMines.Length == tile.AdjacentMineCount && hiddenSafe.Length > 0)
                {
                    scenario = new ChordScenario(game, coordinate, hiddenMines, hiddenSafe);
                    return true;
                }
            }
        }

        scenario = default;
        return false;
    }

    private static bool TryFindChordFailureScenario(out ChordScenario scenario)
    {
        for (var seed = 0; seed < 5000; seed++)
        {
            var game = new MinesweeperGame(new MinesweeperGameSettings(9, 9, 10), seed);
            game.Reveal(new MinesweeperCoordinate(0, 0));

            foreach (var coordinate in game.Board.GetAllCoordinates())
            {
                var tile = game.Board.GetTile(coordinate);
                if (!tile.IsRevealed || tile.AdjacentMineCount <= 0)
                {
                    continue;
                }

                var hiddenNeighbors = game.Board.GetNeighbors(coordinate)
                    .Where(n => game.Board.GetTile(n).IsHidden)
                    .ToArray();
                if (hiddenNeighbors.Length == 0)
                {
                    continue;
                }

                var hiddenMines = hiddenNeighbors.Where(n => game.Board.GetTile(n).HasMine).ToArray();
                var hiddenSafe = hiddenNeighbors.Where(n => !game.Board.GetTile(n).HasMine).ToArray();
                if (hiddenMines.Length == 0 || hiddenSafe.Length == 0)
                {
                    continue;
                }

                var requiredFlags = tile.AdjacentMineCount;
                var wrongSafeFlagsNeeded = Math.Max(1, requiredFlags - (hiddenMines.Length - 1));
                if (hiddenSafe.Length >= wrongSafeFlagsNeeded)
                {
                    scenario = new ChordScenario(game, coordinate, hiddenMines, hiddenSafe);
                    return true;
                }
            }
        }

        scenario = default;
        return false;
    }

    private readonly record struct ChordScenario(
        MinesweeperGame Game,
        MinesweeperCoordinate Target,
        MinesweeperCoordinate[] HiddenMines,
        MinesweeperCoordinate[] HiddenSafe);
}
