using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arcade.Games.Hangman;
using Xunit;

namespace Arcade.Tests;

[Collection("WarningSinkTests")]
public class FileHangmanWordProviderTests
{
    [Fact]
    public void GetEntries_IgnoresExactDuplicateWithoutWarning()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"hangman_words_{Guid.NewGuid():N}.txt");
        var warnings = new List<string>();
        var previousSink = FileHangmanWordProvider.WarningSink;

        try
        {
            FileHangmanWordProvider.WarningSink = warnings.Add;
            File.WriteAllLines(filePath,
            [
                "[easy] chocobo",
                "[easy] chocobo",
            ]);

            var provider = new FileHangmanWordProvider(filePath);
            var entries = provider.GetEntries();

            Assert.Single(entries);
            Assert.Equal("CHOCOBO", entries[0].Text);
            Assert.Equal(HangmanDifficulty.Easy, entries[0].Difficulty);
            Assert.Empty(warnings);
        }
        finally
        {
            FileHangmanWordProvider.WarningSink = previousSink;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void GetEntries_RejectsConflictingDifficultyDuplicateAndWarns()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"hangman_words_{Guid.NewGuid():N}.txt");
        var warnings = new List<string>();
        var previousSink = FileHangmanWordProvider.WarningSink;

        try
        {
            FileHangmanWordProvider.WarningSink = warnings.Add;
            File.WriteAllLines(filePath,
            [
                "[easy] chocobo",
                "[hard] chocobo",
            ]);

            var provider = new FileHangmanWordProvider(filePath);
            var entries = provider.GetEntries();

            Assert.Single(entries);
            Assert.Equal(HangmanDifficulty.Easy, entries[0].Difficulty);
            Assert.Single(warnings);
            Assert.Contains("CHOCOBO", warnings[0], StringComparison.Ordinal);
            Assert.Contains("line 2", warnings[0], StringComparison.Ordinal);
            Assert.Contains("line 1", warnings[0], StringComparison.Ordinal);
            Assert.Contains("Hard", warnings[0], StringComparison.Ordinal);
            Assert.Contains("Easy", warnings[0], StringComparison.Ordinal);
        }
        finally
        {
            FileHangmanWordProvider.WarningSink = previousSink;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void GetEntries_KeepsNormalizedEntriesUnique()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"hangman_words_{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(filePath,
            [
                "[medium] y'shtola",
                "[medium] y\u2019shtola",
                "[medium] moon-cat",
                "[medium] moon\u2014cat",
            ]);

            var provider = new FileHangmanWordProvider(filePath);
            var entries = provider.GetEntries();

            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, entry => entry.Text == "Y'SHTOLA");
            Assert.Contains(entries, entry => entry.Text == "MOON-CAT");
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
    public void GetEntries_UsesFallbackWhenFileContainsNoValidLines()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"hangman_words_{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(filePath,
            [
                "# only comments",
                "",
                "***",
            ]);

            var provider = new FileHangmanWordProvider(filePath);
            var entries = provider.GetEntries();

            Assert.NotEmpty(entries);
            Assert.Contains(entries, entry => entry.Text == "LIMIT BREAK");
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
    public void GetEntries_ReturnsCachedInstance()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"hangman_words_{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(filePath,
            [
                "[easy] chocobo",
                "[medium] limit break",
            ]);

            var provider = new FileHangmanWordProvider(filePath);
            var first = provider.GetEntries();
            var second = provider.GetEntries();

            Assert.Same(first, second);
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
    public void TryParseEntry_NormalizesWhitespaceAndDifficultyTag()
    {
        var parsed = FileHangmanWordProvider.TryParseEntry(" [hard]  warrior   of   light ", out var entry);

        Assert.True(parsed);
        Assert.Equal("WARRIOR OF LIGHT", entry.Text);
        Assert.Equal(HangmanDifficulty.Hard, entry.Difficulty);
    }

    [Fact]
    public void TryParseEntry_RejectsTagOnlyAndCommentOnlyLines()
    {
        Assert.False(FileHangmanWordProvider.TryParseEntry("[easy]   # comment", out _));
        Assert.False(FileHangmanWordProvider.TryParseEntry("[medium]", out _));
    }

}
