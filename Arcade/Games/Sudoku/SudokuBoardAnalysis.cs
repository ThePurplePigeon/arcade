using System;

namespace Arcade.Games.Sudoku;

public sealed class SudokuBoardAnalysisSnapshot
{
    private readonly bool[] conflictingCells;

    internal SudokuBoardAnalysisSnapshot(bool[] conflictingCells, int filledCellCount, bool matchesSolution)
    {
        this.conflictingCells = conflictingCells ?? throw new ArgumentNullException(nameof(conflictingCells));
        FilledCellCount = filledCellCount;
        MatchesSolution = matchesSolution;
        HasConflicts = HasAnyConflicts(conflictingCells);
    }

    public static SudokuBoardAnalysisSnapshot Empty { get; } = new(new bool[SudokuBoard.CellCount], 0, false);

    public int FilledCellCount { get; }
    public bool MatchesSolution { get; }
    public bool HasConflicts { get; }
    public bool IsSolved => MatchesSolution;

    public bool IsConflicting(SudokuCoordinate coordinate)
    {
        return conflictingCells[SudokuBoard.GetIndex(coordinate)];
    }

    private static bool HasAnyConflicts(bool[] conflicts)
    {
        for (var index = 0; index < conflicts.Length; index++)
        {
            if (conflicts[index])
            {
                return true;
            }
        }

        return false;
    }
}

internal static class SudokuBoardAnalysis
{
    public static SudokuBoardAnalysisSnapshot Analyze(SudokuBoard board, string solution)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(solution);

        if (solution.Length != SudokuBoard.CellCount)
        {
            throw new ArgumentException("Solution must be exactly 81 characters long.", nameof(solution));
        }

        var conflicts = new bool[SudokuBoard.CellCount];
        var filledCellCount = 0;
        var matchesSolution = true;

        for (var index = 0; index < SudokuBoard.CellCount; index++)
        {
            var value = board.GetValueAtIndex(index);
            if (value == 0)
            {
                matchesSolution = false;
                continue;
            }

            filledCellCount++;
            if (value != solution[index] - '0')
            {
                matchesSolution = false;
            }
        }

        for (var row = 0; row < SudokuBoard.Size; row++)
        {
            MarkRowConflicts(board, conflicts, row);
        }

        for (var column = 0; column < SudokuBoard.Size; column++)
        {
            MarkColumnConflicts(board, conflicts, column);
        }

        for (var boxRow = 0; boxRow < SudokuBoard.Size; boxRow += 3)
        {
            for (var boxColumn = 0; boxColumn < SudokuBoard.Size; boxColumn += 3)
            {
                MarkBoxConflicts(board, conflicts, boxRow, boxColumn);
            }
        }

        return new SudokuBoardAnalysisSnapshot(conflicts, filledCellCount, matchesSolution);
    }

    private static void MarkRowConflicts(SudokuBoard board, bool[] conflicts, int row)
    {
        Span<int> seen = stackalloc int[10];
        seen.Fill(-1);

        for (var column = 0; column < SudokuBoard.Size; column++)
        {
            var index = (row * SudokuBoard.Size) + column;
            MarkIfDuplicate(board.GetValueAtIndex(index), index, seen, conflicts);
        }
    }

    private static void MarkColumnConflicts(SudokuBoard board, bool[] conflicts, int column)
    {
        Span<int> seen = stackalloc int[10];
        seen.Fill(-1);

        for (var row = 0; row < SudokuBoard.Size; row++)
        {
            var index = (row * SudokuBoard.Size) + column;
            MarkIfDuplicate(board.GetValueAtIndex(index), index, seen, conflicts);
        }
    }

    private static void MarkBoxConflicts(SudokuBoard board, bool[] conflicts, int boxRow, int boxColumn)
    {
        Span<int> seen = stackalloc int[10];
        seen.Fill(-1);

        for (var row = boxRow; row < boxRow + 3; row++)
        {
            for (var column = boxColumn; column < boxColumn + 3; column++)
            {
                var index = (row * SudokuBoard.Size) + column;
                MarkIfDuplicate(board.GetValueAtIndex(index), index, seen, conflicts);
            }
        }
    }

    private static void MarkIfDuplicate(int value, int index, Span<int> seen, bool[] conflicts)
    {
        if (value == 0)
        {
            return;
        }

        var firstIndex = seen[value];
        if (firstIndex >= 0)
        {
            conflicts[firstIndex] = true;
            conflicts[index] = true;
            return;
        }

        seen[value] = index;
    }
}
