using System;
using System.Collections.Generic;
using Arcade.Games.Sudoku;
using Xunit;

namespace Arcade.Tests;

public class SudokuGameTests
{
    private const string Solution = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
    private const string BlankGivens = "000000000000000000000000000000000000000000000000000000000000000000000000000000000";
    private const string OneBlankGivens = "534678912672195348198342567859761423426853791713924856961537284287419635345286170";
    private const string AlternateSolutionOne = "645789123783216459219453678961872534537964812824135967172648395398521746456397281";

    [Fact]
    public void Constructor_StartsReady()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("ready", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 1);

        Assert.Equal(SudokuGameState.Ready, game.State);
        Assert.Equal("ready", game.CurrentPuzzle.Id);
        Assert.Equal(SudokuDifficulty.Any, game.SelectedDifficulty);
    }

    [Fact]
    public void FirstValueEntry_StartsTimerAndMovesToInProgress()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("value", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 2);

        var result = game.SetCellValue(new SudokuCoordinate(0, 0), 5);

        Assert.Equal(SudokuMoveResult.ValueSet, result);
        Assert.Equal(SudokuGameState.InProgress, game.State);
        Assert.NotNull(game.StartedAtUtc);
    }

    [Fact]
    public void FirstNoteToggle_StartsTimerAndMovesToInProgress()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("note", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 3);

        var result = game.ToggleNote(new SudokuCoordinate(0, 0), 5);

        Assert.Equal(SudokuMoveResult.NoteAdded, result);
        Assert.Equal(SudokuGameState.InProgress, game.State);
        Assert.NotNull(game.StartedAtUtc);
    }

    [Fact]
    public void GivenCells_RejectEditsAndNotes()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("givens", SudokuDifficulty.Easy, OneBlankGivens, Solution)),
            seed: 4);

        Assert.Equal(SudokuMoveResult.InvalidMove, game.SetCellValue(new SudokuCoordinate(0, 0), 1));
        Assert.Equal(SudokuMoveResult.InvalidMove, game.ToggleNote(new SudokuCoordinate(0, 0), 1));
    }

    [Fact]
    public void SettingValue_ClearsExistingNotes()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("notes", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 5);
        var coordinate = new SudokuCoordinate(0, 0);

        Assert.Equal(SudokuMoveResult.NoteAdded, game.ToggleNote(coordinate, 5));
        Assert.NotEqual(0, game.Board.GetNoteMask(coordinate));

        Assert.Equal(SudokuMoveResult.ValueSet, game.SetCellValue(coordinate, 5));
        Assert.Equal(0, game.Board.GetNoteMask(coordinate));
    }

    [Fact]
    public void CompletingPuzzle_FiresCompletedExactlyOnce()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("complete", SudokuDifficulty.Easy, OneBlankGivens, Solution)),
            seed: 6);

        var eventCount = 0;
        SudokuPuzzleSummary? summary = null;
        game.PuzzleEnded += ended =>
        {
            eventCount++;
            summary = ended;
        };

        Assert.Equal(SudokuMoveResult.Completed, game.SetCellValue(new SudokuCoordinate(8, 8), 9));
        Assert.Equal(SudokuMoveResult.InvalidMove, game.SetCellValue(new SudokuCoordinate(8, 8), 9));

        Assert.Equal(1, eventCount);
        Assert.NotNull(summary);
        Assert.Equal(SudokuPuzzleOutcome.Completed, summary.Value.Outcome);
        Assert.Equal("complete", summary.Value.PuzzleId);
    }

    [Fact]
    public void StartNewPuzzle_WithProgress_FiresAbandoned()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("a", SudokuDifficulty.Easy, BlankGivens, Solution),
            new SudokuPuzzle("b", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 7);

        var eventCount = 0;
        SudokuPuzzleSummary? summary = null;
        game.PuzzleEnded += ended =>
        {
            eventCount++;
            summary = ended;
        };

        game.SetCellValue(new SudokuCoordinate(0, 0), 5);
        game.StartNewPuzzle();

        Assert.Equal(1, eventCount);
        Assert.NotNull(summary);
        Assert.Equal(SudokuPuzzleOutcome.Abandoned, summary.Value.Outcome);
        Assert.Equal(SudokuGameState.Ready, game.State);
    }

    [Fact]
    public void ResetCurrentPuzzle_DoesNotFireAbandoned()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("reset", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 8);

        var eventCount = 0;
        game.PuzzleEnded += _ => eventCount++;

        game.SetCellValue(new SudokuCoordinate(0, 0), 5);
        game.ResetCurrentPuzzle();

        Assert.Equal(0, eventCount);
        Assert.Equal(SudokuGameState.Ready, game.State);
        Assert.False(game.HasProgress);
        Assert.Equal(0, game.Board.GetValue(new SudokuCoordinate(0, 0)));
    }

    [Fact]
    public void DifficultyPool_DoesNotRepeatBeforeExhaustion()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("easy_1", SudokuDifficulty.Easy, BlankGivens, Solution),
            new SudokuPuzzle("easy_2", SudokuDifficulty.Easy, BlankGivens, "645789123783216459219453678961872534537964812824135967172648395398521746456397281"),
            new SudokuPuzzle("easy_3", SudokuDifficulty.Easy, BlankGivens, "756891234894327561321564789172983645648175923935246178283759416419632857567418392")),
            new SudokuGameSettings(SudokuDifficulty.Easy),
            seed: 9);

        var seen = new HashSet<string>(StringComparer.Ordinal) { game.CurrentPuzzle.Id };
        game.StartNewPuzzle();
        seen.Add(game.CurrentPuzzle.Id);
        game.StartNewPuzzle();
        seen.Add(game.CurrentPuzzle.Id);

        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void NewPuzzle_DoesNotImmediatelyPickRotationOfPrevious_WhenAlternativeExists()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
                new SudokuPuzzle("base_easy", SudokuDifficulty.Easy, BlankGivens, Solution),
                new SudokuPuzzle("base_easy_rot90", SudokuDifficulty.Easy, BlankGivens, Solution, "base_easy", true),
                new SudokuPuzzle("other_easy", SudokuDifficulty.Easy, BlankGivens, AlternateSolutionOne)),
            new SudokuGameSettings(SudokuDifficulty.Easy),
            seed: 109);

        var previousGroup = game.CurrentPuzzle.SelectionGroupTag;
        for (var iteration = 0; iteration < 20; iteration++)
        {
            game.StartNewPuzzle();
            Assert.NotEqual(previousGroup, game.CurrentPuzzle.SelectionGroupTag);
            previousGroup = game.CurrentPuzzle.SelectionGroupTag;
        }
    }

    [Fact]
    public void NewPuzzle_WhenOnlyOneSelectionGroup_AllowsRotationVariants()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
                new SudokuPuzzle("solo_base", SudokuDifficulty.Easy, BlankGivens, Solution),
                new SudokuPuzzle("solo_base_rot90", SudokuDifficulty.Easy, BlankGivens, Solution, "solo_base", true),
                new SudokuPuzzle("solo_base_rot180", SudokuDifficulty.Easy, BlankGivens, AlternateSolutionOne, "solo_base", true)),
            new SudokuGameSettings(SudokuDifficulty.Easy),
            seed: 207);

        for (var iteration = 0; iteration < 20; iteration++)
        {
            Assert.Equal("solo_base", game.CurrentPuzzle.SelectionGroupTag);
            game.StartNewPuzzle();
        }
    }

    [Fact]
    public void NewPuzzle_RotationGuard_DoesNotStarveVariantsAcrossRuns()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
                new SudokuPuzzle("base_a", SudokuDifficulty.Easy, BlankGivens, Solution),
                new SudokuPuzzle("base_a_rot90", SudokuDifficulty.Easy, BlankGivens, Solution, "base_a", true),
                new SudokuPuzzle("base_a_rot180", SudokuDifficulty.Easy, BlankGivens, AlternateSolutionOne, "base_a", true),
                new SudokuPuzzle("base_b", SudokuDifficulty.Easy, BlankGivens, "756891234894327561321564789172983645648175923935246178283759416419632857567418392")),
            new SudokuGameSettings(SudokuDifficulty.Easy),
            seed: 305);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var iteration = 0; iteration < 40; iteration++)
        {
            seen.Add(game.CurrentPuzzle.Id);
            game.StartNewPuzzle();
        }

        Assert.Contains("base_a", seen);
        Assert.Contains("base_a_rot90", seen);
        Assert.Contains("base_a_rot180", seen);
        Assert.Contains("base_b", seen);
    }


    [Fact]
    public void SetDifficulty_WithoutStartingPuzzle_OnlyChangesSelection()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
                new SudokuPuzzle("easy", SudokuDifficulty.Easy, BlankGivens, Solution),
                new SudokuPuzzle("hard", SudokuDifficulty.Hard, BlankGivens, Solution)),
            new SudokuGameSettings(SudokuDifficulty.Easy),
            seed: 10);
        var currentPuzzleId = game.CurrentPuzzle.Id;

        game.SetDifficulty(SudokuDifficulty.Hard, startNewPuzzle: false);

        Assert.Equal(SudokuDifficulty.Hard, game.SelectedDifficulty);
        Assert.Equal(currentPuzzleId, game.CurrentPuzzle.Id);
        Assert.Equal(SudokuGameState.Ready, game.State);

        game.StartNewPuzzle();
        Assert.Equal("hard", game.CurrentPuzzle.Id);
    }

    [Fact]
    public void SetDifficulty_WhenNoEligiblePuzzles_FallsBackToAll()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
                new SudokuPuzzle("easy_a", SudokuDifficulty.Easy, BlankGivens, Solution),
                new SudokuPuzzle("easy_b", SudokuDifficulty.Easy, BlankGivens, AlternateSolutionOne)),
            new SudokuGameSettings(SudokuDifficulty.Hard),
            seed: 11);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 8; i++)
        {
            seen.Add(game.CurrentPuzzle.Id);
            game.StartNewPuzzle();
        }

        Assert.Equal(SudokuDifficulty.Hard, game.SelectedDifficulty);
        Assert.Equal(2, seen.Count);
        Assert.Contains("easy_a", seen);
        Assert.Contains("easy_b", seen);
    }

    [Fact]
    public void ToggleNote_InvalidDigit_ReturnsInvalidMove()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("invalid_note", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 12);

        Assert.Equal(SudokuMoveResult.InvalidMove, game.ToggleNote(new SudokuCoordinate(0, 0), 0));
        Assert.Equal(SudokuMoveResult.InvalidMove, game.ToggleNote(new SudokuCoordinate(0, 0), 10));
        Assert.Equal(SudokuGameState.Ready, game.State);
    }

    [Fact]
    public void ClearNotes_WithoutExistingNotes_DoesNotStartTimer()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("clear_notes", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 13);

        game.ClearNotes(new SudokuCoordinate(0, 0));

        Assert.Null(game.StartedAtUtc);
        Assert.Equal(SudokuGameState.Ready, game.State);
    }

    [Fact]
    public void Elapsed_FreezesAfterCompletion()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("elapsed", SudokuDifficulty.Easy, OneBlankGivens, Solution)),
            seed: 14);

        Assert.Equal(SudokuMoveResult.Completed, game.SetCellValue(new SudokuCoordinate(8, 8), 9));
        var elapsedAfterCompletion = game.Elapsed;

        System.Threading.Thread.Sleep(20);

        Assert.Equal(SudokuGameState.Completed, game.State);
        Assert.Equal(elapsedAfterCompletion, game.Elapsed);
        Assert.NotNull(game.EndedAtUtc);
    }

    [Fact]
    public void ResetCurrentPuzzle_ClearsTimerState()
    {
        var game = new SudokuGame(new FixedPuzzleProvider(
            new SudokuPuzzle("reset_timer", SudokuDifficulty.Easy, BlankGivens, Solution)),
            seed: 15);

        game.SetCellValue(new SudokuCoordinate(0, 0), 5);
        Assert.NotNull(game.StartedAtUtc);

        game.ResetCurrentPuzzle();

        Assert.Null(game.StartedAtUtc);
        Assert.Null(game.EndedAtUtc);
        Assert.Equal(TimeSpan.Zero, game.Elapsed);
        Assert.Equal(SudokuGameState.Ready, game.State);
    }

    private sealed class FixedPuzzleProvider : ISudokuPuzzleProvider
    {
        private readonly IReadOnlyList<SudokuPuzzle> puzzles;

        public FixedPuzzleProvider(params SudokuPuzzle[] puzzles)
        {
            this.puzzles = puzzles;
        }

        public IReadOnlyList<SudokuPuzzle> GetPuzzles() => puzzles;
    }
}
