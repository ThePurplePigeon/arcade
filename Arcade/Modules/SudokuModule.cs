using System;
using System.IO;
using System.Numerics;
using Arcade.Games.Sudoku;
using Arcade.Stats;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Arcade.Modules;

public sealed class SudokuModule : IArcadeModule
{
    private const float MaxLayoutWidth = 820.0f;
    private const float MinBoardRegionHeight = 220.0f;
    private const float MaxBoardRegionHeight = 520.0f;
    private const float MinCellSize = 30.0f;
    private const float MaxCellSize = 56.0f;
    private const float DigitButtonSize = 34.0f;

    private static readonly string[] DigitLabels = ["1", "2", "3", "4", "5", "6", "7", "8", "9"];
    private static readonly SudokuDifficulty[] DifficultyOptions =
    [
        SudokuDifficulty.Any,
        SudokuDifficulty.Easy,
        SudokuDifficulty.Medium,
        SudokuDifficulty.Hard,
    ];

    private readonly Configuration configuration;
    private readonly IAccountStatsService accountStatsService;
    private readonly SudokuGame game;

    private SudokuCoordinate? selectedCell;
    private bool noteMode;
    private bool showCheckedErrors;

    public SudokuModule(Configuration configuration, IAccountStatsService accountStatsService)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.accountStatsService = accountStatsService ?? throw new ArgumentNullException(nameof(accountStatsService));

        var assemblyDirectory = Plugin.PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty;
        var puzzlePath = Path.Combine(assemblyDirectory, "sudoku_puzzles.txt");
        var provider = new FileSudokuPuzzleProvider(puzzlePath);
        var defaultDifficulty = Enum.IsDefined(configuration.DefaultSudokuDifficulty)
            ? configuration.DefaultSudokuDifficulty
            : SudokuDifficulty.Any;

        game = new SudokuGame(
            provider,
            new SudokuGameSettings(defaultDifficulty));

