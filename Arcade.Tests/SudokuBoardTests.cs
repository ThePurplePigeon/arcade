using System;
using Arcade.Games.Sudoku;
using Xunit;

namespace Arcade.Tests;

public class SudokuBoardTests
{
    private const string Solution = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
    private const string OneBlankGivens = "534678912672195348198342567859761423426853791713924856961537284287419635345286170";

    [Fact]
    public void LoadPuzzle_PopulatesGivensAndGivenCount()
    {
        var board = new SudokuBoard();
        board.LoadPuzzle(new SudokuPuzzle("board", SudokuDifficulty.Easy, OneBlankGivens, Solution));

        Assert.Equal(80, board.GivenCellCount);
        Assert.Equal(5, board.GetValue(new SudokuCoordinate(0, 0)));
        Assert.Equal(0, board.GetValue(new SudokuCoordinate(8, 8)));
        Assert.True(board.IsGiven(new SudokuCoordinate(0, 0)));
        Assert.False(board.IsGiven(new SudokuCoordinate(8, 8)));
    }

    [Fact]
    public void SetPlayerValue_TracksProgressAndCanBeCleared()
    {
        var board = new SudokuBoard();
        board.LoadPuzzle(new SudokuPuzzle("board", SudokuDifficulty.Easy, OneBlankGivens, Solution));
        var coordinate = new SudokuCoordinate(8, 8);

        Assert.False(board.HasProgress());
        Assert.True(board.SetPlayerValue(coordinate, 9));
        Assert.True(board.HasProgress());
        Assert.Equal(9, board.GetPlayerValue(coordinate));

        Assert.True(board.SetPlayerValue(coordinate, 0));
        Assert.False(board.HasProgress());
        Assert.Equal(0, board.GetPlayerValue(coordinate));
    }

    [Fact]
    public void ToggleNote_SetsAndClearsMask()
    {
        var board = new SudokuBoard();
        board.LoadPuzzle(new SudokuPuzzle("board", SudokuDifficulty.Easy, OneBlankGivens, Solution));
        var coordinate = new SudokuCoordinate(8, 8);

        Assert.True(board.ToggleNote(coordinate, 3, out var setOnFirstToggle));
        Assert.True(setOnFirstToggle);
        Assert.True(board.HasNote(coordinate, 3));

        Assert.True(board.ToggleNote(coordinate, 3, out var setOnSecondToggle));
        Assert.False(setOnSecondToggle);
        Assert.False(board.HasNote(coordinate, 3));
    }

    [Fact]
    public void SetPlayerValue_ClearsExistingNotes()
    {
        var board = new SudokuBoard();
        board.LoadPuzzle(new SudokuPuzzle("board", SudokuDifficulty.Easy, OneBlankGivens, Solution));
        var coordinate = new SudokuCoordinate(8, 8);

        board.ToggleNote(coordinate, 1, out _);
        Assert.NotEqual(0, board.GetNoteMask(coordinate));

        board.SetPlayerValue(coordinate, 9);

        Assert.Equal(0, board.GetNoteMask(coordinate));
    }

    [Fact]
    public void GetIndex_OutOfBounds_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SudokuBoard.GetIndex(new SudokuCoordinate(-1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SudokuBoard.GetIndex(new SudokuCoordinate(0, 9)));
    }
}
