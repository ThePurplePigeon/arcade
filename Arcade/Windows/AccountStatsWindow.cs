using System;
using System.Numerics;
using Arcade.Stats;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Arcade.Windows;

public sealed class AccountStatsWindow : Window, IDisposable
{
    private readonly IAccountStatsService accountStatsService;

    public AccountStatsWindow(IAccountStatsService accountStatsService)
        : base("Arcade Account Stats###ArcadeAccountStatsWindow")
    {
        this.accountStatsService = accountStatsService ?? throw new ArgumentNullException(nameof(accountStatsService));
        Size = new Vector2(500, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var stats = accountStatsService.GetSnapshot();

        if (ImGui.CollapsingHeader("Hangman", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawHangmanStats(stats.Hangman);
        }

        if (ImGui.CollapsingHeader("Minesweeper", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawMinesweeperStats(stats.Minesweeper);
        }

        if (ImGui.CollapsingHeader("Sudoku", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawSudokuStats(stats.Sudoku);
        }
    }

    private static void DrawHangmanStats(HangmanAccountStatsData stats)
    {
        if (stats.RoundsPlayed == 0)
        {
            ImGui.TextDisabled("No data yet.");
            return;
        }

        ImGui.Text($"Rounds: {stats.RoundsPlayed}");
        ImGui.Text($"Wins / Losses: {stats.Wins} / {stats.Losses}");
        ImGui.Text($"Win Rate: {FormatPercent(stats.Wins, stats.RoundsPlayed)}");
        ImGui.Text($"Best Streak: {stats.BestWinStreak}");
        ImGui.Text($"Total Wrong Guesses: {stats.TotalWrongGuesses}");
        ImGui.Text($"Avg Wrong / Round: {FormatAverage(HangmanStatsMath.GetAverageWrongGuessesPerRound(stats))}");
        ImGui.Text($"Avg Wrong / Win: {FormatAverage(HangmanStatsMath.GetAverageWrongGuessesOnWins(stats))}");
        ImGui.Separator();
        ImGui.TextDisabled("Rounds By Difficulty");
        ImGui.Text($"Any: {stats.AnyRoundsPlayed}");
        ImGui.Text($"Easy: {stats.EasyRoundsPlayed}");
        ImGui.Text($"Medium: {stats.MediumRoundsPlayed}");
        ImGui.Text($"Hard: {stats.HardRoundsPlayed}");
    }

    private static void DrawMinesweeperStats(MinesweeperAccountStatsData stats)
    {
        if (stats.GamesPlayed == 0)
        {
            ImGui.TextDisabled("No data yet.");
            return;
        }

        ImGui.Text($"Games: {stats.GamesPlayed}");
        ImGui.Text($"Wins / Losses: {stats.Wins} / {stats.Losses}");
        ImGui.Text($"Win Rate: {FormatPercent(stats.Wins, stats.GamesPlayed)}");
        if (stats.BestWinSeconds.HasValue)
        {
            ImGui.Text($"Fastest Win: {TimeText.FormatElapsedCompact(TimeSpan.FromSeconds(stats.BestWinSeconds.Value))}");
        }
        else
        {
            ImGui.Text("Fastest Win: -");
        }
    }

    private static void DrawSudokuStats(SudokuAccountStatsData stats)
    {
        if (stats.PuzzlesPlayed == 0)
        {
            ImGui.TextDisabled("No data yet.");
            return;
        }

        ImGui.Text($"Puzzles: {stats.PuzzlesPlayed}");
        ImGui.Text($"Completed / Abandoned: {stats.Completed} / {stats.Abandoned}");
        ImGui.Text($"Completion Rate: {FormatPercent(stats.Completed, stats.PuzzlesPlayed)}");
        ImGui.Separator();
        ImGui.TextDisabled("Completed By Difficulty");
        ImGui.Text($"Easy: {stats.EasyCompleted}");
        ImGui.Text($"Medium: {stats.MediumCompleted}");
        ImGui.Text($"Hard: {stats.HardCompleted}");
        ImGui.Separator();
        ImGui.Text($"Best Easy: {FormatNullableDuration(stats.BestEasyCompletionSeconds)}");
        ImGui.Text($"Best Medium: {FormatNullableDuration(stats.BestMediumCompletionSeconds)}");
        ImGui.Text($"Best Hard: {FormatNullableDuration(stats.BestHardCompletionSeconds)}");
    }

    private static string FormatPercent(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return "0.0%";
        }

        return $"{(numerator * 100.0 / denominator):0.0}%";
    }

    private static string FormatAverage(double? value)
    {
        return value.HasValue ? $"{value.Value:0.0}" : "-";
    }

    private static string FormatNullableDuration(double? totalSeconds)
    {
        return totalSeconds.HasValue ? TimeText.FormatElapsedCompact(TimeSpan.FromSeconds(totalSeconds.Value)) : "-";
    }
}
