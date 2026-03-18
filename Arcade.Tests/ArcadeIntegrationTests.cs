using System;
using System.Collections.Generic;
using Arcade.Games.Hangman;
using Arcade.Games.Minesweeper;
using Arcade.Games.Sudoku;
using Arcade.Stats;
using Xunit;

namespace Arcade.Tests;

public class ArcadeIntegrationTests
{
    private const string SudokuSolution = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
    private const string SudokuOneBlankGivens = "534678912672195348198342567859761423426853791713924856961537284287419635345286170";

    [Fact]
    public void CrossModuleCompletionEvents_UpdateSharedAccountStats()
    {
        var stats = new AccountStatsData();
        var service = new AccountStatsService(stats, () => { });

        var hangman = new HangmanGame(
            new FixedHangmanProvider(new HangmanWordEntry("A", HangmanDifficulty.Easy)),
            new HangmanGameSettings(defaultDifficulty: HangmanDifficulty.Easy),
            seed: 201);
        hangman.RoundCompleted += service.RecordHangmanRound;
        Assert.Equal(HangmanGuessResult.Won, hangman.Guess('A'));

        var minesweeper = new MinesweeperGame(new MinesweeperGameSettings(4, 4, 0), seed: 202);
        minesweeper.MatchCompleted += service.RecordMinesweeperResult;
        Assert.Equal(MinesweeperMoveResult.Won, minesweeper.Reveal(new MinesweeperCoordinate(0, 0)));

        var sudoku = new SudokuGame(
            new FixedSudokuProvider(new SudokuPuzzle("integration", SudokuDifficulty.Medium, SudokuOneBlankGivens, SudokuSolution)),
            new SudokuGameSettings(SudokuDifficulty.Medium),
            seed: 203);
        sudoku.PuzzleEnded += service.RecordSudokuResult;
        Assert.Equal(SudokuMoveResult.Completed, sudoku.SetCellValue(new SudokuCoordinate(8, 8), 9));

        var snapshot = service.GetSnapshot();
        Assert.Equal(1, snapshot.Hangman.RoundsPlayed);
        Assert.Equal(1, snapshot.Hangman.Wins);

        Assert.Equal(1, snapshot.Minesweeper.GamesPlayed);
        Assert.Equal(1, snapshot.Minesweeper.Wins);

        Assert.Equal(1, snapshot.Sudoku.PuzzlesPlayed);
        Assert.Equal(1, snapshot.Sudoku.Completed);
        Assert.Equal(0, snapshot.Sudoku.Abandoned);
    }

    private sealed class FixedHangmanProvider : IHangmanWordProvider
    {
        private readonly IReadOnlyList<HangmanWordEntry> entries;

        public FixedHangmanProvider(params HangmanWordEntry[] entries)
        {
            this.entries = entries;
        }

        public IReadOnlyList<HangmanWordEntry> GetEntries() => entries;
    }

    private sealed class FixedSudokuProvider : ISudokuPuzzleProvider
    {
        private readonly IReadOnlyList<SudokuPuzzle> puzzles;

        public FixedSudokuProvider(params SudokuPuzzle[] puzzles)
        {
            this.puzzles = puzzles;
        }

        public IReadOnlyList<SudokuPuzzle> GetPuzzles() => puzzles;
    }
}
