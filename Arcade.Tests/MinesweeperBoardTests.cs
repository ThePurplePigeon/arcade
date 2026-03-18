using System;
using System.Linq;
using Arcade.Games.Minesweeper;
using Xunit;

namespace Arcade.Tests;

public class MinesweeperBoardTests
{
    [Fact]
    public void PlaceMines_RespectsSafeRadius_WhenItFits()
    {
        var board = new MinesweeperBoard(9, 9, 10);
        var first = new MinesweeperCoordinate(4, 4);

        board.PlaceMines(new Random(301), first, safeRadius: 1);

        Assert.Equal(10, board.GetAllCoordinates().Count(c => board.GetTile(c).HasMine));
        for (var x = first.X - 1; x <= first.X + 1; x++)
        {
            for (var y = first.Y - 1; y <= first.Y + 1; y++)
            {
                Assert.False(board.GetTile(new MinesweeperCoordinate(x, y)).HasMine);
            }
        }
    }

    [Fact]
    public void PlaceMines_DegradesSafeRadius_WhenBoardTooDense()
    {
        var board = new MinesweeperBoard(3, 3, 7);
        var first = new MinesweeperCoordinate(1, 1);

        board.PlaceMines(new Random(302), first, safeRadius: 1);

        Assert.False(board.GetTile(first).HasMine);
        Assert.Equal(7, board.GetAllCoordinates().Count(c => board.GetTile(c).HasMine));
    }

    [Fact]
    public void PlaceMines_NegativeSafeRadius_Throws()
    {
        var board = new MinesweeperBoard(5, 5, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            board.PlaceMines(new Random(303), new MinesweeperCoordinate(0, 0), safeRadius: -1));
    }

    [Fact]
    public void GetTile_OutOfBounds_Throws()
    {
        var board = new MinesweeperBoard(5, 5, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() => board.GetTile(new MinesweeperCoordinate(-1, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => board.GetTile(new MinesweeperCoordinate(5, 0)));
    }

    [Fact]
    public void Clear_ResetsTileState()
    {
        var board = new MinesweeperBoard(5, 5, 4);
        board.PlaceMines(new Random(304), new MinesweeperCoordinate(0, 0), safeRadius: 0);

        var anyMine = board.GetAllCoordinates().First(c => board.GetTile(c).HasMine);
        var anySafe = board.GetAllCoordinates().First(c => !board.GetTile(c).HasMine);
        board.GetTile(anyMine).ToggleFlag();
        board.GetTile(anySafe).TryReveal();

        board.Clear();

        Assert.DoesNotContain(board.GetAllCoordinates(), c => board.GetTile(c).HasMine);
        Assert.DoesNotContain(board.GetAllCoordinates(), c => board.GetTile(c).IsRevealed);
        Assert.DoesNotContain(board.GetAllCoordinates(), c => board.GetTile(c).IsFlagged);
    }
}
