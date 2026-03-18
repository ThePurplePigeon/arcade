using Arcade.Games.Hangman;
using Arcade.Games.Sudoku;
using Arcade.Stats;
using Xunit;

namespace Arcade.Tests;

public class ConfigurationMigrationTests
{
    [Fact]
    public void Migrate_InitializesDefaultsFromEmptyState()
    {
        var version = 0;
        var hangman = (HangmanDifficulty)999;
        var sudoku = (SudokuDifficulty)999;
        AccountStatsData? stats = null;

        var changed = ConfigurationMigration.Migrate(ref version, ref hangman, ref sudoku, ref stats);

        Assert.True(changed);
        Assert.Equal(ConfigurationMigration.CurrentVersion, version);
        Assert.Equal(HangmanDifficulty.Any, hangman);
        Assert.Equal(SudokuDifficulty.Any, sudoku);
        Assert.NotNull(stats);
        Assert.NotNull(stats.Hangman);
        Assert.NotNull(stats.Minesweeper);
        Assert.NotNull(stats.Sudoku);
    }

    [Fact]
    public void Migrate_CurrentAndValidState_IsNoOp()
    {
        var version = ConfigurationMigration.CurrentVersion;
        var hangman = HangmanDifficulty.Medium;
        var sudoku = SudokuDifficulty.Hard;
        AccountStatsData? stats = new()
        {
            Hangman = new HangmanAccountStatsData(),
            Minesweeper = new MinesweeperAccountStatsData(),
            Sudoku = new SudokuAccountStatsData(),
        };

        var changed = ConfigurationMigration.Migrate(ref version, ref hangman, ref sudoku, ref stats);

        Assert.False(changed);
        Assert.Equal(ConfigurationMigration.CurrentVersion, version);
        Assert.Equal(HangmanDifficulty.Medium, hangman);
        Assert.Equal(SudokuDifficulty.Hard, sudoku);
    }

    [Fact]
    public void Migrate_RepairsMissingNestedStatsSections()
    {
        var version = ConfigurationMigration.CurrentVersion;
        var hangman = HangmanDifficulty.Any;
        var sudoku = SudokuDifficulty.Any;
        AccountStatsData? stats = new()
        {
            Hangman = null!,
            Minesweeper = null!,
            Sudoku = null!,
        };

        var changed = ConfigurationMigration.Migrate(ref version, ref hangman, ref sudoku, ref stats);

        Assert.True(changed);
        Assert.NotNull(stats);
        Assert.NotNull(stats.Hangman);
        Assert.NotNull(stats.Minesweeper);
        Assert.NotNull(stats.Sudoku);
    }
}
