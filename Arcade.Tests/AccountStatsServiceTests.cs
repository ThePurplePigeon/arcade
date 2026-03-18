using System;
using Arcade.Games.Hangman;
using Arcade.Games.Minesweeper;
using Arcade.Games.Sudoku;
using Arcade.Stats;
using Xunit;

namespace Arcade.Tests;

public class AccountStatsServiceTests
{
    [Fact]
    public void RecordHangmanRound_UpdatesLifetimeStats()
    {
        var stats = new AccountStatsData();
        var saveCalls = 0;
        var service = new AccountStatsService(stats, () => saveCalls++);

        service.RecordHangmanRound(new HangmanRoundSummary(1, HangmanGameState.Won, "A", HangmanDifficulty.Easy, 0, 6));
        service.RecordHangmanRound(new HangmanRoundSummary(2, HangmanGameState.Won, "B", HangmanDifficulty.Easy, 1, 6));
        service.RecordHangmanRound(new HangmanRoundSummary(3, HangmanGameState.Lost, "C", HangmanDifficulty.Hard, 6, 6));
        service.Save();

        var snapshot = service.GetSnapshot().Hangman;
        var averageWrongPerRound = HangmanStatsMath.GetAverageWrongGuessesPerRound(snapshot);
        var averageWrongOnWins = HangmanStatsMath.GetAverageWrongGuessesOnWins(snapshot);

        Assert.Equal(3, snapshot.RoundsPlayed);
        Assert.Equal(2, snapshot.Wins);
        Assert.Equal(1, snapshot.Losses);
        Assert.Equal(0, snapshot.CurrentWinStreak);
        Assert.Equal(2, snapshot.BestWinStreak);
        Assert.Equal(7, snapshot.TotalWrongGuesses);
        Assert.Equal(1, snapshot.TotalWrongGuessesOnWins);
        Assert.Equal(0, snapshot.MediumRoundsPlayed);
        Assert.Equal(2, snapshot.EasyRoundsPlayed);
        Assert.Equal(1, snapshot.HardRoundsPlayed);
        Assert.Equal(0, snapshot.AnyRoundsPlayed);
        Assert.NotNull(averageWrongPerRound);
        Assert.NotNull(averageWrongOnWins);
        Assert.Equal(7.0 / 3.0, averageWrongPerRound.GetValueOrDefault(), 6);
        Assert.Equal(0.5, averageWrongOnWins.GetValueOrDefault(), 6);
        Assert.Equal(1, saveCalls);
    }

    [Fact]
    public void HangmanStatsMath_ReturnsNullForEmptyStats()
    {
        var stats = new HangmanAccountStatsData();

        Assert.Null(HangmanStatsMath.GetAverageWrongGuessesPerRound(stats));
        Assert.Null(HangmanStatsMath.GetAverageWrongGuessesOnWins(stats));
    }

    [Fact]
    public void RecordMinesweeperResult_TracksWinsLossesAndFastestWin()
    {
        var stats = new AccountStatsData();
        var service = new AccountStatsService(stats, () => { });

        service.RecordMinesweeperResult(new MinesweeperMatchSummary(
            MinesweeperGameState.Won,
            9,
            9,
            10,
            71,
            71,
            10,
            TimeSpan.FromSeconds(65)));

        service.RecordMinesweeperResult(new MinesweeperMatchSummary(
            MinesweeperGameState.Won,
            9,
            9,
            10,
            71,
            71,
            11,
            TimeSpan.FromSeconds(42)));

        service.RecordMinesweeperResult(new MinesweeperMatchSummary(
            MinesweeperGameState.Lost,
            16,
            16,
            40,
            10,
            216,
            8,
            TimeSpan.FromSeconds(20)));

        var snapshot = service.GetSnapshot().Minesweeper;
        Assert.Equal(3, snapshot.GamesPlayed);
        Assert.Equal(2, snapshot.Wins);
        Assert.Equal(1, snapshot.Losses);
        Assert.Equal(42, snapshot.BestWinSeconds);
    }

