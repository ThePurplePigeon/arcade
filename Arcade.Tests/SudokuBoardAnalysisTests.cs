using Arcade.Games.Sudoku;
using Xunit;

namespace Arcade.Tests;

public class SudokuBoardAnalysisTests
{
    private const string Solution = "534678912672195348198342567859761423426853791713924856961537284287419635345286179";
    private static readonly SudokuPuzzle BlankPuzzle = new("analysis", SudokuDifficulty.Easy, new string('0', 81), Solution);

    [Fact]
    public void Analyze_MarksRowDuplicates()
    {
        var board = CreateBlankBoard();
        board.SetPlayerValue(new SudokuCoordinate(0, 0), 1);
        board.SetPlayerValue(new SudokuCoordinate(0, 1), 1);

        var analysis = SudokuBoardAnalysis.Analyze(board, Solution);

        Assert.True(analysis.IsConflicting(new SudokuCoordinate(0, 0)));
        Assert.True(analysis.IsConflicting(new SudokuCoordinate(0, 1)));
    }

    [Fact]
    public void Analyze_MarksColumnDuplicates()
    {
        var board = CreateBlankBoard();
        board.SetPlayerValue(new SudokuCoordinate(0, 0), 1);
        board.SetPlayerValue(new SudokuCoordinate(1, 0), 1);

        var analysis = SudokuBoardAnalysis.Analyze(board, Solution);

        Assert.True(analysis.IsConflicting(new SudokuCoordinate(0, 0)));
        Assert.True(analysis.IsConflicting(new SudokuCoordinate(1, 0)));
    }

    [Fact]
    public void Analyze_MarksBoxDuplicates()
    {
        var board = CreateBlankBoard();
        board.SetPlayerValue(new SudokuCoordinate(0, 0), 1);
        board.SetPlayerValue(new SudokuCoordinate(1, 1), 1);

        var analysis = SudokuBoardAnalysis.Analyze(board, Solution);

        Assert.True(analysis.IsConflicting(new SudokuCoordinate(0, 0)));
        Assert.True(analysis.IsConflicting(new SudokuCoordinate(1, 1)));
    }

    [Fact]
    public void Analyze_MatchingSolutionIsSolved()
    {
        var board = CreateBlankBoard();
        FillBoard(board, Solution);

        var analysis = SudokuBoardAnalysis.Analyze(board, Solution);

        Assert.Equal(81, analysis.FilledCellCount);
        Assert.True(analysis.IsSolved);
        Assert.False(analysis.HasConflicts);
    }

    [Fact]
    public void Analyze_FullBoardDifferentValidGridIsNotSolved()
    {
        var board = CreateBlankBoard();
        FillBoard(board, "645789123783216459219453678961872534537964812824135967172648395398521746456397281");

        var analysis = SudokuBoardAnalysis.Analyze(board, Solution);

        Assert.Equal(81, analysis.FilledCellCount);
        Assert.False(analysis.IsSolved);
        Assert.False(analysis.HasConflicts);
    }


    [Fact]
    public void Analyze_EmptyBoardIsNotSolvedAndHasNoConflicts()
    {
        var board = CreateBlankBoard();

        var analysis = SudokuBoardAnalysis.Analyze(board, Solution);

        Assert.Equal(0, analysis.FilledCellCount);
        Assert.False(analysis.IsSolved);
        Assert.False(analysis.HasConflicts);
    }

    [Fact]
    public void Analyze_ChangingDuplicateValue_ClearsConflictMarkers()
    {
        var board = CreateBlankBoard();
        var first = new SudokuCoordinate(0, 0);
        var second = new SudokuCoordinate(0, 1);

        board.SetPlayerValue(first, 3);
        board.SetPlayerValue(second, 3);
        var withConflict = SudokuBoardAnalysis.Analyze(board, Solution);
        Assert.True(withConflict.IsConflicting(first));
        Assert.True(withConflict.IsConflicting(second));

        board.SetPlayerValue(second, 4);
        var resolved = SudokuBoardAnalysis.Analyze(board, Solution);

        Assert.False(resolved.IsConflicting(first));
        Assert.False(resolved.IsConflicting(second));
        Assert.False(resolved.HasConflicts);
    }

    private static SudokuBoard CreateBlankBoard()
    {
        var board = new SudokuBoard();
        board.LoadPuzzle(BlankPuzzle);
        return board;
    }

    private static void FillBoard(SudokuBoard board, string values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var row = index / 9;
            var column = index % 9;
            board.SetPlayerValue(new SudokuCoordinate(row, column), values[index] - '0');
        }
    }
}
