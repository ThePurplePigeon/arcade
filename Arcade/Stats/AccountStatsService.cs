using System;
using Arcade.Games.Hangman;
using Arcade.Games.Minesweeper;
using Arcade.Games.Sudoku;

namespace Arcade.Stats;

public sealed class AccountStatsService : IAccountStatsService
{
    private readonly AccountStatsData stats;
    private readonly Action saveAction;

    public AccountStatsService(AccountStatsData stats, Action saveAction)
    {
        this.stats = stats ?? throw new ArgumentNullException(nameof(stats));
        this.saveAction = saveAction ?? throw new ArgumentNullException(nameof(saveAction));
        this.stats.Hangman ??= new HangmanAccountStatsData();
        this.stats.Minesweeper ??= new MinesweeperAccountStatsData();
        this.stats.Sudoku ??= new SudokuAccountStatsData();
    }

    public void RecordHangmanRound(HangmanRoundSummary summary)
    {
        var hangman = stats.Hangman;
        hangman.RoundsPlayed++;
        hangman.TotalWrongGuesses += summary.WrongGuessCount;

        switch (summary.Difficulty)
        {
            case HangmanDifficulty.Easy:
                hangman.EasyRoundsPlayed++;
                break;
            case HangmanDifficulty.Medium:
                hangman.MediumRoundsPlayed++;
                break;
            case HangmanDifficulty.Hard:
                hangman.HardRoundsPlayed++;
                break;
            default:
                hangman.AnyRoundsPlayed++;
                break;
        }

        if (summary.Result == HangmanGameState.Won)
        {
            hangman.Wins++;
            hangman.CurrentWinStreak++;
            hangman.TotalWrongGuessesOnWins += summary.WrongGuessCount;
            if (hangman.CurrentWinStreak > hangman.BestWinStreak)
            {
                hangman.BestWinStreak = hangman.CurrentWinStreak;
            }
        }
        else
        {
            hangman.Losses++;
            hangman.CurrentWinStreak = 0;
        }
    }

    public void RecordMinesweeperResult(MinesweeperMatchSummary summary)
    {
        var minesweeper = stats.Minesweeper;
        minesweeper.GamesPlayed++;

        if (summary.Result == MinesweeperGameState.Won)
        {
            minesweeper.Wins++;
            var totalSeconds = summary.Elapsed.TotalSeconds;
            if (!minesweeper.BestWinSeconds.HasValue || totalSeconds < minesweeper.BestWinSeconds.Value)
            {
                minesweeper.BestWinSeconds = totalSeconds;
            }
        }
        else
        {
            minesweeper.Losses++;
        }
    }

    public void RecordSudokuResult(SudokuPuzzleSummary summary)
    {
        var sudoku = stats.Sudoku;
        sudoku.PuzzlesPlayed++;

        switch (summary.Difficulty)
        {
            case SudokuDifficulty.Easy:
                sudoku.EasyPlayed++;
                break;
            case SudokuDifficulty.Medium:
                sudoku.MediumPlayed++;
                break;
            case SudokuDifficulty.Hard:
                sudoku.HardPlayed++;
                break;
        }

        if (summary.Outcome == SudokuPuzzleOutcome.Completed)
        {
            sudoku.Completed++;
            var totalSeconds = summary.Elapsed.TotalSeconds;
            switch (summary.Difficulty)
            {
                case SudokuDifficulty.Easy:
                    sudoku.EasyCompleted++;
                    if (!sudoku.BestEasyCompletionSeconds.HasValue || totalSeconds < sudoku.BestEasyCompletionSeconds.Value)
                    {
                        sudoku.BestEasyCompletionSeconds = totalSeconds;
                    }

                    break;
                case SudokuDifficulty.Medium:
                    sudoku.MediumCompleted++;
                    if (!sudoku.BestMediumCompletionSeconds.HasValue || totalSeconds < sudoku.BestMediumCompletionSeconds.Value)
                    {
                        sudoku.BestMediumCompletionSeconds = totalSeconds;
                    }

                    break;
                case SudokuDifficulty.Hard:
                    sudoku.HardCompleted++;
                    if (!sudoku.BestHardCompletionSeconds.HasValue || totalSeconds < sudoku.BestHardCompletionSeconds.Value)
                    {
                        sudoku.BestHardCompletionSeconds = totalSeconds;
                    }

                    break;
            }
        }
        else
        {
            sudoku.Abandoned++;
        }
    }

    public AccountStatsData GetSnapshot()
    {
        return stats;
    }

    public void Save()
    {
        saveAction();
    }
}
