using System;
using System.Numerics;
using Arcade.Games.Minesweeper;
using Arcade.Stats;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Arcade.Modules;

public sealed class MinesweeperModule : IArcadeModule
{
    private const int MinBoardSide = 5;
    private const int MaxBoardWidth = 60;
    private const int MaxBoardHeight = 40;
    private const float AdjacentNumberTextScale = 1.25f;
    private static readonly string[] AdjacentMineText = ["", "1", "2", "3", "4", "5", "6", "7", "8"];

    private readonly IAccountStatsService accountStatsService;
    private readonly uint[] numberColorsU32;
    private MinesweeperGame game;
    private DifficultyPreset selectedPreset;
    private int customWidth;
    private int customHeight;
    private int customMines;
    private string? validationMessage;

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
        ImGui.Text("Minesweeper");
        DrawGameConfiguration();
        ImGui.Separator();
        ImGui.Text($"State: {game.State}");
        ImGui.Text($"Board: {game.Settings.Width} x {game.Settings.Height}");
        ImGui.Text($"Mines (total): {game.Settings.MineCount}");
        ImGui.SameLine();
        ImGui.Text($"Mines (remaining): {game.RemainingMinesEstimate}");
        ImGui.Text($"Time: {TimeText.FormatElapsedCompact(game.Elapsed)}");
        ImGui.Text($"Revealed safe tiles: {game.RevealedSafeTileCount}/{game.Board.SafeTileCount}");
        ImGui.Text($"Flags placed: {game.FlaggedTileCount}");

        if (ImGui.Button("Restart Current Board"))
        {
            game.Reset();
        }

        ImGui.Separator();
        DrawBoard();
    }

    private void DrawGameConfiguration()
    {
        ImGui.Text("Game Configuration");

        if (ImGui.RadioButton("Beginner (9x9, 10)", selectedPreset == DifficultyPreset.Beginner) &&
            selectedPreset != DifficultyPreset.Beginner)
        {
            selectedPreset = DifficultyPreset.Beginner;
            ApplySelectedPreset();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Intermediate (16x16, 40)", selectedPreset == DifficultyPreset.Intermediate) &&
            selectedPreset != DifficultyPreset.Intermediate)
        {
            selectedPreset = DifficultyPreset.Intermediate;
            ApplySelectedPreset();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Expert (30x16, 99)", selectedPreset == DifficultyPreset.Expert) &&
            selectedPreset != DifficultyPreset.Expert)
        {
            selectedPreset = DifficultyPreset.Expert;
            ApplySelectedPreset();
        }

        if (ImGui.RadioButton("Custom", selectedPreset == DifficultyPreset.Custom))
        {
            selectedPreset = DifficultyPreset.Custom;
            validationMessage = null;
            SyncCustomInputsFromCurrentGame();
        }

        if (selectedPreset == DifficultyPreset.Custom)
        {
            ImGui.PushItemWidth(100);
            if (ImGui.InputInt("Width", ref customWidth))
            {
                validationMessage = null;
            }

            if (ImGui.InputInt("Height", ref customHeight))
            {
                validationMessage = null;
            }

            if (ImGui.InputInt("Mines", ref customMines))
            {
                validationMessage = null;
            }

            ImGui.PopItemWidth();

            var safeWidth = Math.Clamp(customWidth, MinBoardSide, MaxBoardWidth);
            var safeHeight = Math.Clamp(customHeight, MinBoardSide, MaxBoardHeight);
            var maxCustomMines = Math.Max(0L, ((long)safeWidth * safeHeight) - 1);
            ImGui.TextDisabled($"Valid ranges: Width {MinBoardSide}-{MaxBoardWidth}, Height {MinBoardSide}-{MaxBoardHeight}, Mines 0-{maxCustomMines}");

            if (ImGui.Button("Apply Custom + New Game"))
            {
                ApplyCustomSettings();
            }
        }

        if (!string.IsNullOrEmpty(validationMessage))
        {
            ImGui.TextColored(new Vector4(0.95f, 0.30f, 0.30f, 1.0f), validationMessage);
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
        validationMessage = null;
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
        validationMessage = null;

        customWidth = Math.Clamp(customWidth, MinBoardSide, MaxBoardWidth);
        customHeight = Math.Clamp(customHeight, MinBoardSide, MaxBoardHeight);

        var maxMines = (customWidth * customHeight) - 1;
        customMines = Math.Clamp(customMines, 0, maxMines);

        try
        {
            var customSettings = new MinesweeperGameSettings(customWidth, customHeight, customMines);
            ReplaceGame(customSettings);
            SyncCustomInputsFromCurrentGame();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            validationMessage = ex.Message;
        }
    }

    private void SyncCustomInputsFromCurrentGame()
    {
        customWidth = game.Settings.Width;
        customHeight = game.Settings.Height;
        customMines = game.Settings.MineCount;
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
