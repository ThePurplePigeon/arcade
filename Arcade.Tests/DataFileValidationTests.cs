using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Arcade.Games.Hangman;
using Arcade.Games.Sudoku;
using Xunit;
using Xunit.Sdk;

namespace Arcade.Tests;

[Collection("WarningSinkTests")]
public class DataFileValidationTests
{
    [Fact]
    public void HangmanWordBank_IsValidAndConflictFree()
    {
        var warnings = new List<string>();
        var previousSink = FileHangmanWordProvider.WarningSink;

        try
        {
            FileHangmanWordProvider.WarningSink = warnings.Add;
            var provider = new FileHangmanWordProvider(GetDataPath("hangman_words.txt"));
            var entries = provider.GetEntries();

            Assert.NotEmpty(entries);
            Assert.Empty(warnings);
            Assert.Equal(entries.Count, entries.Select(entry => entry.Text).Distinct(StringComparer.Ordinal).Count());
            Assert.Contains(entries, entry => entry.Difficulty == HangmanDifficulty.Easy);
            Assert.Contains(entries, entry => entry.Difficulty == HangmanDifficulty.Medium);
            Assert.Contains(entries, entry => entry.Difficulty == HangmanDifficulty.Hard);
        }
        finally
        {
            FileHangmanWordProvider.WarningSink = previousSink;
        }
    }

    [Fact]
    public void SudokuPuzzleBank_IsValidAndConflictFree()
    {
        var warnings = new List<string>();
        var previousSink = FileSudokuPuzzleProvider.WarningSink;

        try
        {
            FileSudokuPuzzleProvider.WarningSink = warnings.Add;
            var provider = new FileSudokuPuzzleProvider(GetDataPath("sudoku_puzzles.txt"));
            var puzzles = provider.GetPuzzles();

            Assert.NotEmpty(puzzles);
            Assert.Empty(warnings);
            Assert.Equal(puzzles.Count, puzzles.Select(puzzle => puzzle.Id).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(puzzles.Count, puzzles.Select(puzzle => puzzle.Givens).Distinct(StringComparer.Ordinal).Count());
            Assert.True(puzzles.Count(puzzle => puzzle.Difficulty == SudokuDifficulty.Easy) >= 5);
            Assert.True(puzzles.Count(puzzle => puzzle.Difficulty == SudokuDifficulty.Medium) >= 5);
            Assert.True(puzzles.Count(puzzle => puzzle.Difficulty == SudokuDifficulty.Hard) >= 5);
        }
        finally
        {
            FileSudokuPuzzleProvider.WarningSink = previousSink;
        }
    }

    [Fact]
    public void SudokuPuzzleBank_AllPuzzlesAreSolvable()
    {
        var provider = new FileSudokuPuzzleProvider(GetDataPath("sudoku_puzzles.txt"));
        var puzzles = provider.GetPuzzles();
        Assert.NotEmpty(puzzles);

        var failures = new List<string>();
        foreach (var puzzle in puzzles)
        {
            var result = CheckSolvable(puzzle.Givens);
            if (!result.IsSolvable)
            {
                var detailSuffix = string.IsNullOrWhiteSpace(result.Detail)
                    ? string.Empty
                    : $" ({result.Detail})";
                failures.Add($"- {puzzle.Id}: {result.Step}{detailSuffix}");
            }
        }

        if (failures.Count > 0)
        {
            throw new XunitException("Sudoku solvability check failed:\n" + string.Join('\n', failures));
        }
    }

    private static string GetDataPath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Data", fileName));
    }

    private static SudokuSolvabilityResult CheckSolvable(string givens)
    {
        if (givens.Length != SudokuBoard.CellCount)
        {
            return new SudokuSolvabilityResult(
                false,
                "InputValidation.Length",
                $"expected {SudokuBoard.CellCount}, got {givens.Length}");
        }

        var board = new int[SudokuBoard.CellCount];
        Span<int> rowMasks = stackalloc int[SudokuBoard.Size];
        Span<int> columnMasks = stackalloc int[SudokuBoard.Size];
        Span<int> boxMasks = stackalloc int[SudokuBoard.Size];

        for (var index = 0; index < givens.Length; index++)
        {
            var ch = givens[index];
            if (ch == '0')
            {
                continue;
            }

            if (ch is < '1' or > '9')
            {
                return new SudokuSolvabilityResult(
                    false,
                    "InputValidation.Character",
                    $"index {index}, value '{ch}'");
            }

            var value = ch - '0';
            var bit = 1 << value;
            var row = index / SudokuBoard.Size;
            var column = index % SudokuBoard.Size;
            var box = ((row / 3) * 3) + (column / 3);

            if (((rowMasks[row] | columnMasks[column] | boxMasks[box]) & bit) != 0)
            {
                return new SudokuSolvabilityResult(
                    false,
                    "InputValidation.GivensContradiction",
                    $"row {row + 1}, col {column + 1}, value {value}");
            }

            rowMasks[row] |= bit;
            columnMasks[column] |= bit;
            boxMasks[box] |= bit;
            board[index] = value;
        }

        return SolveBoard(board, rowMasks, columnMasks, boxMasks)
            ? new SudokuSolvabilityResult(true, "Solved", string.Empty)
            : new SudokuSolvabilityResult(false, "Backtracking.NoSolution", "search exhausted");
    }

    private static bool SolveBoard(int[] board, Span<int> rowMasks, Span<int> columnMasks, Span<int> boxMasks)
    {
        const int allDigitsMask = 0x3FE; // Bits 1-9 set.

        var bestIndex = -1;
        var bestCandidates = 0;
        var bestCount = int.MaxValue;

        for (var index = 0; index < board.Length; index++)
        {
            if (board[index] != 0)
            {
                continue;
            }

            var row = index / SudokuBoard.Size;
            var column = index % SudokuBoard.Size;
            var box = ((row / 3) * 3) + (column / 3);
            var used = rowMasks[row] | columnMasks[column] | boxMasks[box];
            var candidates = allDigitsMask & ~used;
            if (candidates == 0)
            {
                return false;
            }

            var count = BitOperations.PopCount((uint)candidates);
            if (count < bestCount)
            {
                bestCount = count;
                bestCandidates = candidates;
                bestIndex = index;
                if (count == 1)
                {
                    break;
                }
            }
        }

        if (bestIndex < 0)
        {
            return true;
        }

        var targetRow = bestIndex / SudokuBoard.Size;
        var targetColumn = bestIndex % SudokuBoard.Size;
        var targetBox = ((targetRow / 3) * 3) + (targetColumn / 3);

        var candidatesMask = bestCandidates;
        while (candidatesMask != 0)
        {
            var bit = candidatesMask & -candidatesMask;
            candidatesMask ^= bit;

            var value = BitOperations.TrailingZeroCount((uint)bit);
            board[bestIndex] = value;
            rowMasks[targetRow] |= bit;
            columnMasks[targetColumn] |= bit;
            boxMasks[targetBox] |= bit;

            if (SolveBoard(board, rowMasks, columnMasks, boxMasks))
            {
                return true;
            }

            board[bestIndex] = 0;
            rowMasks[targetRow] &= ~bit;
            columnMasks[targetColumn] &= ~bit;
            boxMasks[targetBox] &= ~bit;
        }

        return false;
    }

    private readonly record struct SudokuSolvabilityResult(bool IsSolvable, string Step, string Detail);
}
