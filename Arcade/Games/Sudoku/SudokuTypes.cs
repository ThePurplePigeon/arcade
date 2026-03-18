using System;
using System.Collections.Generic;

namespace Arcade.Games.Sudoku;

public readonly record struct SudokuCoordinate(int Row, int Column);

public enum SudokuDifficulty
{
    Any,
    Easy,
    Medium,
    Hard,
}

public enum SudokuGameState
{
    Ready,
    InProgress,
    Completed,
}

public enum SudokuMoveResult
{
    NoChange,
    ValueSet,
    ValueCleared,
    NoteAdded,
    NoteRemoved,
    InvalidMove,
    Completed,
}

public enum SudokuPuzzleOutcome
{
    Completed,
    Abandoned,
}

public readonly record struct SudokuPuzzle(
    string Id,
    SudokuDifficulty Difficulty,
    string Givens,
    string Solution,
    string RotationGroupTag = "",
    bool IsRotatedVariant = false)
{
    public string SelectionGroupTag => string.IsNullOrWhiteSpace(RotationGroupTag) ? Id : RotationGroupTag;
}

public readonly record struct SudokuPuzzleSummary(
    SudokuPuzzleOutcome Outcome,
    string PuzzleId,
    SudokuDifficulty Difficulty,
    TimeSpan Elapsed,
    int FilledCellCount,
    int GivenCellCount);

public sealed class SudokuGameSettings
{
    public SudokuGameSettings(SudokuDifficulty defaultDifficulty = SudokuDifficulty.Any)
    {
        DefaultDifficulty = defaultDifficulty;
    }

    public SudokuDifficulty DefaultDifficulty { get; }
}

public interface ISudokuPuzzleProvider
{
    IReadOnlyList<SudokuPuzzle> GetPuzzles();
}
