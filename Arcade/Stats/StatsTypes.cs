using System;
using Arcade.Games.Hangman;
using Arcade.Games.Minesweeper;
using Arcade.Games.Sudoku;

namespace Arcade.Stats;

[Serializable]
public sealed class AccountStatsData
{
    public HangmanAccountStatsData Hangman { get; set; } = new();
    public MinesweeperAccountStatsData Minesweeper { get; set; } = new();
    public SudokuAccountStatsData Sudoku { get; set; } = new();
}

[Serializable]
public sealed class HangmanAccountStatsData
{
    public int RoundsPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int CurrentWinStreak { get; set; }
    public int BestWinStreak { get; set; }
    public int TotalWrongGuesses { get; set; }
    public int TotalWrongGuessesOnWins { get; set; }
    public int EasyRoundsPlayed { get; set; }
    public int MediumRoundsPlayed { get; set; }
    public int HardRoundsPlayed { get; set; }
    public int AnyRoundsPlayed { get; set; }
}

[Serializable]
public sealed class MinesweeperAccountStatsData
{
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double? BestWinSeconds { get; set; }
}

[Serializable]
public sealed class SudokuAccountStatsData
{
    public int PuzzlesPlayed { get; set; }
    public int Completed { get; set; }
    public int Abandoned { get; set; }
    public int EasyPlayed { get; set; }
    public int MediumPlayed { get; set; }
    public int HardPlayed { get; set; }
    public int EasyCompleted { get; set; }
    public int MediumCompleted { get; set; }
    public int HardCompleted { get; set; }
    public double? BestEasyCompletionSeconds { get; set; }
    public double? BestMediumCompletionSeconds { get; set; }
    public double? BestHardCompletionSeconds { get; set; }
}

public interface IAccountStatsService
{
    void RecordHangmanRound(HangmanRoundSummary summary);
    void RecordMinesweeperResult(MinesweeperMatchSummary summary);
    void RecordSudokuResult(SudokuPuzzleSummary summary);
    AccountStatsData GetSnapshot();
    void Save();
}
