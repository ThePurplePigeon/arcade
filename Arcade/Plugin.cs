using Arcade.Games.Hangman;
using Arcade.Games.Sudoku;
using Arcade.Stats;
using Arcade.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Arcade;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public IAccountStatsService AccountStatsService { get; }
    public WindowSystem WindowSystem { get; } = new("Arcade");

    private ConfigWindow ConfigWindow { get; }
    private AccountStatsWindow AccountStatsWindow { get; }
    private MainWindow MainWindow { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.Migrate())
        {
            Configuration.Save();
        }

        FileHangmanWordProvider.WarningSink ??= static message => Log.Warning(message);
        FileSudokuPuzzleProvider.WarningSink ??= static message => Log.Warning(message);

        AccountStatsService = new AccountStatsService(Configuration.AccountStats, Configuration.Save);

        ConfigWindow = new ConfigWindow(this);
        AccountStatsWindow = new AccountStatsWindow(AccountStatsService);
        MainWindow = new MainWindow(this, AccountStatsService);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(AccountStatsWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(PluginCommands.Primary, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open or close the Arcade window."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"Initialized {PluginInterface.Manifest.Name}.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        AccountStatsWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(PluginCommands.Primary);
    }

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleAccountStatsUi() => AccountStatsWindow.Toggle();
}
