using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Arcade.Games.Sudoku;

public sealed class FileSudokuPuzzleProvider : ISudokuPuzzleProvider
{
    private static readonly string[] DefaultFallbackLines =
    [
        "easy|fallback_easy_001|534678010070100308108042567859760403406053090010904856961537080080400605305086179|534678912672195348198342567859761423426853791713924856961537284287419635345286179",
        "medium|fallback_medium_001|500678002602090048190040507800761003406050091710020806900537004207010035340080109|534678912672195348198342567859761423426853791713924856961537284287419635345286179",
        "hard|fallback_hard_001|500070010000100300008002000050000003400050090000900800001007000080000005300080070|534678912672195348198342567859761423426853791713924856961537284287419635345286179",
    ];

    private readonly string filePath;
    private readonly IReadOnlyList<string> fallbackLines;
    private IReadOnlyList<SudokuPuzzle>? cachedPuzzles;

    internal static Action<string>? WarningSink { get; set; }

    public FileSudokuPuzzleProvider(string filePath, IEnumerable<string>? fallbackLines = null)
    {
        this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        this.fallbackLines = (fallbackLines ?? DefaultFallbackLines).ToArray();
    }

    public IReadOnlyList<SudokuPuzzle> GetPuzzles()
    {
        cachedPuzzles ??= LoadPuzzles();
        return cachedPuzzles;
    }

    private IReadOnlyList<SudokuPuzzle> LoadPuzzles()
    {
        var puzzles = new List<SudokuPuzzle>();
        var acceptedIds = new Dictionary<string, int>(StringComparer.Ordinal);
        var acceptedGivens = new Dictionary<string, int>(StringComparer.Ordinal);

        if (File.Exists(filePath))
        {
            LoadLines(
                File.ReadLines(filePath),
                puzzles,
                acceptedIds,
                acceptedGivens,
                logProblems: true,
                sourceName: Path.GetFileName(filePath));
        }

        if (puzzles.Count == 0)
        {
            LoadLines(
                fallbackLines,
                puzzles,
                acceptedIds,
                acceptedGivens,
                logProblems: false,
                sourceName: "fallback");
        }

        if (puzzles.Count == 0)
        {
            throw new InvalidOperationException("Sudoku puzzle provider has no valid puzzles.");
        }

        return puzzles;
    }

    private static void LoadLines(
        IEnumerable<string> lines,
        List<SudokuPuzzle> puzzles,
        Dictionary<string, int> acceptedIds,
        Dictionary<string, int> acceptedGivens,
        bool logProblems,
        string sourceName)
    {
        var lineNumber = 0;
        foreach (var line in lines)
        {
            lineNumber++;
            if (!TryParsePuzzle(line, out var puzzle, out var error))
            {
                if (logProblems && error is not null)
                {
                    WarningSink?.Invoke($"Sudoku puzzle ignored in {sourceName} at line {lineNumber}: {error}");
                }

                continue;
            }

            if (acceptedIds.TryGetValue(puzzle.Id, out var existingIdLine))
            {
                if (logProblems)
                {
                    WarningSink?.Invoke(
                        $"Sudoku puzzle ignored in {sourceName} at line {lineNumber}: duplicate id '{puzzle.Id}' already accepted at line {existingIdLine}.");
                }

                continue;
            }

            if (acceptedGivens.TryGetValue(puzzle.Givens, out var existingGivensLine))
            {
                if (logProblems)
                {
                    WarningSink?.Invoke(
                        $"Sudoku puzzle ignored in {sourceName} at line {lineNumber}: duplicate givens already accepted at line {existingGivensLine}.");
                }

                continue;
            }

            puzzles.Add(puzzle);
            acceptedIds.Add(puzzle.Id, lineNumber);
            acceptedGivens.Add(puzzle.Givens, lineNumber);
        }
    }

    internal static bool TryParsePuzzle(string? rawValue, out SudokuPuzzle puzzle, out string? error)
    {
        puzzle = default;
        error = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var trimmed = rawValue.Trim();
        if (trimmed.StartsWith('#'))
        {
            return false;
        }

        var parts = trimmed.Split('|');
        if (parts.Length != 4)
        {
            error = "expected 4 pipe-delimited fields.";
            return false;
        }

        if (!TryParseDifficulty(parts[0], out var difficulty))
        {
            error = $"invalid difficulty '{parts[0]}'.";
            return false;
        }

        var id = parts[1].Trim();
        if (!IsValidPuzzleId(id))
        {
            error = $"invalid puzzle id '{id}'.";
            return false;
        }

        var givens = parts[2].Trim();
        if (!IsValidGivens(givens, out error))
        {
            return false;
        }

        var solution = parts[3].Trim();
        if (!IsValidSolution(solution, out error))
        {
            return false;
        }

        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            if (givens[index] != '0' && givens[index] != solution[index])
            {
                error = "givens must match the same positions in the solution.";
                return false;
            }
        }

