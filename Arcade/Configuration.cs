using System;
using Arcade.Games.Hangman;
using Arcade.Games.Sudoku;
using Arcade.Stats;
using Dalamud.Configuration;

namespace Arcade;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public HangmanDifficulty DefaultHangmanDifficulty { get; set; } = HangmanDifficulty.Any;
    public SudokuDifficulty DefaultSudokuDifficulty { get; set; } = SudokuDifficulty.Any;
    public AccountStatsData AccountStats { get; set; } = new();

    public bool Migrate()
    {
        var version = Version;
        var hangmanDifficulty = DefaultHangmanDifficulty;
        var sudokuDifficulty = DefaultSudokuDifficulty;
        AccountStatsData? accountStats = AccountStats;

        var changed = ConfigurationMigration.Migrate(
            ref version,
            ref hangmanDifficulty,
            ref sudokuDifficulty,
            ref accountStats);

        Version = version;
        DefaultHangmanDifficulty = hangmanDifficulty;
        DefaultSudokuDifficulty = sudokuDifficulty;
        AccountStats = accountStats ?? new AccountStatsData();
        return changed;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