        game.PuzzleEnded += OnPuzzleEnded;
    }

    public string Name => "Sudoku";

    public void Dispose()
    {
        game.PuzzleEnded -= OnPuzzleEnded;
    }

    public void Draw()
    {
        var available = ImGui.GetContentRegionAvail();
        var layoutWidth = MathF.Min(MaxLayoutWidth, available.X);
        if (available.X > layoutWidth)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((available.X - layoutWidth) * 0.5f));
        }

        using var layout = ImRaii.Child("SudokuLayout", new Vector2(layoutWidth, 0), false);
        if (!layout.Success)
        {
            return;
        }

        ImGui.Text("Sudoku");
        DrawTopControls();
        ImGui.Spacing();
        DrawStatusRows();

        if (game.State == SudokuGameState.Completed)
        {
            ImGui.Spacing();
            DrawCompletionBanner();
        }

        ImGui.Separator();
        DrawBoard();
        ImGui.Spacing();
        DrawDigitPalette();
    }

    private void DrawTopControls()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Difficulty");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150.0f);

        if (ImGui.BeginCombo("##SudokuDifficulty", FormatDifficulty(game.SelectedDifficulty)))
        {
            foreach (var option in DifficultyOptions)
            {
                var isSelected = option == game.SelectedDifficulty;
                if (ImGui.Selectable(FormatDifficulty(option), isSelected))
                {
                    ApplyDifficulty(option);
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("New Puzzle"))
        {
            game.StartNewPuzzle();
            selectedCell = null;
            showCheckedErrors = false;
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset Puzzle"))
        {
            game.ResetCurrentPuzzle();
            selectedCell = null;
            showCheckedErrors = false;
        }
    }

    private void DrawStatusRows()
    {
        DrawInlineStat("State", FormatState(game.State));
        ImGui.SameLine();
        DrawInlineSeparator();
        ImGui.SameLine();
        DrawInlineStat("Puzzle", FormatDifficulty(game.CurrentPuzzle.Difficulty));
        ImGui.SameLine();
        DrawInlineSeparator();
        ImGui.SameLine();
        DrawInlineStat("Time", TimeText.FormatElapsedCompact(game.Elapsed));
        ImGui.SameLine();
        DrawInlineSeparator();
        ImGui.SameLine();
        DrawInlineStat("Filled", $"{game.Analysis.FilledCellCount}/81");

        var selectionText = selectedCell.HasValue
            ? $"R{selectedCell.Value.Row + 1} C{selectedCell.Value.Column + 1}"
            : "-";
        DrawInlineStat("Selected", selectionText);
        ImGui.SameLine();
        DrawInlineSeparator();
        ImGui.SameLine();
        DrawInlineStat("Mode", noteMode ? "Notes" : "Values");
    }

    private void DrawCompletionBanner()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.30f, 0.20f, 0.30f));
        using var banner = ImRaii.Child("SudokuCompletedBanner", new Vector2(0, 76), true);
        if (banner.Success)
        {
            ImGui.TextColored(new Vector4(0.20f, 0.82f, 0.36f, 1.0f), "Puzzle Complete");
            ImGui.Text($"Difficulty: {FormatDifficulty(game.CurrentPuzzle.Difficulty)}");
            ImGui.SameLine();
            ImGui.Text($"Time: {TimeText.FormatElapsedCompact(game.Elapsed)}");

            if (ImGui.Button("Next Puzzle"))
            {
                game.StartNewPuzzle();
                selectedCell = null;
                showCheckedErrors = false;
            }
        }

        ImGui.PopStyleColor();
    }

    private void DrawBoard()
    {
        var available = ImGui.GetContentRegionAvail();
        var boardRegionHeight = MathF.Min(MaxBoardRegionHeight, MathF.Max(MinBoardRegionHeight, available.Y - 80.0f));
        boardRegionHeight = MathF.Min(boardRegionHeight, available.Y);
        boardRegionHeight = MathF.Max(160.0f, boardRegionHeight);

        using var boardChild = ImRaii.Child("SudokuBoardRegion", new Vector2(0, boardRegionHeight), true);
        if (!boardChild.Success)
        {
            return;
        }

        var childAvail = ImGui.GetContentRegionAvail();
        const float boardPadding = 8.0f;
        var cellSize = MathF.Floor(MathF.Min((childAvail.X - (boardPadding * 2.0f)) / 9.0f, (childAvail.Y - (boardPadding * 2.0f)) / 9.0f));
        cellSize = MathF.Max(MinCellSize, MathF.Min(MaxCellSize, cellSize));

        var boardSize = new Vector2(cellSize * 9.0f, cellSize * 9.0f);
        var spareSpace = childAvail - boardSize;
        var origin = ImGui.GetCursorScreenPos() + new Vector2(
            boardPadding + MathF.Max(0.0f, spareSpace.X * 0.5f),
            boardPadding + MathF.Max(0.0f, spareSpace.Y * 0.5f));

        var drawList = ImGui.GetWindowDrawList();
        var colors = GetBoardColors();
        var valueTextSize = ImGui.CalcTextSize("9");
        var noteTextSize = ImGui.CalcTextSize("9");

        for (var row = 0; row < 9; row++)
        {
            for (var column = 0; column < 9; column++)
            {
                var coordinate = new SudokuCoordinate(row, column);
                var cellMin = origin + new Vector2(column * cellSize, row * cellSize);
                var cellMax = cellMin + new Vector2(cellSize, cellSize);

                ImGui.SetCursorScreenPos(cellMin);
                ImGui.PushID((row * 9) + column);
                ImGui.InvisibleButton("cell", new Vector2(cellSize, cellSize));
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    if (showCheckedErrors && (!selectedCell.HasValue || selectedCell.Value != coordinate))
                    {
                        showCheckedErrors = false;
                    }

                    selectedCell = coordinate;
                }

                ImGui.PopID();
                DrawCell(drawList, coordinate, cellMin, cellMax, cellSize, valueTextSize, noteTextSize, colors);
            }
        }

        DrawGrid(drawList, origin, boardSize, cellSize, colors);

        ImGui.SetCursorScreenPos(origin + boardSize);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawCell(
        ImDrawListPtr drawList,
        SudokuCoordinate coordinate,
        Vector2 cellMin,
        Vector2 cellMax,
        float cellSize,
        Vector2 valueTextSize,
        Vector2 noteTextSize,
        BoardColors colors)
    {
        var isSelected = selectedCell.HasValue && selectedCell.Value == coordinate;
        var isPeer = selectedCell.HasValue && IsPeer(selectedCell.Value, coordinate);
        var isGiven = !game.CanEdit(coordinate);
        var isCheckedError = IsCheckedError(coordinate);

        var fill = isGiven ? colors.GivenBackground : colors.EditableBackground;
        if (game.State == SudokuGameState.Completed)
        {
            fill = Lerp(fill, colors.SolvedTint, 0.25f);
        }

        if (isPeer)
        {
            fill = Lerp(fill, colors.PeerTint, 0.35f);
        }

        if (isSelected)
        {
            fill = colors.SelectedBackground;
        }

        if (isCheckedError)
        {
            fill = Lerp(fill, colors.ConflictTint, isSelected ? 0.45f : 0.70f);
        }

        drawList.AddRectFilled(cellMin, cellMax, ImGui.ColorConvertFloat4ToU32(fill));

        var value = game.Board.GetValue(coordinate);
        if (value != 0)
        {
            var label = DigitLabels[value - 1];
            var textSize = valueTextSize;
            var textColor = isGiven ? colors.GivenText : colors.PlayerText;

            var valuePlateInset = MathF.Max(2.0f, cellSize * 0.18f);
            var valuePlateMin = new Vector2(cellMin.X + valuePlateInset, cellMin.Y + valuePlateInset);
            var valuePlateMax = new Vector2(cellMax.X - valuePlateInset, cellMax.Y - valuePlateInset);
            var valuePlateColor = isGiven ? colors.GivenValuePlate : colors.PlayerValuePlate;
            drawList.AddRectFilled(valuePlateMin, valuePlateMax, ImGui.ColorConvertFloat4ToU32(valuePlateColor), 4.0f);

            var textPosition = new Vector2(
                cellMin.X + ((cellSize - textSize.X) * 0.5f),
                cellMin.Y + ((cellSize - textSize.Y) * 0.5f));
            DrawOutlinedText(drawList, textPosition, label, textColor, 1.0f);
            return;
        }

        var noteMask = game.Board.GetNoteMask(coordinate);
        if (noteMask == 0)
        {
            return;
        }

        var miniCellSize = cellSize / 3.0f;
        for (var digit = 1; digit <= 9; digit++)
        {
            var noteBit = (ushort)(1 << (digit - 1));
            if ((noteMask & noteBit) == 0)
            {
                continue;
            }

            var miniRow = (digit - 1) / 3;
            var miniColumn = (digit - 1) % 3;
            var notePosition = new Vector2(
                cellMin.X + (miniColumn * miniCellSize) + ((miniCellSize - noteTextSize.X) * 0.5f),
                cellMin.Y + (miniRow * miniCellSize) + ((miniCellSize - noteTextSize.Y) * 0.5f));
            DrawHighContrastNoteText(drawList, notePosition, DigitLabels[digit - 1], colors.NoteText);
        }
    }

    private void DrawGrid(ImDrawListPtr drawList, Vector2 origin, Vector2 boardSize, float cellSize, BoardColors colors)
    {
        for (var index = 0; index <= 9; index++)
        {
            var thickness = index % 3 == 0 ? 3.0f : 1.0f;
            var x = origin.X + (index * cellSize);
            var y = origin.Y + (index * cellSize);

            drawList.AddLine(
                new Vector2(x, origin.Y),
                new Vector2(x, origin.Y + boardSize.Y),
                ImGui.ColorConvertFloat4ToU32(colors.Grid),
                thickness);
            drawList.AddLine(
                new Vector2(origin.X, y),
                new Vector2(origin.X + boardSize.X, y),
                ImGui.ColorConvertFloat4ToU32(colors.Grid),
                thickness);
        }
    }

    private void DrawDigitPalette()
    {
        var canEditSelected = selectedCell.HasValue
            && game.CanEdit(selectedCell.Value)
            && game.State != SudokuGameState.Completed;

        DrawCenteredButtonRow(DigitLabels.Length, DigitButtonSize, DrawDigitButton);

        ImGui.Spacing();

        const int actionButtonCount = 4;
        var actionButtonWidth = GetFittedButtonWidth(actionButtonCount, 96.0f);
        DrawCenteredButtonRow(actionButtonCount, actionButtonWidth, index => DrawActionButton(index, actionButtonWidth));

        if (showCheckedErrors)
        {
            var errorCount = CountCheckedErrors();
            if (errorCount == 0)
            {
                ImGui.TextColored(new Vector4(0.26f, 0.82f, 0.40f, 1.0f), "Check: no errors found in filled cells.");
            }
            else
            {
                var suffix = errorCount == 1 ? string.Empty : "s";
                ImGui.TextColored(new Vector4(0.95f, 0.34f, 0.34f, 1.0f), $"Check: {errorCount} error{suffix} highlighted.");
            }
        }

        if (selectedCell.HasValue)
        {
            var selection = selectedCell.Value;
            ImGui.TextDisabled(
                canEditSelected
                    ? $"Selected cell: R{selection.Row + 1} C{selection.Column + 1}"
                    : $"Selected cell: R{selection.Row + 1} C{selection.Column + 1} (given)");
        }
        else
        {
            ImGui.TextDisabled("Select a cell to enter values or notes.");
        }
    }

    private void DrawDigitButton(int index)
    {
        var digit = index + 1;
        var canEditSelected = selectedCell.HasValue
            && game.CanEdit(selectedCell.Value)
            && game.State != SudokuGameState.Completed;

        ImGui.BeginDisabled(!canEditSelected);
        if (ImGui.Button(DigitLabels[index], new Vector2(DigitButtonSize, DigitButtonSize)) && selectedCell.HasValue)
        {
            if (noteMode)
            {
                game.ToggleNote(selectedCell.Value, digit);
            }
            else
            {
                game.SetCellValue(selectedCell.Value, digit);
            }
        }

        ImGui.EndDisabled();
    }

    private void DrawActionButton(int index, float buttonWidth)
    {
        var canEditSelected = selectedCell.HasValue
            && game.CanEdit(selectedCell.Value)
            && game.State != SudokuGameState.Completed;

        switch (index)
        {
            case 0:
                var pushedNoteModeStyle = false;
                if (noteMode)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.22f, 0.38f, 0.70f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.42f, 0.76f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.20f, 0.34f, 0.64f, 1.0f));
                    pushedNoteModeStyle = true;
                }

                if (ImGui.Button("Note Mode", new Vector2(buttonWidth, 0.0f)))
                {
                    noteMode = !noteMode;
                }

                if (pushedNoteModeStyle)
                {
                    ImGui.PopStyleColor(3);
                }

                break;

            case 1:
                ImGui.BeginDisabled(!canEditSelected);
                if (ImGui.Button("Clear", new Vector2(buttonWidth, 0.0f)) && selectedCell.HasValue)
                {
                    game.SetCellValue(selectedCell.Value, null);
                }

                ImGui.EndDisabled();
                break;

            case 2:
                ImGui.BeginDisabled(!canEditSelected);
                if (ImGui.Button("Clear Notes", new Vector2(buttonWidth, 0.0f)) && selectedCell.HasValue)
                {
                    game.ClearNotes(selectedCell.Value);
                }

                ImGui.EndDisabled();
                break;

            case 3:
                ImGui.BeginDisabled(game.State == SudokuGameState.Completed);
                if (ImGui.Button("Check", new Vector2(buttonWidth, 0.0f)))
                {
                    showCheckedErrors = true;
                }

                ImGui.EndDisabled();
                break;
        }
    }

    private void ApplyDifficulty(SudokuDifficulty difficulty)
    {
        game.SetDifficulty(difficulty, startNewPuzzle: true);
        selectedCell = null;
        showCheckedErrors = false;

        if (configuration.DefaultSudokuDifficulty != difficulty)
        {
            configuration.DefaultSudokuDifficulty = difficulty;
            configuration.Save();
        }
    }

    private void OnPuzzleEnded(SudokuPuzzleSummary summary)
    {
        accountStatsService.RecordSudokuResult(summary);
        accountStatsService.Save();
    }

    private void DrawCenteredButtonRow(
        int itemCount,
        float itemWidth,
        Action<int> drawItem)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var rowWidth = (itemCount * itemWidth) + ((itemCount - 1) * spacing);
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var startX = ImGui.GetCursorPosX() + MathF.Max(0.0f, (availableWidth - rowWidth) * 0.5f);
        ImGui.SetCursorPosX(startX);

        for (var index = 0; index < itemCount; index++)
        {
            drawItem(index);
            if (index < itemCount - 1)
            {
                ImGui.SameLine();
            }
        }
    }

    private float GetFittedButtonWidth(int itemCount, float preferredWidth)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var maxFittableWidth = MathF.Max(1.0f, (availableWidth - ((itemCount - 1) * spacing)) / itemCount);
        return MathF.Min(preferredWidth, maxFittableWidth);
    }

    private int CountCheckedErrors()
    {
        if (!showCheckedErrors)
        {
            return 0;
        }

        var count = 0;
        for (var row = 0; row < SudokuBoard.Size; row++)
        {
            for (var column = 0; column < SudokuBoard.Size; column++)
            {
                if (IsCheckedError(new SudokuCoordinate(row, column)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private bool IsCheckedError(SudokuCoordinate coordinate)
    {
        if (!showCheckedErrors)
        {
            return false;
        }

        var playerValue = game.Board.GetPlayerValue(coordinate);
        if (playerValue == 0)
        {
            return false;
        }

        if (game.Analysis.IsConflicting(coordinate))
        {
            return true;
        }

        var solutionIndex = SudokuBoard.GetIndex(coordinate);
        var expected = game.CurrentPuzzle.Solution[solutionIndex] - '0';
        return playerValue != expected;
    }

    private static bool IsPeer(SudokuCoordinate selected, SudokuCoordinate candidate)
    {
        if (selected == candidate)
        {
            return true;
        }

        return selected.Row == candidate.Row
            || selected.Column == candidate.Column
            || ((selected.Row / 3) == (candidate.Row / 3) && (selected.Column / 3) == (candidate.Column / 3));
    }

    private static void DrawInlineStat(string label, string value)
    {
        ImGui.TextDisabled($"{label}:");
        ImGui.SameLine();
        ImGui.TextUnformatted(value);
    }

    private static void DrawInlineSeparator()
    {
        ImGui.TextDisabled("|");
    }

    private static string FormatDifficulty(SudokuDifficulty difficulty)
    {
        return difficulty switch
        {
            SudokuDifficulty.Easy => "Easy",
            SudokuDifficulty.Medium => "Medium",
            SudokuDifficulty.Hard => "Hard",
            _ => "Any",
        };
    }

    private static string FormatState(SudokuGameState state)
    {
        return state switch
        {
            SudokuGameState.InProgress => "In Progress",
            SudokuGameState.Completed => "Completed",
            _ => "Ready",
        };
    }

    private static BoardColors GetBoardColors()
    {
        return new BoardColors(
            new Vector4(0.15f, 0.15f, 0.18f, 1.0f),
            new Vector4(0.08f, 0.08f, 0.11f, 1.0f),
            new Vector4(0.16f, 0.20f, 0.28f, 1.0f),
            new Vector4(0.90f, 0.22f, 0.22f, 1.0f),
            new Vector4(0.16f, 0.44f, 0.26f, 1.0f),
            new Vector4(0.94f, 0.94f, 0.97f, 1.0f),
            new Vector4(0.96f, 0.98f, 1.00f, 1.0f),
            new Vector4(0.97f, 0.98f, 1.00f, 1.0f),
            new Vector4(1.00f, 1.00f, 1.00f, 1.00f),
            new Vector4(0.10f, 0.14f, 0.20f, 0.95f),
            new Vector4(0.15f, 0.30f, 0.50f, 0.95f));
    }

    private static void DrawOutlinedText(ImDrawListPtr drawList, Vector2 position, string text, Vector4 textColor, float shadowAlpha = 0.9f)
    {
        var shadowColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.0f, 0.0f, shadowAlpha));
        drawList.AddText(position + new Vector2(-1.0f, 0.0f), shadowColor, text);
        drawList.AddText(position + new Vector2(1.0f, 0.0f), shadowColor, text);
        drawList.AddText(position + new Vector2(0.0f, -1.0f), shadowColor, text);
        drawList.AddText(position + new Vector2(0.0f, 1.0f), shadowColor, text);
        drawList.AddText(position + new Vector2(-1.0f, -1.0f), shadowColor, text);
        drawList.AddText(position + new Vector2(-1.0f, 1.0f), shadowColor, text);
        drawList.AddText(position + new Vector2(1.0f, -1.0f), shadowColor, text);
        drawList.AddText(position + new Vector2(1.0f, 1.0f), shadowColor, text);
        drawList.AddText(position, ImGui.ColorConvertFloat4ToU32(textColor), text);
    }

    private static void DrawHighContrastNoteText(ImDrawListPtr drawList, Vector2 position, string text, Vector4 textColor)
    {
        // Notes are tiny; avoid dark outlines and overdraw a light pass for legibility on dark cells.
        var color = ImGui.ColorConvertFloat4ToU32(textColor);
        drawList.AddText(position, color, text);
        drawList.AddText(position + new Vector2(0.6f, 0.0f), color, text);
    }

    private static Vector4 Lerp(Vector4 start, Vector4 end, float amount)
    {
        return start + ((end - start) * Math.Clamp(amount, 0.0f, 1.0f));
    }

    private readonly record struct BoardColors(
        Vector4 GivenBackground,
        Vector4 EditableBackground,
        Vector4 SelectedBackground,
        Vector4 ConflictTint,
        Vector4 SolvedTint,
        Vector4 Grid,
        Vector4 GivenText,
        Vector4 PlayerText,
        Vector4 NoteText,
        Vector4 GivenValuePlate,
        Vector4 PlayerValuePlate)
    {
        public Vector4 PeerTint => new(0.80f, 0.78f, 0.40f, 1.0f);
    }
}

