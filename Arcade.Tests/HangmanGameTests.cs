using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcade.Games.Hangman;
using Xunit;

namespace Arcade.Tests;

public class HangmanGameTests
{
    [Fact]
    public void WordProvider_ParsesDifficultyTagsAndKeepsNormalizationRules()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"hangman_words_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllLines(filePath,
            [
                "# comment",
                "",
                "[easy] chocobo",
                "[medium] limit break",
                "[hard] warrior-of-light",
                "[medium] y\u2019shtola",
                "[easy] moon\u2014cat",
                "[unknown] allagan",
                "aetheryte",
                "bad@token",
                "[easy] chocobo",
            ]);

            var provider = new FileHangmanWordProvider(filePath);
            var entries = provider.GetEntries();

            Assert.Contains(entries, entry => entry.Text == "CHOCOBO" && entry.Difficulty == HangmanDifficulty.Easy);
            Assert.Contains(entries, entry => entry.Text == "LIMIT BREAK" && entry.Difficulty == HangmanDifficulty.Medium);
            Assert.Contains(entries, entry => entry.Text == "WARRIOR-OF-LIGHT" && entry.Difficulty == HangmanDifficulty.Hard);
            Assert.Contains(entries, entry => entry.Text == "Y'SHTOLA" && entry.Difficulty == HangmanDifficulty.Medium);
            Assert.Contains(entries, entry => entry.Text == "MOON-CAT" && entry.Difficulty == HangmanDifficulty.Easy);
            Assert.Contains(entries, entry => entry.Text == "ALLAGAN" && entry.Difficulty == HangmanDifficulty.Any);
            Assert.Contains(entries, entry => entry.Text == "AETHERYTE" && entry.Difficulty == HangmanDifficulty.Any);
            Assert.DoesNotContain(entries, entry => entry.Text == "BAD@TOKEN");
            Assert.Equal(entries.Count, entries.Select(entry => entry.Text).Distinct(StringComparer.Ordinal).Count());
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void WordProvider_UsesFallback_WhenFileMissing()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.txt");
        var provider = new FileHangmanWordProvider(missingPath);
        var entries = provider.GetEntries();

        Assert.NotEmpty(entries);
        Assert.Contains(entries, entry => entry.Text == "LIMIT BREAK");
    }

    [Fact]
    public void MaskedEntry_RevealsSeparators_ForPhrases()
    {
        var game = new HangmanGame(new FixedWordProvider(new HangmanWordEntry("KRILE'S-BOOK", HangmanDifficulty.Any)), seed: 1);
        Assert.Equal("_____'_-____", game.MaskedEntry);

        var result = game.Guess('L');
        Assert.Equal(HangmanGuessResult.Correct, result);
        Assert.Equal("___L_'_-____", game.MaskedEntry);
    }

    [Fact]
    public void RepeatedGuess_DoesNotConsumeAttempt()
    {
        var game = new HangmanGame(
            new FixedWordProvider(new HangmanWordEntry("CRYSTAL", HangmanDifficulty.Any)),
            seed: 2);

        var first = game.Guess('Z');
        var second = game.Guess('Z');

        Assert.Equal(HangmanGuessResult.Incorrect, first);
        Assert.Equal(HangmanGuessResult.AlreadyGuessed, second);
        Assert.Equal(1, game.WrongGuessCount);
        Assert.Equal(game.Settings.MaxWrongGuesses - 1, game.RemainingGuesses);
    }

    [Fact]
    public void DifficultyPool_EasyUsesEasyAndAnyEntries()
    {
        var game = new HangmanGame(
            new FixedWordProvider(
                new HangmanWordEntry("EASYONE", HangmanDifficulty.Easy),
                new HangmanWordEntry("EASYTWO", HangmanDifficulty.Easy),
                new HangmanWordEntry("ANYONE", HangmanDifficulty.Any),
                new HangmanWordEntry("MEDIUMONE", HangmanDifficulty.Medium),
                new HangmanWordEntry("HARDONE", HangmanDifficulty.Hard)),
            new HangmanGameSettings(defaultDifficulty: HangmanDifficulty.Easy),
            seed: 5);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 20; i++)
        {
            seen.Add(game.CurrentEntry);
            game.StartNewRound();
        }

        Assert.DoesNotContain("MEDIUMONE", seen);
        Assert.DoesNotContain("HARDONE", seen);
        Assert.Contains("EASYONE", seen);
        Assert.Contains("ANYONE", seen);
    }

    [Fact]
    public void DifficultyPool_DoesNotRepeatBeforeExhaustion()
    {
        var game = new HangmanGame(
            new FixedWordProvider(
                new HangmanWordEntry("ONE", HangmanDifficulty.Easy),
                new HangmanWordEntry("TWO", HangmanDifficulty.Easy),
                new HangmanWordEntry("THREE", HangmanDifficulty.Easy)),
            new HangmanGameSettings(defaultDifficulty: HangmanDifficulty.Easy),
            seed: 8);

        var seen = new HashSet<string>(StringComparer.Ordinal) { game.CurrentEntry };
        game.StartNewRound();
        seen.Add(game.CurrentEntry);
        game.StartNewRound();
        seen.Add(game.CurrentEntry);

        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void SetDifficulty_StartsNewRoundAndFiltersByDifficulty()
    {
        var game = new HangmanGame(
            new FixedWordProvider(
                new HangmanWordEntry("ALPHA", HangmanDifficulty.Easy),
                new HangmanWordEntry("BETA", HangmanDifficulty.Medium)),
            new HangmanGameSettings(defaultDifficulty: HangmanDifficulty.Easy),
            seed: 9);

        game.SetDifficulty(HangmanDifficulty.Medium, startNewRound: true);

        Assert.Equal(HangmanDifficulty.Medium, game.SelectedDifficulty);
        Assert.Equal("BETA", game.CurrentEntry);
    }

    [Fact]
    public void RoundCompleted_EventFiresExactlyOnce()
    {
        var game = new HangmanGame(
            new FixedWordProvider(new HangmanWordEntry("A", HangmanDifficulty.Any)),
            seed: 10);

        var eventCount = 0;
        HangmanRoundSummary? lastSummary = null;
        game.RoundCompleted += summary =>
        {
            eventCount++;
            lastSummary = summary;
        };

        Assert.Equal(HangmanGuessResult.Won, game.Guess('A'));
        Assert.Equal(HangmanGuessResult.Invalid, game.Guess('B'));

        Assert.Equal(1, eventCount);
        Assert.NotNull(lastSummary);
        Assert.Equal(HangmanGameState.Won, lastSummary.Value.Result);
        Assert.Equal("A", lastSummary.Value.Answer);
    }

    [Fact]
    public void FinishedRound_BlocksFurtherGuesses_UntilNextRound()
    {
        var game = new HangmanGame(
            new FixedWordProvider(new HangmanWordEntry("A", HangmanDifficulty.Any)),
            seed: 11);
        Assert.Equal(HangmanGuessResult.Won, game.Guess('A'));
        Assert.Equal(HangmanGuessResult.Invalid, game.Guess('B'));

        game.StartNewRound();
        Assert.Equal(HangmanGameState.Ready, game.State);
        Assert.True(game.IsLetterAvailable('B'));
    }

    [Fact]
    public void RemainingGuesses_TracksBoundaryAndResetsOnNewRound()
    {
        var game = new HangmanGame(
            new FixedWordProvider(new HangmanWordEntry("CAT", HangmanDifficulty.Any)),
            new HangmanGameSettings(maxWrongGuesses: 1),
            seed: 12);

        Assert.Equal(1, game.RemainingGuesses);
        Assert.Equal(HangmanGuessResult.Lost, game.Guess('Z'));
        Assert.Equal(0, game.RemainingGuesses);
        Assert.Equal(HangmanGuessResult.Invalid, game.Guess('X'));
        Assert.Equal(0, game.RemainingGuesses);

        game.StartNewRound();
        Assert.Equal(HangmanGameState.Ready, game.State);
        Assert.Equal(1, game.RemainingGuesses);
        Assert.Equal(2, game.RoundNumber);
    }


    [Fact]
    public void Guess_NonLetterCharacter_IsInvalidAndDoesNotStartRound()
    {
        var game = new HangmanGame(
            new FixedWordProvider(new HangmanWordEntry("ALPHA", HangmanDifficulty.Any)),
            seed: 13);

        var result = game.Guess('1');

        Assert.Equal(HangmanGuessResult.Invalid, result);
        Assert.Equal(HangmanGameState.Ready, game.State);
        Assert.Empty(game.GuessedLetters);
        Assert.Equal(game.Settings.MaxWrongGuesses, game.RemainingGuesses);
    }

    [Fact]
    public void SetDifficulty_WithoutStartingRound_OnlyChangesSelection()
    {
        var game = new HangmanGame(
            new FixedWordProvider(
                new HangmanWordEntry("ALPHA", HangmanDifficulty.Easy),
                new HangmanWordEntry("OMEGA", HangmanDifficulty.Hard)),
            new HangmanGameSettings(defaultDifficulty: HangmanDifficulty.Easy),
            seed: 14);
        var currentEntry = game.CurrentEntry;

        game.SetDifficulty(HangmanDifficulty.Hard, startNewRound: false);

        Assert.Equal(HangmanDifficulty.Hard, game.SelectedDifficulty);
        Assert.Equal(currentEntry, game.CurrentEntry);
        Assert.Equal(HangmanGameState.Ready, game.State);

        game.StartNewRound();
        Assert.Equal("OMEGA", game.CurrentEntry);
    }

    [Fact]
    public void DifficultyPool_FallsBackToAll_WhenSelectionHasNoEligibleEntries()
    {
        var game = new HangmanGame(
            new FixedWordProvider(
                new HangmanWordEntry("ALPHA", HangmanDifficulty.Easy),
                new HangmanWordEntry("BETA", HangmanDifficulty.Easy)),
            new HangmanGameSettings(defaultDifficulty: HangmanDifficulty.Hard),
            seed: 15);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 8; i++)
        {
            seen.Add(game.CurrentEntry);
            game.StartNewRound();
        }

        Assert.Equal(HangmanDifficulty.Hard, game.SelectedDifficulty);
        Assert.Equal(2, seen.Count);
        Assert.Contains("ALPHA", seen);
        Assert.Contains("BETA", seen);
    }

    [Fact]
    public void MaskedEntry_HidesNonLetters_WhenConfigured()
    {
        var game = new HangmanGame(
            new FixedWordProvider(new HangmanWordEntry("A-B", HangmanDifficulty.Any)),
            new HangmanGameSettings(revealNonLetterCharacters: false),
            seed: 16);

        Assert.Equal("___", game.MaskedEntry);
        Assert.Equal(HangmanGuessResult.Correct, game.Guess('A'));
        Assert.Equal("A__", game.MaskedEntry);
        Assert.Equal(HangmanGuessResult.Won, game.Guess('B'));
        Assert.Equal("A_B", game.MaskedEntry);
    }

    [Fact]
    public void LosingRound_UpdatesSessionStatsAndRoundSummary()
    {
        var game = new HangmanGame(
            new FixedWordProvider(new HangmanWordEntry("A", HangmanDifficulty.Hard)),
            new HangmanGameSettings(maxWrongGuesses: 1, defaultDifficulty: HangmanDifficulty.Hard),
            seed: 17);

        var result = game.Guess('Z');

        Assert.Equal(HangmanGuessResult.Lost, result);
        Assert.Equal(HangmanGameState.Lost, game.State);

        var stats = game.SessionStats;
        Assert.Equal(1, stats.RoundsPlayed);
        Assert.Equal(0, stats.Wins);
        Assert.Equal(1, stats.Losses);
        Assert.Equal(0, stats.CurrentWinStreak);
        Assert.Equal(0, stats.BestWinStreak);

        Assert.NotNull(game.LastRoundSummary);
        Assert.Equal(HangmanGameState.Lost, game.LastRoundSummary.Value.Result);
        Assert.Equal("A", game.LastRoundSummary.Value.Answer);
        Assert.Equal(HangmanDifficulty.Hard, game.LastRoundSummary.Value.Difficulty);
        Assert.Equal(1, game.LastRoundSummary.Value.WrongGuessCount);
        Assert.Equal(1, game.LastRoundSummary.Value.MaxWrongGuesses);
    }

    private sealed class FixedWordProvider : IHangmanWordProvider
    {
        private readonly IReadOnlyList<HangmanWordEntry> entries;

        public FixedWordProvider(params HangmanWordEntry[] entries)
        {
            this.entries = entries;
        }

        public IReadOnlyList<HangmanWordEntry> GetEntries() => entries;
    }
}