    [Fact]
    public void RecordSudokuResult_TracksOutcomeCountsAndBestTimes()
    {
        var stats = new AccountStatsData();
        var service = new AccountStatsService(stats, () => { });

        service.RecordSudokuResult(new SudokuPuzzleSummary(
            SudokuPuzzleOutcome.Completed,
            "easy_1",
            SudokuDifficulty.Easy,
            TimeSpan.FromSeconds(125),
            81,
            28));
        service.RecordSudokuResult(new SudokuPuzzleSummary(
            SudokuPuzzleOutcome.Completed,
            "easy_2",
            SudokuDifficulty.Easy,
            TimeSpan.FromSeconds(98),
            81,
            30));
        service.RecordSudokuResult(new SudokuPuzzleSummary(
            SudokuPuzzleOutcome.Abandoned,
            "hard_1",
            SudokuDifficulty.Hard,
            TimeSpan.FromSeconds(40),
            43,
            24));
        service.RecordSudokuResult(new SudokuPuzzleSummary(
            SudokuPuzzleOutcome.Completed,
            "medium_1",
            SudokuDifficulty.Medium,
            TimeSpan.FromSeconds(180),
            81,
            26));
        service.RecordSudokuResult(new SudokuPuzzleSummary(
            SudokuPuzzleOutcome.Completed,
            "hard_2",
            SudokuDifficulty.Hard,
            TimeSpan.FromSeconds(240),
            81,
            24));

        var snapshot = service.GetSnapshot().Sudoku;
        Assert.Equal(5, snapshot.PuzzlesPlayed);
        Assert.Equal(4, snapshot.Completed);
        Assert.Equal(1, snapshot.Abandoned);
        Assert.Equal(2, snapshot.EasyPlayed);
        Assert.Equal(1, snapshot.MediumPlayed);
        Assert.Equal(2, snapshot.HardPlayed);
        Assert.Equal(2, snapshot.EasyCompleted);
        Assert.Equal(1, snapshot.MediumCompleted);
        Assert.Equal(1, snapshot.HardCompleted);
        Assert.Equal(98, snapshot.BestEasyCompletionSeconds);
        Assert.Equal(180, snapshot.BestMediumCompletionSeconds);
        Assert.Equal(240, snapshot.BestHardCompletionSeconds);
    }

    [Fact]
    public void Constructor_InitializesMissingStatsSections()
    {
        var stats = new AccountStatsData
        {
            Hangman = null!,
            Minesweeper = null!,
            Sudoku = null!,
        };

        var service = new AccountStatsService(stats, () => { });
        var snapshot = service.GetSnapshot();

        Assert.NotNull(snapshot.Hangman);
        Assert.NotNull(snapshot.Minesweeper);
        Assert.NotNull(snapshot.Sudoku);
    }

    [Fact]
    public void RecordHangmanRound_AnyDifficultyIncrementsAnyCounter()
    {
        var stats = new AccountStatsData();
        var service = new AccountStatsService(stats, () => { });

        service.RecordHangmanRound(new HangmanRoundSummary(1, HangmanGameState.Lost, "TEST", HangmanDifficulty.Any, 3, 6));

        var snapshot = service.GetSnapshot().Hangman;
        Assert.Equal(1, snapshot.RoundsPlayed);
        Assert.Equal(1, snapshot.AnyRoundsPlayed);
        Assert.Equal(0, snapshot.EasyRoundsPlayed);
        Assert.Equal(1, snapshot.Losses);
    }

    [Fact]
    public void RecordMinesweeperResult_LossDoesNotOverwriteBestWin()
    {
        var stats = new AccountStatsData();
        var service = new AccountStatsService(stats, () => { });

        service.RecordMinesweeperResult(new MinesweeperMatchSummary(
            MinesweeperGameState.Won,
            9,
            9,
            10,
            71,
            71,
            10,
            TimeSpan.FromSeconds(33)));

        service.RecordMinesweeperResult(new MinesweeperMatchSummary(
            MinesweeperGameState.Lost,
            9,
            9,
            10,
            20,
            71,
            5,
            TimeSpan.FromSeconds(10)));

        var snapshot = service.GetSnapshot().Minesweeper;
        Assert.Equal(33, snapshot.BestWinSeconds);
    }

    [Fact]
    public void RecordSudokuResult_AbandonDoesNotSetBestTime()
    {
        var stats = new AccountStatsData();
        var service = new AccountStatsService(stats, () => { });

        service.RecordSudokuResult(new SudokuPuzzleSummary(
            SudokuPuzzleOutcome.Abandoned,
            "medium_1",
            SudokuDifficulty.Medium,
            TimeSpan.FromSeconds(25),
            10,
            30));

        var snapshot = service.GetSnapshot().Sudoku;
        Assert.Equal(1, snapshot.PuzzlesPlayed);
        Assert.Equal(1, snapshot.Abandoned);
        Assert.Equal(0, snapshot.EasyCompleted);
        Assert.Equal(0, snapshot.MediumCompleted);
        Assert.Equal(0, snapshot.HardCompleted);
        Assert.Null(snapshot.BestMediumCompletionSeconds);
    }

}
