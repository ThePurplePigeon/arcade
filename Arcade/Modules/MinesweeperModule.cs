using System;
using System.Numerics;
using Arcade.Games.Minesweeper;
using Arcade.Stats;
using Arcade.Ui;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Arcade.Modules;

public sealed class MinesweeperModule : IArcadeModule
{
    private const float StepperButtonWidth = 26.0f;
    private const float StepperValueWidth = 52.0f;
    private const float HeaderActionButtonWidth = 106.0f;
    private const float AdjacentNumberTextScale = 1.25f;
    private static readonly string[] AdjacentMineText = ["", "1", "2", "3", "4", "5", "6", "7", "8"];
    private static readonly DifficultyPreset[] PresetOptions =
    [
        DifficultyPreset.Beginner,
        DifficultyPreset.Intermediate,
        DifficultyPreset.Expert,
        DifficultyPreset.Custom,
    ];

    private readonly IAccountStatsService accountStatsService;
    private readonly uint[] numberColorsU32;
    private MinesweeperGame game;
    private DifficultyPreset selectedPreset;
    private int customWidth;
    private int customHeight;
    private int customMines;

    public MinesweeperModule(IAccountStatsService accountStatsService)
    {
        this.accountStatsService = accountStatsService ?? throw new ArgumentNullException(nameof(accountStatsService));
        selectedPreset = DifficultyPreset.Beginner;
        customWidth = MinesweeperGameSettings.Beginner.Width;
        customHeight = MinesweeperGameSettings.Beginner.Height;
        customMines = MinesweeperGameSettings.Beginner.MineCount;
        game = new MinesweeperGame(MinesweeperGameSettings.Beginner);
        game.MatchCompleted += OnMatchCompleted;
        numberColorsU32 =
        [
            0,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.20f, 0.40f, 1.00f, 1.00f)), // 1
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.60f, 0.20f, 1.00f)), // 2
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.90f, 0.20f, 0.20f, 1.00f)), // 3
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.20f, 0.20f, 0.80f, 1.00f)), // 4
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.60f, 0.10f, 0.10f, 1.00f)), // 5
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.50f, 0.50f, 1.00f)), // 6
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.10f, 0.10f, 1.00f)), // 7
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.45f, 0.45f, 0.45f, 1.00f)), // 8
        ];
    }

    public string Name => "Minesweeper";

    public void Dispose()
    {
        game.MatchCompleted -= OnMatchCompleted;
    }

    public void Draw()
    {
        DrawHeaderRow();
        ArcadeUi.DrawCompactStatusRow(
            ("State", FormatState(game.State)),
            ("Board", $"{game.Settings.Width}x{game.Settings.Height}"),
            ("Mines", $"{game.RemainingMinesEstimate}/{game.Settings.MineCount}"),
            ("Time", TimeText.FormatElapsedCompact(game.Elapsed)),
            ("Revealed", $"{game.RevealedSafeTileCount}/{game.Board.SafeTileCount}"),
            ("Flags", game.FlaggedTileCount.ToString()));

        DrawGameConfiguration();
        ImGui.Separator();
        DrawBoard();
        ArcadeUi.DrawSecondaryText("Left click to reveal, right click to flag, middle click to chord-reveal.");
    }

    private void DrawHeaderRow()
    {
        ImGui.Text("Minesweeper");

        var style = ImGui.GetStyle();
        var actionsWidth = (HeaderActionButtonWidth * 2.0f) + style.ItemSpacing.X;
        var startX = MathF.Max(
            ImGui.GetCursorPosX() + style.ItemSpacing.X,
            ImGui.GetWindowContentRegionMax().X - actionsWidth);

        ImGui.SameLine(startX);
        if (ImGui.Button("New Game", new Vector2(HeaderActionButtonWidth, 0.0f)))
        {
            ReplaceGame(game.Settings);
            SyncCustomInputsFromCurrentGame();
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset Board", new Vector2(HeaderActionButtonWidth, 0.0f)))
        {
            game.Reset();
        }
    }

    private void DrawGameConfiguration()
    {
        ArcadeUi.DrawSectionLabel("Setup");
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Preset");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220.0f);
        if (ImGui.BeginCombo("##MinesweeperPreset", FormatPreset(selectedPreset)))
        {
            foreach (var option in PresetOptions)
            {
                var isSelected = option == selectedPreset;
                if (ImGui.Selectable(FormatPreset(option), isSelected))
                {
                    if (option != selectedPreset)
                    {
                        selectedPreset = option;
                        if (selectedPreset == DifficultyPreset.Custom)
                        {
                            SyncCustomInputsFromCurrentGame();
                        }
                        else
                        {
                            ApplySelectedPreset();
                        }
                    }
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        if (selectedPreset != DifficultyPreset.Custom)
        {
            return;
        }

        DrawCustomStepper("Width", ref customWidth, MinesweeperCustomSettingsNormalizer.MinBoardSide, MinesweeperCustomSettingsNormalizer.MaxBoardWidth);
        DrawCustomStepper("Height", ref customHeight, MinesweeperCustomSettingsNormalizer.MinBoardSide, MinesweeperCustomSettingsNormalizer.MaxBoardHeight);

        NormalizeCustomInputs();
        var maxCustomMines = MinesweeperCustomSettingsNormalizer.GetMaxMineCount(customWidth, customHeight);
        DrawCustomStepper("Mines", ref customMines, 0, maxCustomMines);
        NormalizeCustomInputs();

        ArcadeUi.DrawSecondaryText($"Ranges: Width {MinesweeperCustomSettingsNormalizer.MinBoardSide}-{MinesweeperCustomSettingsNormalizer.MaxBoardWidth}, Height {MinesweeperCustomSettingsNormalizer.MinBoardSide}-{MinesweeperCustomSettingsNormalizer.MaxBoardHeight}, Mines 0-{maxCustomMines}");

        if (ImGui.Button("Apply Custom + New Game"))
        {
            ApplyCustomSettings();
        }
    }

    private void DrawBoard()
    {
        var board = game.Board;
        var regionSize = ImGui.GetContentRegionAvail();
        if (regionSize.X < 1 || regionSize.Y < 1)
        {
            return;
        }

        const float maxTileSize = 42.0f;
        const float minTileSize = 12.0f;
        const float boardPadding = 8.0f;

        using var boardChild = ImRaii.Child("MinesweeperBoardRegion", regionSize, true);
        var childAvail = ImGui.GetContentRegionAvail();
        var fitWidth = (childAvail.X - boardPadding * 2) / board.Width;
        var fitHeight = (childAvail.Y - boardPadding * 2) / board.Height;
        var tileSize = MathF.Min(fitWidth, fitHeight);
        tileSize = MathF.Max(minTileSize, MathF.Min(maxTileSize, tileSize));

        var boardSize = new Vector2(tileSize * board.Width, tileSize * board.Height);
        var spareSpace = childAvail - boardSize;
        var origin = ImGui.GetCursorScreenPos() + new Vector2(
            boardPadding + MathF.Max(0, spareSpace.X * 0.5f),
            boardPadding + MathF.Max(0, spareSpace.Y * 0.5f));
        var drawList = ImGui.GetWindowDrawList();
        var colors = BuildBoardColors();

        for (var y = 0; y < board.Height; y++)
        {
            for (var x = 0; x < board.Width; x++)
            {
                var coordinate = new MinesweeperCoordinate(x, y);
                var tile = board.GetTile(coordinate);

                var tileMin = origin + new Vector2(x * tileSize, y * tileSize);
                var tileMax = tileMin + new Vector2(tileSize, tileSize);

                ImGui.SetCursorScreenPos(tileMin);
                ImGui.PushID(y * board.Width + x);
                ImGui.InvisibleButton("tile", new Vector2(tileSize, tileSize));
                var isHovered = ImGui.IsItemHovered();
                var isPressed = ImGui.IsItemActive();

                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    game.Reveal(coordinate);
                }
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    game.ToggleFlag(coordinate);
                }
                else if (ImGui.IsItemClicked(ImGuiMouseButton.Middle))
                {
                    game.ChordReveal(coordinate);
                }

                ImGui.PopID();
                DrawTile(drawList, tileMin, tileMax, tileSize, tile, colors, isHovered, isPressed);
            }
        }

        ImGui.SetCursorScreenPos(origin + boardSize);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawTile(
        ImDrawListPtr drawList,
        Vector2 tileMin,
        Vector2 tileMax,
        float tileSize,
        MinesweeperTile tile,
        BoardColors colors,
        bool isHovered,
        bool isPressed)
    {
        var isGameLost = game.State == MinesweeperGameState.Lost;
        var shouldShowMine = tile.HasMine && (tile.IsRevealed || isGameLost);
        var fillColor = tile.IsRevealed ? colors.Revealed : colors.Hidden;

        if (!tile.IsRevealed)
        {
            if (isPressed)
            {
                fillColor = colors.HiddenPressed;
            }
            else if (isHovered)
            {
                fillColor = colors.HiddenHovered;
            }
        }

        if (tile.WasExploded)
        {
            fillColor = colors.ExplodedMine;
        }

        drawList.AddRectFilled(tileMin, tileMax, fillColor);
        drawList.AddRect(tileMin, tileMax, colors.Border);

        var center = (tileMin + tileMax) * 0.5f;

        if (tile.IsWrongFlag)
        {
            DrawCenteredText(drawList, center, "X", colors.WrongFlag);
            return;
        }

        if (tile.IsFlagged && !tile.IsRevealed && !shouldShowMine)
        {
            DrawCenteredText(drawList, center, "F", colors.Flag);
            return;
        }

        if (shouldShowMine)
        {
            var mineRadius = tileSize * 0.22f;
            drawList.AddCircleFilled(center, mineRadius, colors.Mine, 16);
            if (tile.IsFlagged)
            {
                DrawCenteredText(drawList, center, "F", colors.Flag);
            }

            return;
        }

        if (tile.IsRevealed && tile.AdjacentMineCount > 0)
        {
            var value = tile.AdjacentMineCount;
            var clamped = value > 8 ? 8 : value;
            var numberColor = numberColorsU32[clamped];
            DrawCenteredScaledText(
                drawList,
                center,
                AdjacentMineText[clamped],
                numberColor == 0 ? colors.Text : numberColor,
                AdjacentNumberTextScale);
        }
    }

    private static void DrawCenteredText(ImDrawListPtr drawList, Vector2 center, string text, uint color)
    {
        var size = ImGui.CalcTextSize(text);
        var pos = center - (size * 0.5f);
        drawList.AddText(pos, color, text);
    }

    private static void DrawCenteredScaledText(ImDrawListPtr drawList, Vector2 center, string text, uint color, float scale)
    {
        var safeScale = MathF.Max(0.1f, scale);
        var size = ImGui.CalcTextSize(text) * safeScale;
        var pos = center - (size * 0.5f);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * safeScale, pos, color, text);
    }

    private static BoardColors BuildBoardColors()
    {
        return new BoardColors(
            ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.08f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.38f, 0.38f, 0.42f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.44f, 0.44f, 0.48f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.33f, 0.33f, 0.37f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.80f, 0.80f, 0.83f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.12f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.80f, 0.18f, 0.18f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.95f, 0.20f, 0.20f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.95f, 0.35f, 0.35f, 1.0f)),
            ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.05f, 1.0f)));
    }

    private void ApplySelectedPreset()
    {
        var settings = selectedPreset switch
        {
            DifficultyPreset.Beginner => MinesweeperGameSettings.Beginner,
            DifficultyPreset.Intermediate => MinesweeperGameSettings.Intermediate,
            DifficultyPreset.Expert => MinesweeperGameSettings.Expert,
            _ => null,
        };

        if (settings is null)
        {
            return;
        }

        ReplaceGame(settings);
        SyncCustomInputsFromCurrentGame();
    }

    private void ApplyCustomSettings()
    {
        var normalized = NormalizeCustomInputs();
        var customSettings = new MinesweeperGameSettings(normalized.Width, normalized.Height, normalized.MineCount);
        ReplaceGame(customSettings);
        SyncCustomInputsFromCurrentGame();
    }

    private void SyncCustomInputsFromCurrentGame()
    {
        var normalized = MinesweeperCustomSettingsNormalizer.Normalize(
            game.Settings.Width,
            game.Settings.Height,
            game.Settings.MineCount);
        customWidth = normalized.Width;
        customHeight = normalized.Height;
        customMines = normalized.MineCount;
    }

    private MinesweeperCustomSettings NormalizeCustomInputs()
    {
        var normalized = MinesweeperCustomSettingsNormalizer.Normalize(customWidth, customHeight, customMines);
        customWidth = normalized.Width;
        customHeight = normalized.Height;
        customMines = normalized.MineCount;
        return normalized;
    }

    private static void DrawCustomStepper(string label, ref int value, int minValue, int maxValue)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(label);
        ImGui.SameLine();
        ImGui.PushID(label);

        ImGui.BeginDisabled(value <= minValue);
        if (ImGui.Button("-", new Vector2(StepperButtonWidth, 0)))
        {
            value--;
        }

        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled();
        ImGui.Button(value.ToString(), new Vector2(StepperValueWidth, 0));
        ImGui.EndDisabled();
        ImGui.SameLine();

        ImGui.BeginDisabled(value >= maxValue);
        if (ImGui.Button("+", new Vector2(StepperButtonWidth, 0)))
        {
            value++;
        }

        ImGui.EndDisabled();
        ImGui.PopID();
    }

    private void ReplaceGame(MinesweeperGameSettings settings)
    {
        game.MatchCompleted -= OnMatchCompleted;
        game = new MinesweeperGame(settings);
        game.MatchCompleted += OnMatchCompleted;
    }

    private void OnMatchCompleted(MinesweeperMatchSummary summary)
    {
        accountStatsService.RecordMinesweeperResult(summary);
        accountStatsService.Save();
    }

    private static string FormatState(MinesweeperGameState state)
    {
        return state switch
        {
            MinesweeperGameState.InProgress => "In Progress",
            MinesweeperGameState.Won => "Won",
            MinesweeperGameState.Lost => "Lost",
            _ => "Ready",
        };
    }

    private static string FormatPreset(DifficultyPreset preset)
    {
        return preset switch
        {
            DifficultyPreset.Beginner => "Beginner (9x9, 10)",
            DifficultyPreset.Intermediate => "Intermediate (16x16, 40)",
            DifficultyPreset.Expert => "Expert (30x16, 99)",
            _ => "Custom",
        };
    }

    private enum DifficultyPreset
    {
        Beginner,
        Intermediate,
        Expert,
        Custom,
    }

    private readonly record struct BoardColors(
        uint Border,
        uint Hidden,
        uint HiddenHovered,
        uint HiddenPressed,
        uint Revealed,
        uint Mine,
        uint ExplodedMine,
        uint Flag,
        uint WrongFlag,
        uint Text);
}
