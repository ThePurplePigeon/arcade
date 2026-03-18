using System;
using Arcade.Games.Hangman;
using Arcade.Games.Sudoku;
using Arcade.Stats;

namespace Arcade;

public static class ConfigurationMigration
{
    public const int CurrentVersion = 4;

    public static bool Migrate(
        ref int version,
        ref HangmanDifficulty defaultHangmanDifficulty,
        ref SudokuDifficulty defaultSudokuDifficulty,
        ref AccountStatsData? accountStats)
    {
        var changed = false;

        if (version < 1)
        {
            defaultHangmanDifficulty = HangmanDifficulty.Any;
            defaultSudokuDifficulty = SudokuDifficulty.Any;
            changed = true;
        }

        if (version < 2 || accountStats is null)
        {
            accountStats ??= new AccountStatsData();
            changed = true;
        }

        if (version < 3)
        {
            defaultSudokuDifficulty = SudokuDifficulty.Any;
            changed = true;
        }

        if (!Enum.IsDefined(defaultHangmanDifficulty))
        {
            defaultHangmanDifficulty = HangmanDifficulty.Any;
            changed = true;
        }

        if (!Enum.IsDefined(defaultSudokuDifficulty))
        {
            defaultSudokuDifficulty = SudokuDifficulty.Any;
            changed = true;
        }

        accountStats ??= new AccountStatsData();

        if (accountStats.Hangman is null)
        {
            accountStats.Hangman = new HangmanAccountStatsData();
            changed = true;
        }

        if (accountStats.Minesweeper is null)
        {
            accountStats.Minesweeper = new MinesweeperAccountStatsData();
            changed = true;
        }

        if (accountStats.Sudoku is null)
        {
            accountStats.Sudoku = new SudokuAccountStatsData();
            changed = true;
        }

        if (version != CurrentVersion)
        {
            version = CurrentVersion;
            changed = true;
        }

        return changed;
    }
}
