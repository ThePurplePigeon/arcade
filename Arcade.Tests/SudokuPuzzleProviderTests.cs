using System;
using System.Collections.Generic;
using System.IO;
using Arcade.Games.Sudoku;
using Xunit;

namespace Arcade.Tests;

[Collection("WarningSinkTests")]
public class SudokuPuzzleProviderTests
{
    private const string Solution = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
    private const string Givens = "500678002602090048190040507800761003406050091710020806900537004207010035340080109";
    private const string AlternateSolution = "645789123783216459219453678961872534537964812824135967172648395398521746456397281";
    private const string AlternateGivens = "600080100003010009200450670001000504530064800804000007100040305008500046406090200";
    private static readonly string BlankGivens = new('0', 81);

    [Fact]
    public void GetPuzzles_UsesFallbackWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sudoku_missing_{Guid.NewGuid():N}.txt");
        var provider = new FileSudokuPuzzleProvider(path);

        var puzzles = provider.GetPuzzles();

        Assert.NotEmpty(puzzles);
        Assert.Contains(puzzles, puzzle => puzzle.Id == "fallback_easy_001");
    }

    [Fact]
    public void GetPuzzles_SkipsInvalidAndDuplicateEntriesWithWarnings()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"sudoku_puzzles_{Guid.NewGuid():N}.txt");
        var warnings = new List<string>();
        var previousSink = FileSudokuPuzzleProvider.WarningSink;

        try
        {
            FileSudokuPuzzleProvider.WarningSink = warnings.Add;
            File.WriteAllLines(filePath,
            [
                "# comment",
                $"easy|starter_001|{Givens}|{Solution}",
                $"medium|starter_001|{AlternateGivens}|{AlternateSolution}",
                $"hard|hard_001|{Givens}|{Solution}",
                $"legendary|bad_diff|{Givens}|{Solution}",
                $"easy|bad id|{Givens}|{Solution}",
                $"easy|bad_solution|{BlankGivens}|123456789123456789123456789123456789123456789123456789123456789123456789123456789",
                $"easy|mismatch|{Solution}|{AlternateSolution}",
            ]);

            var provider = new FileSudokuPuzzleProvider(filePath);
            var puzzles = provider.GetPuzzles();

            Assert.Single(puzzles);
            Assert.Equal("starter_001", puzzles[0].Id);
            Assert.Equal(6, warnings.Count);
            Assert.Contains(warnings, warning => warning.Contains("duplicate id 'starter_001'", StringComparison.Ordinal));
            Assert.Contains(warnings, warning => warning.Contains("duplicate givens", StringComparison.Ordinal));
            Assert.Contains(warnings, warning => warning.Contains("invalid difficulty", StringComparison.Ordinal));
            Assert.Contains(warnings, warning => warning.Contains("invalid puzzle id", StringComparison.Ordinal));
            Assert.Contains(warnings, warning => warning.Contains("valid solved Sudoku grid", StringComparison.Ordinal));
            Assert.Contains(warnings, warning => warning.Contains("givens must match", StringComparison.Ordinal));
        }
        finally
        {
            FileSudokuPuzzleProvider.WarningSink = previousSink;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void TryParsePuzzle_InvalidFieldCount_IsRejectedWithError()
    {
        var parsed = FileSudokuPuzzleProvider.TryParsePuzzle("easy|id|too_few", out _, out var error);

        Assert.False(parsed);
        Assert.NotNull(error);
        Assert.Contains("expected 4 pipe-delimited fields", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("starter_001_rot90")]
    [InlineData("starter_001_rot180")]
    [InlineData("starter_001_rot270")]
    public void TryParsePuzzle_RotationSuffix_SetsInternalRotationTag(string puzzleId)
    {
        var parsed = FileSudokuPuzzleProvider.TryParsePuzzle(
            $"easy|{puzzleId}|{Givens}|{Solution}",
            out var puzzle,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.True(puzzle.IsRotatedVariant);
        Assert.Equal("starter_001", puzzle.RotationGroupTag);
        Assert.Equal("starter_001", puzzle.SelectionGroupTag);
    }

    [Fact]
    public void TryParsePuzzle_NonRotationId_UsesIdAsSelectionGroup()
    {
        var parsed = FileSudokuPuzzleProvider.TryParsePuzzle(
            $"easy|starter_002|{Givens}|{Solution}",
            out var puzzle,
            out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.False(puzzle.IsRotatedVariant);
        Assert.Equal("starter_002", puzzle.RotationGroupTag);
        Assert.Equal("starter_002", puzzle.SelectionGroupTag);
    }

    [Fact]
    public void GetPuzzles_UsesFallbackWhenFileHasOnlyInvalidLines()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"sudoku_invalid_{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(filePath,
            [
                "# comment",
                "easy|bad|123|456",
            ]);

            var provider = new FileSudokuPuzzleProvider(filePath);
            var puzzles = provider.GetPuzzles();

            Assert.NotEmpty(puzzles);
            Assert.Contains(puzzles, puzzle => puzzle.Id == "fallback_easy_001");
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
    public void GetPuzzles_ThrowsWhenFileAndFallbackAreBothInvalid()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"sudoku_broken_{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllLines(filePath,
            [
                "hard|bad|123|456",
            ]);

            var provider = new FileSudokuPuzzleProvider(filePath, fallbackLines: ["easy|bad|123|456"]);

            Assert.Throws<InvalidOperationException>(() => provider.GetPuzzles());
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

}
