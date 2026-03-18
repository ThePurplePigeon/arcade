using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Arcade.Games.Hangman;

public sealed class FileHangmanWordProvider : IHangmanWordProvider
{
    private static readonly string[] DefaultFallbackEntries =
    [
        "EORZEA",
        "CRYSTAL",
        "AETHERYTE",
        "CHOCOBO",
        "TANK",
        "HEALER",
        "DUNGEON",
        "PRIMAL",
        "LIMIT BREAK",
        "WARRIOR OF LIGHT",
    ];

    private readonly string filePath;
    private readonly IReadOnlyList<string> fallbackEntries;
    private IReadOnlyList<HangmanWordEntry>? cachedEntries;

    internal static Action<string>? WarningSink { get; set; }

    public FileHangmanWordProvider(string filePath, IEnumerable<string>? fallbackEntries = null)
    {
        this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        this.fallbackEntries = (fallbackEntries ?? DefaultFallbackEntries).ToArray();
    }

    public IReadOnlyList<HangmanWordEntry> GetEntries()
    {
        cachedEntries ??= LoadEntries();
        return cachedEntries;
    }

    private IReadOnlyList<HangmanWordEntry> LoadEntries()
    {
        var entries = new List<HangmanWordEntry>();
        var acceptedEntries = new Dictionary<string, AcceptedEntry>(StringComparer.Ordinal);

        if (File.Exists(filePath))
        {
            LoadEntries(
                File.ReadLines(filePath),
                entries,
                acceptedEntries,
                logConflicts: true,
                sourceName: Path.GetFileName(filePath));
        }

        if (entries.Count == 0)
        {
            LoadEntries(
                fallbackEntries,
                entries,
                acceptedEntries,
                logConflicts: false,
                sourceName: "fallback");
        }

        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Hangman word provider has no valid entries.");
        }

        return entries;
    }

    private static void LoadEntries(
        IEnumerable<string> lines,
        List<HangmanWordEntry> entries,
        Dictionary<string, AcceptedEntry> acceptedEntries,
        bool logConflicts,
        string sourceName)
    {
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            if (!TryParseEntry(line, out var parsed))
            {
                continue;
            }

            if (acceptedEntries.TryGetValue(parsed.Text, out var existing))
            {
                if (logConflicts && existing.Entry.Difficulty != parsed.Difficulty)
                {
                    WarningSink?.Invoke(
                        $"Hangman word bank conflict ignored for '{parsed.Text}' in {sourceName}: " +
                        $"line {lineNumber} ({FormatDifficulty(parsed.Difficulty)}) conflicts with " +
                        $"line {existing.LineNumber} ({FormatDifficulty(existing.Entry.Difficulty)}).");
                }

                continue;
            }

            acceptedEntries.Add(parsed.Text, new AcceptedEntry(parsed, lineNumber));
            entries.Add(parsed);
        }
    }

    internal static bool TryParseEntry(string? rawValue, out HangmanWordEntry entry)
    {
        entry = default;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith('#'))
        {
            return false;
        }

        var parsedDifficulty = HangmanDifficulty.Any;
        trimmed = StripLeadingTag(trimmed, ref parsedDifficulty);
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return false;
        }

        if (!TryNormalizeText(trimmed, out var normalized))
        {
            return false;
        }

        entry = new HangmanWordEntry(normalized, parsedDifficulty);
        return true;
    }

    private static bool TryNormalizeText(string value, out string normalized)
    {
        normalized = string.Empty;
        var upper = value.ToUpperInvariant();
        var builder = new StringBuilder(upper.Length);
        var previousWasSpace = false;
        var hasLetter = false;

        foreach (var ch in upper)
        {
            if (ch is >= 'A' and <= 'Z')
            {
                builder.Append(ch);
                previousWasSpace = false;
                hasLetter = true;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            if (TryNormalizeSupportedPunctuation(ch, out var normalizedPunctuation))
            {
                builder.Append(normalizedPunctuation);
                previousWasSpace = false;
                continue;
            }

            return false;
        }

        normalized = builder.ToString().Trim();
        return hasLetter && normalized.Length > 0;
    }

    private static bool TryNormalizeSupportedPunctuation(char value, out char normalized)
    {
        normalized = value switch
        {
            '\'' or '\u2018' or '\u2019' or '\u02BC' or '\uFF07' => '\'',
            '-' or '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2212' => '-',
            _ => '\0',
        };

        return normalized != '\0';
    }

    private static string StripLeadingTag(string value, ref HangmanDifficulty difficulty)
    {
        if (value.Length == 0 || value[0] != '[')
        {
            return value;
        }

        var close = value.IndexOf(']');
        if (close <= 1)
        {
            return value;
        }

        var tag = value[1..close];
        if (!IsValidTag(tag))
        {
            return value;
        }

        difficulty = ParseDifficultyTag(tag);
        return value[(close + 1)..].TrimStart();
    }

    private static HangmanDifficulty ParseDifficultyTag(string tag)
    {
        return tag.ToUpperInvariant() switch
        {
            "EASY" => HangmanDifficulty.Easy,
            "MEDIUM" => HangmanDifficulty.Medium,
            "HARD" => HangmanDifficulty.Hard,
            _ => HangmanDifficulty.Any,
        };
    }

    private static bool IsValidTag(string tag)
    {
        foreach (var ch in tag)
        {
            if (!(ch is >= 'a' and <= 'z' || ch is >= 'A' and <= 'Z' || ch is >= '0' and <= '9' || ch == '-' || ch == '_'))
            {
                return false;
            }
        }

        return tag.Length > 0;
    }

    private static string FormatDifficulty(HangmanDifficulty difficulty)
    {
        return difficulty switch
        {
            HangmanDifficulty.Easy => "Easy",
            HangmanDifficulty.Medium => "Medium",
            HangmanDifficulty.Hard => "Hard",
            _ => "Any",
        };
    }

    private readonly record struct AcceptedEntry(HangmanWordEntry Entry, int LineNumber);
}
