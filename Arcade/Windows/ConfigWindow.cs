using System;
using System.Numerics;
using Arcade.Games.Hangman;
using Arcade.Games.Sudoku;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Arcade.Windows;

public class ConfigWindow : Window, IDisposable
{
    private static readonly HangmanDifficulty[] HangmanDifficultyOptions =
    [
        HangmanDifficulty.Any,
        HangmanDifficulty.Easy,
        HangmanDifficulty.Medium,
        HangmanDifficulty.Hard,
    ];

    private static readonly SudokuDifficulty[] SudokuDifficultyOptions =
    [
        SudokuDifficulty.Any,
        SudokuDifficulty.Easy,
        SudokuDifficulty.Medium,
        SudokuDifficulty.Hard,
    ];

    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin)
        : base("Arcade Settings###ArcadeConfigWindow")
    {
        configuration = plugin.Configuration;
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(460, 260);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose()
    {
    }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        ImGui.TextDisabled("General");

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Allow moving this settings window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Separator();
        ImGui.TextDisabled("Game Defaults");
        DrawDefaultHangmanDifficulty();
        DrawDefaultSudokuDifficulty();

        ImGui.Separator();
        ImGui.TextDisabled("Commands");
        ImGui.BulletText($"{PluginCommands.Primary} - Toggle Arcade main window");
    }

    private void DrawDefaultHangmanDifficulty()
    {
        var selected = Enum.IsDefined(configuration.DefaultHangmanDifficulty)
            ? configuration.DefaultHangmanDifficulty
            : HangmanDifficulty.Any;

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Hangman");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180.0f);
        if (ImGui.BeginCombo("##ConfigHangmanDifficulty", FormatHangmanDifficulty(selected)))
        {
            foreach (var option in HangmanDifficultyOptions)
            {
                var isSelected = option == selected;
                if (ImGui.Selectable(FormatHangmanDifficulty(option), isSelected))
                {
                    configuration.DefaultHangmanDifficulty = option;
                    configuration.Save();
                    selected = option;
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawDefaultSudokuDifficulty()
    {
        var selected = Enum.IsDefined(configuration.DefaultSudokuDifficulty)
            ? configuration.DefaultSudokuDifficulty
            : SudokuDifficulty.Any;

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Sudoku");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180.0f);
        if (ImGui.BeginCombo("##ConfigSudokuDifficulty", FormatSudokuDifficulty(selected)))
        {
            foreach (var option in SudokuDifficultyOptions)
            {
                var isSelected = option == selected;
                if (ImGui.Selectable(FormatSudokuDifficulty(option), isSelected))
                {
                    configuration.DefaultSudokuDifficulty = option;
                    configuration.Save();
                    selected = option;
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
    }

    private static string FormatHangmanDifficulty(HangmanDifficulty difficulty)
    {
        return difficulty switch
        {
            HangmanDifficulty.Easy => "Easy",
            HangmanDifficulty.Medium => "Medium",
            HangmanDifficulty.Hard => "Hard",
            _ => "Any",
        };
    }

    private static string FormatSudokuDifficulty(SudokuDifficulty difficulty)
    {
        return difficulty switch
        {
            SudokuDifficulty.Easy => "Easy",
            SudokuDifficulty.Medium => "Medium",
            SudokuDifficulty.Hard => "Hard",
            _ => "Any",
        };
    }
}