        if (!IsValidSolvedGrid(solution))
        {
            error = "solution must be a valid solved Sudoku grid.";
            return false;
        }

        var (rotationGroupTag, isRotatedVariant) = ParseRotationTag(id);
        puzzle = new SudokuPuzzle(id, difficulty, givens, solution, rotationGroupTag, isRotatedVariant);
        return true;
    }

    private static bool TryParseDifficulty(string rawValue, out SudokuDifficulty difficulty)
    {
        difficulty = rawValue.Trim().ToUpperInvariant() switch
        {
            "EASY" => SudokuDifficulty.Easy,
            "MEDIUM" => SudokuDifficulty.Medium,
            "HARD" => SudokuDifficulty.Hard,
            _ => SudokuDifficulty.Any,
        };

        return difficulty != SudokuDifficulty.Any;
    }

    private static bool IsValidPuzzleId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        foreach (var ch in id)
        {
            if (!(ch is >= 'a' and <= 'z'
                || ch is >= 'A' and <= 'Z'
                || ch is >= '0' and <= '9'
                || ch == '_'
                || ch == '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidGivens(string givens, out string? error)
    {
        error = null;
        if (givens.Length != SudokuBoard.CellCount)
        {
            error = "givens must be exactly 81 digits.";
            return false;
        }

        for (var index = 0; index < givens.Length; index++)
        {
            if (givens[index] is < '0' or > '9')
            {
                error = "givens may only contain digits 0-9.";
                return false;
            }
        }

        return true;
    }

    private static bool IsValidSolution(string solution, out string? error)
    {
        error = null;
        if (solution.Length != SudokuBoard.CellCount)
        {
            error = "solution must be exactly 81 digits.";
            return false;
        }

        for (var index = 0; index < solution.Length; index++)
        {
            if (solution[index] is < '1' or > '9')
            {
                error = "solution may only contain digits 1-9.";
                return false;
            }
        }

        return true;
    }

    private static bool IsValidSolvedGrid(string solution)
    {
        var seen = new bool[10];

        for (var row = 0; row < SudokuBoard.Size; row++)
        {
            Array.Clear(seen, 0, seen.Length);
            for (var column = 0; column < SudokuBoard.Size; column++)
            {
                var value = solution[(row * SudokuBoard.Size) + column] - '0';
                if (seen[value])
                {
                    return false;
                }

                seen[value] = true;
            }
        }

        for (var column = 0; column < SudokuBoard.Size; column++)
        {
            Array.Clear(seen, 0, seen.Length);
            for (var row = 0; row < SudokuBoard.Size; row++)
            {
                var value = solution[(row * SudokuBoard.Size) + column] - '0';
                if (seen[value])
                {
                    return false;
                }

                seen[value] = true;
            }
        }

        for (var boxRow = 0; boxRow < SudokuBoard.Size; boxRow += 3)
        {
            for (var boxColumn = 0; boxColumn < SudokuBoard.Size; boxColumn += 3)
            {
                Array.Clear(seen, 0, seen.Length);
                for (var row = boxRow; row < boxRow + 3; row++)
                {
                    for (var column = boxColumn; column < boxColumn + 3; column++)
                    {
                        var value = solution[(row * SudokuBoard.Size) + column] - '0';
                        if (seen[value])
                        {
                            return false;
                        }

                        seen[value] = true;
                    }
                }
            }
        }

        return true;
    }

    private static (string RotationGroupTag, bool IsRotatedVariant) ParseRotationTag(string id)
    {
        const string Rot90Suffix = "_rot90";
        const string Rot180Suffix = "_rot180";
        const string Rot270Suffix = "_rot270";

        if (TryStripSuffix(id, Rot90Suffix, out var baseId)
            || TryStripSuffix(id, Rot180Suffix, out baseId)
            || TryStripSuffix(id, Rot270Suffix, out baseId))
        {
            return (baseId, true);
        }

        return (id, false);
    }

    private static bool TryStripSuffix(string id, string suffix, out string baseId)
    {
        baseId = string.Empty;
        if (!id.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = id[..^suffix.Length];
        if (candidate.Length == 0)
        {
            return false;
        }

        baseId = candidate;
        return true;
    }
}
