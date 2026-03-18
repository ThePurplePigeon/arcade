using System;
using System.Collections.Generic;

namespace Arcade.Games.Hangman;

public enum HangmanDifficulty
{
    Any,
    Easy,
    Medium,
    Hard,
}

public enum HangmanGameState
{
    Ready,
    InProgress,
    Won,
    Lost,
}

public enum HangmanGuessResult
{
    Invalid,
    AlreadyGuessed,
    Correct,
    Incorrect,
    Won,
    Lost,
}

public readonly record struct HangmanWordEntry(string Text, HangmanDifficulty Difficulty);

public readonly record struct HangmanRoundSummary(
    int RoundNumber,
    HangmanGameState Result,
    string Answer,
    HangmanDifficulty Difficulty,
    int WrongGuessCount,
    int MaxWrongGuesses);

public sealed class HangmanGameSettings
{
    public HangmanGameSettings(
        int maxWrongGuesses = 6,
        bool revealNonLetterCharacters = true,
        HangmanDifficulty defaultDifficulty = HangmanDifficulty.Any)
    {
        if (maxWrongGuesses <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxWrongGuesses), "Max wrong guesses must be greater than zero.");
        }

        MaxWrongGuesses = maxWrongGuesses;
        RevealNonLetterCharacters = revealNonLetterCharacters;
        DefaultDifficulty = defaultDifficulty;
    }

    public int MaxWrongGuesses { get; }
    public bool RevealNonLetterCharacters { get; }
    public HangmanDifficulty DefaultDifficulty { get; }
}

public sealed class HangmanSessionStats
{
    public int RoundsPlayed { get; private set; }
    public int Wins { get; private set; }
    public int Losses { get; private set; }
    public int CurrentWinStreak { get; private set; }
    public int BestWinStreak { get; private set; }

    internal void RegisterWin()
    {
        RoundsPlayed++;
        Wins++;
        CurrentWinStreak++;
        if (CurrentWinStreak > BestWinStreak)
        {
            BestWinStreak = CurrentWinStreak;
        }
    }

    internal void RegisterLoss()
    {
        RoundsPlayed++;
        Losses++;
        CurrentWinStreak = 0;
    }
}

public interface IHangmanWordProvider
{
    IReadOnlyList<HangmanWordEntry> GetEntries();
}
