using System;

namespace Arcade.Games.Sudoku;

public sealed class SudokuBoard
{
    public const int Size = 9;
    public const int CellCount = Size * Size;

    private readonly byte[] givens = new byte[CellCount];
    private readonly byte[] playerValues = new byte[CellCount];
    private readonly ushort[] noteMasks = new ushort[CellCount];

    public int GivenCellCount { get; private set; }

    public void LoadPuzzle(SudokuPuzzle puzzle)
    {
        if (puzzle.Givens.Length != CellCount)
        {
            throw new ArgumentException("Puzzle givens must be exactly 81 characters long.", nameof(puzzle));
        }

        GivenCellCount = 0;
        for (var index = 0; index < CellCount; index++)
        {
            var given = ToDigit(puzzle.Givens[index]);
            givens[index] = given;
            playerValues[index] = 0;
            noteMasks[index] = 0;

            if (given != 0)
            {
                GivenCellCount++;
            }
        }
    }

    public bool IsInBounds(SudokuCoordinate coordinate)
    {
        return coordinate.Row is >= 0 and < Size
            && coordinate.Column is >= 0 and < Size;
    }

    public bool IsGiven(SudokuCoordinate coordinate)
    {
        return GetGivenValue(coordinate) != 0;
    }

    public int GetGivenValue(SudokuCoordinate coordinate)
    {
        return givens[GetIndex(coordinate)];
    }

    public int GetPlayerValue(SudokuCoordinate coordinate)
    {
        return playerValues[GetIndex(coordinate)];
    }

    public int GetValue(SudokuCoordinate coordinate)
    {
        var index = GetIndex(coordinate);
        return givens[index] != 0 ? givens[index] : playerValues[index];
    }

    public ushort GetNoteMask(SudokuCoordinate coordinate)
    {
        return noteMasks[GetIndex(coordinate)];
    }

    public bool HasNote(SudokuCoordinate coordinate, int value)
    {
        if (value is < 1 or > 9)
        {
            return false;
        }

        var mask = (ushort)(1 << (value - 1));
        return (GetNoteMask(coordinate) & mask) != 0;
    }

    public bool HasProgress()
    {
        for (var index = 0; index < CellCount; index++)
        {
            if (playerValues[index] != 0 || noteMasks[index] != 0)
            {
                return true;
            }
        }

        return false;
    }

    internal int GetValueAtIndex(int index)
    {
        return givens[index] != 0 ? givens[index] : playerValues[index];
    }

    internal ushort GetNoteMaskAtIndex(int index)
    {
        return noteMasks[index];
    }

    internal bool SetPlayerValue(SudokuCoordinate coordinate, int value)
    {
        var index = GetIndex(coordinate);
        var normalizedValue = (byte)value;
        if (playerValues[index] == normalizedValue && noteMasks[index] == 0)
        {
            return false;
        }

        playerValues[index] = normalizedValue;
        noteMasks[index] = 0;
        return true;
    }

    internal bool ToggleNote(SudokuCoordinate coordinate, int value, out bool isNowSet)
    {
        var index = GetIndex(coordinate);
        var mask = (ushort)(1 << (value - 1));
        var oldMask = noteMasks[index];
        var newMask = (ushort)(oldMask ^ mask);
        noteMasks[index] = newMask;
        isNowSet = (newMask & mask) != 0;
        return newMask != oldMask;
    }

    internal bool ClearNotes(SudokuCoordinate coordinate)
    {
        var index = GetIndex(coordinate);
        if (noteMasks[index] == 0)
        {
            return false;
        }

        noteMasks[index] = 0;
        return true;
    }

    internal static int GetIndex(SudokuCoordinate coordinate)
    {
        if (coordinate.Row is < 0 or >= Size)
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Row must be between 0 and 8.");
        }

        if (coordinate.Column is < 0 or >= Size)
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Column must be between 0 and 8.");
        }

        return (coordinate.Row * Size) + coordinate.Column;
    }

    private static byte ToDigit(char value)
    {
        return value is >= '0' and <= '9'
            ? (byte)(value - '0')
            : throw new ArgumentOutOfRangeException(nameof(value), "Grid characters must be digits.");
    }
}
