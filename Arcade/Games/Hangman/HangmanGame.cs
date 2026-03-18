using System;
using System.Collections.Generic;

namespace Arcade.Games.Hangman;

public sealed class HangmanGame
{
    private readonly Random random;
    private readonly IReadOnlyList<HangmanWordEntry> entries;
    private readonly Dictionary<HangmanDifficulty, DifficultyPoolState> pools = [];
    private readonly HashSet<char> guessedLetters = [];
    private readonly HashSet<char> wrongLetters = [];
    private readonly HashSet<char> unresolvedLetters = [];
    private string cachedMaskedEntry = string.Empty;
    private bool isMaskedEntryDirty = true;

    public HangmanGame(IHangmanWordProvider wordProvider, HangmanGameSettings? settings = null, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(wordProvider);
        Settings = settings ?? new HangmanGameSettings();
        entries = wordProvider.GetEntries();
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Hangman game cannot start with an empty word list.");
        }

        random = seed.HasValue ? new Random(seed.Value) : new Random();
        SessionStats = new HangmanSessionStats();
        BuildDifficultyPools();

        SelectedDifficulty = NormalizeDifficulty(Settings.DefaultDifficulty);
        StartNewRound();
    }

    public HangmanGameSettings Settings { get; }
    public HangmanSessionStats SessionStats { get; }
    public HangmanGameState State { get; private set; }
    public HangmanDifficulty SelectedDifficulty { get; private set; }
    public string CurrentEntry { get; private set; } = string.Empty;
    public int RoundNumber { get; private set; }
    public HangmanRoundSummary? LastRoundSummary { get; private set; }
    public int WrongGuessCount => wrongLetters.Count;
    public int RemainingGuesses => Settings.MaxWrongGuesses - WrongGuessCount;
    public IReadOnlyCollection<char> GuessedLetters => guessedLetters;
    public IReadOnlyCollection<char> WrongLetters => wrongLetters;

    public event Action<HangmanRoundSummary>? RoundCompleted;

    public string MaskedEntry
    {
        get
        {
            if (isMaskedEntryDirty)
            {
                RebuildMaskedEntry();
            }

            return cachedMaskedEntry;
        }
    }

    public void SetDifficulty(HangmanDifficulty difficulty, bool startNewRound = true)
    {
        var normalized = NormalizeDifficulty(difficulty);
        if (SelectedDifficulty == normalized && !startNewRound)
        {
            return;
        }

        SelectedDifficulty = normalized;
        if (startNewRound)
        {
            StartNewRound();
        }
    }

    public void StartNewRound()
    {
        var index = DrawNextEntryIndex(SelectedDifficulty);
        CurrentEntry = entries[index].Text;
        guessedLetters.Clear();
        wrongLetters.Clear();
        unresolvedLetters.Clear();
        PopulateUnresolvedLetters(CurrentEntry, unresolvedLetters);
        cachedMaskedEntry = string.Empty;
        isMaskedEntryDirty = true;
        State = HangmanGameState.Ready;
        RoundNumber++;
    }

    public HangmanGuessResult Guess(char guess)
    {
        if (State is HangmanGameState.Won or HangmanGameState.Lost)
        {
            return HangmanGuessResult.Invalid;
        }

        if (string.IsNullOrEmpty(CurrentEntry))
        {
            return HangmanGuessResult.Invalid;
        }

        var normalizedGuess = NormalizeGuess(guess);
        if (normalizedGuess == '\0')
        {
            return HangmanGuessResult.Invalid;
        }

        if (guessedLetters.Contains(normalizedGuess))
        {
            return HangmanGuessResult.AlreadyGuessed;
        }

        if (State == HangmanGameState.Ready)
        {
            State = HangmanGameState.InProgress;
        }

        guessedLetters.Add(normalizedGuess);
        isMaskedEntryDirty = true;

        if (CurrentEntry.IndexOf(normalizedGuess, StringComparison.Ordinal) >= 0)
        {
            unresolvedLetters.Remove(normalizedGuess);
            if (unresolvedLetters.Count == 0)
            {
                CompleteRound(HangmanGameState.Won);
                return HangmanGuessResult.Won;
            }

            return HangmanGuessResult.Correct;
        }

        wrongLetters.Add(normalizedGuess);
        if (wrongLetters.Count >= Settings.MaxWrongGuesses)
        {
            CompleteRound(HangmanGameState.Lost);
            return HangmanGuessResult.Lost;
        }

        return HangmanGuessResult.Incorrect;
    }

    public bool IsLetterAvailable(char letter)
    {
        var normalized = NormalizeGuess(letter);
        return normalized != '\0' && !guessedLetters.Contains(normalized);
    }

    public bool IsLetterGuessed(char letter)
    {
        var normalized = NormalizeGuess(letter);
        return normalized != '\0' && guessedLetters.Contains(normalized);
    }

    public bool IsLetterWrong(char letter)
    {
        var normalized = NormalizeGuess(letter);
        return normalized != '\0' && wrongLetters.Contains(normalized);
    }

    private void CompleteRound(HangmanGameState result)
    {
        State = result;
        if (result == HangmanGameState.Won)
        {
            SessionStats.RegisterWin();
        }
        else
        {
            SessionStats.RegisterLoss();
        }

        var summary = new HangmanRoundSummary(
            RoundNumber,
            result,
            CurrentEntry,
            SelectedDifficulty,
            WrongGuessCount,
            Settings.MaxWrongGuesses);

        LastRoundSummary = summary;
        RoundCompleted?.Invoke(summary);
    }

    private void RebuildMaskedEntry()
    {
        if (string.IsNullOrEmpty(CurrentEntry))
        {
            cachedMaskedEntry = string.Empty;
            isMaskedEntryDirty = false;
            return;
        }

        var result = new char[CurrentEntry.Length];
        for (var i = 0; i < CurrentEntry.Length; i++)
        {
            var ch = CurrentEntry[i];
            if (IsAsciiLetter(ch))
            {
                result[i] = guessedLetters.Contains(ch) ? ch : '_';
            }
            else
            {
                result[i] = Settings.RevealNonLetterCharacters ? ch : '_';
            }
        }

        cachedMaskedEntry = new string(result);
        isMaskedEntryDirty = false;
    }

    private static char NormalizeGuess(char guess)
    {
        var upper = char.ToUpperInvariant(guess);
        return IsAsciiLetter(upper) ? upper : '\0';
    }

    private static bool IsAsciiLetter(char ch)
    {
        return ch is >= 'A' and <= 'Z';
    }

    private static void PopulateUnresolvedLetters(string entry, HashSet<char> destination)
    {
        for (var i = 0; i < entry.Length; i++)
        {
            var ch = entry[i];
            if (IsAsciiLetter(ch))
            {
                destination.Add(ch);
            }
        }
    }

    private int DrawNextEntryIndex(HangmanDifficulty difficulty)
    {
        var pool = pools[difficulty];
        if (pool.RemainingIndexes.Count == 0)
        {
            pool.RemainingIndexes.AddRange(pool.EligibleIndexes);
        }

        var pickedSlot = random.Next(pool.RemainingIndexes.Count);
        var selected = pool.RemainingIndexes[pickedSlot];
        pool.RemainingIndexes.RemoveAt(pickedSlot);

        if (pool.EligibleIndexes.Count > 1 && selected == pool.LastSelectedIndex && pool.RemainingIndexes.Count > 0)
        {
            var alternateSlot = random.Next(pool.RemainingIndexes.Count);
            var alternate = pool.RemainingIndexes[alternateSlot];
            pool.RemainingIndexes.RemoveAt(alternateSlot);
            pool.RemainingIndexes.Add(selected);
            selected = alternate;
        }

        pool.LastSelectedIndex = selected;
        return selected;
    }

    private void BuildDifficultyPools()
    {
        pools.Add(HangmanDifficulty.Any, BuildPoolState(HangmanDifficulty.Any));
        pools.Add(HangmanDifficulty.Easy, BuildPoolState(HangmanDifficulty.Easy));
        pools.Add(HangmanDifficulty.Medium, BuildPoolState(HangmanDifficulty.Medium));
        pools.Add(HangmanDifficulty.Hard, BuildPoolState(HangmanDifficulty.Hard));
    }

    private DifficultyPoolState BuildPoolState(HangmanDifficulty difficulty)
    {
        var eligible = new List<int>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            if (IsEntryEligible(entries[i], difficulty))
            {
                eligible.Add(i);
            }
        }

        if (eligible.Count == 0)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                eligible.Add(i);
            }
        }

        return new DifficultyPoolState(eligible);
    }

    private static bool IsEntryEligible(HangmanWordEntry entry, HangmanDifficulty selected)
    {
        if (selected == HangmanDifficulty.Any)
        {
            return true;
        }

        return entry.Difficulty is HangmanDifficulty.Any || entry.Difficulty == selected;
    }

    private static HangmanDifficulty NormalizeDifficulty(HangmanDifficulty difficulty)
    {
        return Enum.IsDefined(difficulty) ? difficulty : HangmanDifficulty.Any;
    }

    private sealed class DifficultyPoolState
    {
        public DifficultyPoolState(List<int> eligibleIndexes)
        {
            EligibleIndexes = eligibleIndexes;
            RemainingIndexes = [];
        }

        public List<int> EligibleIndexes { get; }
        public List<int> RemainingIndexes { get; }
        public int LastSelectedIndex { get; set; } = -1;
    }
}
