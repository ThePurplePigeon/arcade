using System;
using System.IO;
using System.Numerics;
using System.Text;
using Arcade.Games.Hangman;
using Arcade.Stats;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Arcade.Modules;

public sealed class HangmanModule : IArcadeModule
{
    private const float MaxLayoutWidth = 760.0f;
    private const float MaxGuessPanelWidth = 700.0f;
    private const float MaxGallowsPanelWidth = 520.0f;
    private const float KeyboardButtonSize = 28.0f;
    private static readonly string[] KeyboardRows = ["QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM"];
    private static readonly string[] LetterLabels = BuildLetterLabels();
    private static readonly string[] CharLabelCache = BuildCharLabelCache();
    private static readonly HangmanDifficulty[] DifficultyOptions =
    [
        HangmanDifficulty.Any,
        HangmanDifficulty.Easy,
        HangmanDifficulty.Medium,
        HangmanDifficulty.Hard,
    ];

    private readonly Configuration configuration;
    private readonly IAccountStatsService accountStatsService;
    private readonly HangmanGame game;

    private string feedback = "Guess a letter to start.";
    private DateTime? roundCompletionFlashStartedAtUtc;
    private string cachedGuessDisplay = string.Empty;
    private float cachedGuessWidth = -1.0f;
    private HangmanGuessLayout cachedGuessLayout;

    public HangmanModule(Configuration configuration, IAccountStatsService accountStatsService)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.accountStatsService = accountStatsService ?? throw new ArgumentNullException(nameof(accountStatsService));

        var assemblyDirectory = Plugin.PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty;
        var wordBankPath = Path.Combine(assemblyDirectory, "hangman_words.txt");
        var provider = new FileHangmanWordProvider(wordBankPath);
        var defaultDifficulty = Enum.IsDefined(configuration.DefaultHangmanDifficulty)
            ? configuration.DefaultHangmanDifficulty
            : HangmanDifficulty.Any;

        game = new HangmanGame(
            provider,
            new HangmanGameSettings(
                maxWrongGuesses: 6,
                revealNonLetterCharacters: true,
                defaultDifficulty: defaultDifficulty));

        game.RoundCompleted += OnRoundCompleted;
    }

    public string Name => "Hangman";

    public void Dispose()
    {
        game.RoundCompleted -= OnRoundCompleted;
    }

    public void Draw()
    {
        var available = ImGui.GetContentRegionAvail();
        var layoutWidth = MathF.Min(MaxLayoutWidth, available.X);
        if (available.X > layoutWidth)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((available.X - layoutWidth) * 0.5f));
        }

        using var layout = ImRaii.Child("HangmanLayout", new Vector2(layoutWidth, 0), false);
        if (!layout.Success)
        {
            return;
        }

        ImGui.Text("Hangman");
        DrawTopControls();
        ImGui.Spacing();
        DrawStatusRows();
        ImGui.Separator();
        DrawGuessField();
        ImGui.Spacing();
        DrawRoundRecapAndFeedback();
        ImGui.Spacing();
        DrawGuessedLetterSummary();
        ImGui.Separator();
        DrawGallowsPanel();
        ImGui.Spacing();
        DrawKeyboard();
    }

    private void DrawTopControls()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("Difficulty");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(150.0f);

        var selected = game.SelectedDifficulty;
        if (ImGui.BeginCombo("##HangmanDifficulty", FormatDifficulty(selected)))
        {
            foreach (var option in DifficultyOptions)
            {
                var isSelected = option == selected;
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
        var canRestartCurrentRound = game.State is HangmanGameState.Ready or HangmanGameState.InProgress;
        ImGui.BeginDisabled(!canRestartCurrentRound);
        if (ImGui.Button("New Round"))
        {
            game.StartNewRound();
            feedback = "New round started.";
        }

        ImGui.EndDisabled();
    }

    private void DrawStatusRows()
    {
        DrawInlineStat("Round", game.RoundNumber.ToString());
        ImGui.SameLine();
        DrawInlineSeparator();
        ImGui.SameLine();
        DrawInlineStat("State", FormatState(game.State));
        ImGui.SameLine();
        DrawInlineSeparator();
        ImGui.SameLine();
        DrawInlineStat("Remaining", $"{game.RemainingGuesses}/{game.Settings.MaxWrongGuesses}");

        var stats = game.SessionStats;
        DrawInlineStat("Session", $"{stats.Wins}W / {stats.Losses}L");
        ImGui.SameLine();
        DrawInlineSeparator();
        ImGui.SameLine();
        DrawInlineStat("Streak", stats.CurrentWinStreak.ToString());
        ImGui.SameLine();
        DrawInlineSeparator();
        ImGui.SameLine();
        DrawInlineStat("Best", stats.BestWinStreak.ToString());
    }

    private void DrawRoundRecapAndFeedback()
    {
        var flashStrength = GetRoundCompletionFlashStrength();
        var isCompleted = game.State is HangmanGameState.Won or HangmanGameState.Lost;

        if (isCompleted)
        {
            var isWon = game.State == HangmanGameState.Won;
            var baseBg = isWon ? new Vector4(0.16f, 0.30f, 0.20f, 0.30f) : new Vector4(0.34f, 0.14f, 0.14f, 0.30f);
            var bg = new Vector4(baseBg.X, baseBg.Y, baseBg.Z, baseBg.W + (flashStrength * 0.20f));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, bg);
            using (var recap = ImRaii.Child("HangmanRoundRecap", new Vector2(0, 108), true))
            {
                if (recap.Success)
                {
                    var titleColor = isWon
                        ? new Vector4(0.20f, 0.82f, 0.36f, 1.0f)
                        : new Vector4(0.95f, 0.34f, 0.34f, 1.0f);
                    titleColor = Lerp(titleColor, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), flashStrength * 0.35f);

                    ImGui.TextColored(titleColor, isWon ? $"Round {game.RoundNumber}: Cleared" : $"Round {game.RoundNumber}: Failed");
                    ImGui.Text($"Answer: {game.CurrentEntry}");
                    ImGui.Text($"Wrong guesses: {game.WrongGuessCount}/{game.Settings.MaxWrongGuesses}");
                    ImGui.SameLine();
                    ImGui.Text($"Difficulty: {FormatDifficulty(game.SelectedDifficulty)}");

                    if (ImGui.Button("Next Round"))
                    {
                        game.StartNewRound();
                        feedback = "Next round started.";
                    }
                }
            }

            ImGui.PopStyleColor();
        }
        else if (game.LastRoundSummary is { } lastSummary)
        {
            var lastResultText = lastSummary.Result == HangmanGameState.Won ? "Cleared" : "Failed";
            ImGui.TextDisabled($"Last Round ({lastSummary.RoundNumber}): {lastResultText} on {FormatDifficulty(lastSummary.Difficulty)}");
        }

        var feedbackColor = new Vector4(0.82f, 0.84f, 0.88f, 1.0f);
        feedbackColor = Lerp(feedbackColor, new Vector4(1.0f, 1.0f, 1.0f, 1.0f), flashStrength * 0.30f);
        ImGui.TextColored(feedbackColor, feedback);
    }

    private void DrawGuessedLetterSummary()
    {
        ImGui.TextDisabled("Guessed Letters");
        ImGui.Text($"Correct: {BuildLetterSummary(includeWrongLetters: false)}");
        ImGui.Text($"Wrong: {BuildLetterSummary(includeWrongLetters: true)}");
    }

    private void DrawGuessField()
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var panelWidth = MathF.Min(MaxGuessPanelWidth, availableWidth);
        if (availableWidth > panelWidth)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availableWidth - panelWidth) * 0.5f));
        }

        var display = game.State == HangmanGameState.Lost ? game.CurrentEntry : game.MaskedEntry;
        var style = ImGui.GetStyle();
        var estimatedContentWidth = MathF.Max(1.0f, panelWidth - (style.WindowPadding.X * 2.0f));
        var precomputedLayout = GetGuessLayout(display, estimatedContentWidth);
        var panelHeight = MathF.Max(96.0f, precomputedLayout.ContentHeight + (style.WindowPadding.Y * 2.0f));

        using var panel = ImRaii.Child("HangmanGuessField", new Vector2(panelWidth, panelHeight), true);
        if (!panel.Success)
        {
            return;
        }

        var contentOrigin = ImGui.GetCursorScreenPos();
        var contentSize = ImGui.GetContentRegionAvail();
        var guessLayout = GetGuessLayout(display, contentSize.X);
        if (contentSize.X < 40.0f || contentSize.Y < guessLayout.CellSize)
        {
            ImGui.TextUnformatted("Resize to view guessing field.");
            return;
        }

        var draw = ImGui.GetWindowDrawList();
        var drawOrigin = contentOrigin + new Vector2(guessLayout.Padding, guessLayout.Padding);
        var maxLineWidth = MathF.Max(1.0f, contentSize.X - (guessLayout.Padding * 2.0f));
        var y = drawOrigin.Y;

        var borderColor = ImGui.GetColorU32(new Vector4(0.32f, 0.34f, 0.38f, 1.0f));
        var hiddenFill = ImGui.GetColorU32(new Vector4(0.14f, 0.15f, 0.18f, 1.0f));
        var revealedFill = ImGui.GetColorU32(new Vector4(0.23f, 0.28f, 0.25f, 1.0f));
        var separatorFill = ImGui.GetColorU32(new Vector4(0.20f, 0.20f, 0.23f, 1.0f));
        var textColor = ImGui.GetColorU32(new Vector4(0.92f, 0.92f, 0.93f, 1.0f));
        var hiddenUnderscore = ImGui.GetColorU32(new Vector4(0.62f, 0.64f, 0.68f, 1.0f));
        var lineHeight = guessLayout.CellSize + guessLayout.RowGap;

        foreach (var line in guessLayout.Lines)
        {
            var lineWidth = HangmanGuessLayoutHelper.MeasureLineWidth(
                line,
                guessLayout.CellSize,
                guessLayout.CellGap,
                guessLayout.SpaceGap);
            var x = drawOrigin.X + MathF.Max(0.0f, (maxLineWidth - lineWidth) * 0.5f);

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == ' ')
                {
                    x += guessLayout.SpaceGap;
                    continue;
                }

                var min = new Vector2(x, y);
                var max = min + new Vector2(guessLayout.CellSize, guessLayout.CellSize);
                var isHidden = ch == '_';
                var isLetter = ch is >= 'A' and <= 'Z';
                var fill = isLetter || isHidden ? (isHidden ? hiddenFill : revealedFill) : separatorFill;

                draw.AddRectFilled(min, max, fill, 4.0f);
                draw.AddRect(min, max, borderColor, 4.0f);

                if (isHidden)
                {
                    var underlineY = max.Y - guessLayout.UnderlineBottomOffset;
                    draw.AddLine(
                        new Vector2(min.X + guessLayout.UnderlineInset, underlineY),
                        new Vector2(max.X - guessLayout.UnderlineInset, underlineY),
                        hiddenUnderscore,
                        2.0f);
                }
                else
                {
                    DrawCenteredText(draw, (min + max) * 0.5f, GetCharLabel(ch), textColor);
                }

                if (i < line.Length - 1 && line[i + 1] != ' ')
                {
                    x += guessLayout.CellSize + guessLayout.CellGap;
                }
                else
                {
                    x += guessLayout.CellSize;
                }
            }

            y += lineHeight;
        }

        ImGui.SetCursorScreenPos(contentOrigin + contentSize);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawGallowsPanel()
    {
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var panelWidth = MathF.Min(MaxGallowsPanelWidth, availableWidth);
        if (availableWidth > panelWidth)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ((availableWidth - panelWidth) * 0.5f));
        }

        using var panel = ImRaii.Child("HangmanGallows", new Vector2(panelWidth, 150), true);
        if (!panel.Success)
        {
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var size = ImGui.GetContentRegionAvail();
        if (size.X < 100 || size.Y < 100)
        {
            ImGui.Text("Resize to view gallows.");
            return;
        }

        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(new Vector4(0.90f, 0.90f, 0.90f, 1.0f));
        const float thickness = 2.0f;

        var left = origin.X + 15.0f;
        var right = origin.X + size.X - 15.0f;
        var top = origin.Y + 10.0f;
        var bottom = origin.Y + size.Y - 10.0f;

        var poleX = left + (right - left) * 0.25f;
        var beamEndX = left + (right - left) * 0.68f;
        var ropeX = left + (right - left) * 0.58f;
        var ropeTop = top + 10.0f;
        var headCenter = new Vector2(ropeX, top + 35.0f);
        const float headRadius = 12.0f;

        draw.AddLine(new Vector2(left, bottom), new Vector2(right, bottom), color, thickness);
        draw.AddLine(new Vector2(poleX, bottom), new Vector2(poleX, top), color, thickness);
        draw.AddLine(new Vector2(poleX, top), new Vector2(beamEndX, top), color, thickness);
        draw.AddLine(new Vector2(ropeX, top), new Vector2(ropeX, ropeTop), color, thickness);

        var parts = GetBodyPartsToDraw(game.WrongGuessCount, game.Settings.MaxWrongGuesses);

        if (parts >= 1)
        {
            draw.AddCircle(headCenter, headRadius, color, 24, thickness);
        }

        var torsoStart = new Vector2(ropeX, headCenter.Y + headRadius);
        var torsoEnd = new Vector2(ropeX, torsoStart.Y + 34.0f);
        if (parts >= 2)
        {
            draw.AddLine(torsoStart, torsoEnd, color, thickness);
        }

        if (parts >= 3)
        {
            draw.AddLine(new Vector2(ropeX, torsoStart.Y + 8.0f), new Vector2(ropeX - 18.0f, torsoStart.Y + 20.0f), color, thickness);
        }

        if (parts >= 4)
        {
            draw.AddLine(new Vector2(ropeX, torsoStart.Y + 8.0f), new Vector2(ropeX + 18.0f, torsoStart.Y + 20.0f), color, thickness);
        }

        if (parts >= 5)
        {
            draw.AddLine(torsoEnd, new Vector2(ropeX - 16.0f, torsoEnd.Y + 22.0f), color, thickness);
        }

        if (parts >= 6)
        {
            draw.AddLine(torsoEnd, new Vector2(ropeX + 16.0f, torsoEnd.Y + 22.0f), color, thickness);
        }

        ImGui.SetCursorScreenPos(origin + size);
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawKeyboard()
    {
        var canInteract = game.State is HangmanGameState.Ready or HangmanGameState.InProgress;
        var rowStartX = ImGui.GetCursorPosX();
        var rowAvailableWidth = ImGui.GetContentRegionAvail().X;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        foreach (var row in KeyboardRows)
        {
            var rowWidth = (row.Length * KeyboardButtonSize) + ((row.Length - 1) * spacing);
            var centerOffset = MathF.Max(0.0f, (rowAvailableWidth - rowWidth) * 0.5f);
            ImGui.SetCursorPosX(rowStartX + centerOffset);

            for (var i = 0; i < row.Length; i++)
            {
                var letter = row[i];
                var guessed = game.IsLetterGuessed(letter);
                var wrong = game.IsLetterWrong(letter);

                var pushedStyle = false;
                if (guessed && wrong)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.65f, 0.24f, 0.24f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.65f, 0.24f, 0.24f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.65f, 0.24f, 0.24f, 1.0f));
                    pushedStyle = true;
                }
                else if (guessed)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.25f, 0.50f, 0.25f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.50f, 0.25f, 1.0f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.25f, 0.50f, 0.25f, 1.0f));
                    pushedStyle = true;
                }

                var letterAvailable = canInteract && game.IsLetterAvailable(letter);
                ImGui.BeginDisabled(!letterAvailable);
                if (ImGui.Button(GetLetterLabel(letter), new Vector2(KeyboardButtonSize, KeyboardButtonSize)))
                {
                    var result = game.Guess(letter);
                    feedback = BuildFeedback(result, letter);
                }

                ImGui.EndDisabled();

                if (pushedStyle)
                {
                    ImGui.PopStyleColor(3);
                }

                if (i < row.Length - 1)
                {
                    ImGui.SameLine();
                }
            }
        }
    }

    private void ApplyDifficulty(HangmanDifficulty difficulty)
    {
        game.SetDifficulty(difficulty, startNewRound: true);
        feedback = $"Difficulty set to {FormatDifficulty(difficulty)}.";
        roundCompletionFlashStartedAtUtc = null;

        if (configuration.DefaultHangmanDifficulty != difficulty)
        {
            configuration.DefaultHangmanDifficulty = difficulty;
            configuration.Save();
        }
    }

    private void OnRoundCompleted(HangmanRoundSummary summary)
    {
        accountStatsService.RecordHangmanRound(summary);
        accountStatsService.Save();
        roundCompletionFlashStartedAtUtc = DateTime.UtcNow;
    }

    private float GetRoundCompletionFlashStrength()
    {
        if (!roundCompletionFlashStartedAtUtc.HasValue)
        {
            return 0.0f;
        }

        var elapsed = DateTime.UtcNow - roundCompletionFlashStartedAtUtc.Value;
        const float durationSeconds = 1.2f;
        if (elapsed.TotalSeconds >= durationSeconds)
        {
            return 0.0f;
        }

        return 1.0f - (float)(elapsed.TotalSeconds / durationSeconds);
    }

    private HangmanGuessLayout GetGuessLayout(string display, float availableWidth)
    {
        if (string.Equals(cachedGuessDisplay, display, StringComparison.Ordinal)
            && MathF.Abs(cachedGuessWidth - availableWidth) < 0.5f)
        {
            return cachedGuessLayout;
        }

        cachedGuessDisplay = display;
        cachedGuessWidth = availableWidth;
        cachedGuessLayout = HangmanGuessLayoutHelper.Build(display, availableWidth);
        return cachedGuessLayout;
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

    private static string FormatDifficulty(HangmanDifficulty difficulty)
    {
        return difficulty switch
        {
            HangmanDifficulty.Easy => "Easy",
            HangmanDifficulty.Medium => "Medium",
            HangmanDifficulty.Hard => "Hard",
            _ => "Any",
        };
    }

    private static string FormatState(HangmanGameState state)
    {
        return state switch
        {
            HangmanGameState.InProgress => "In Progress",
            HangmanGameState.Won => "Won",
            HangmanGameState.Lost => "Lost",
            _ => "Ready",
        };
    }

    private string BuildLetterSummary(bool includeWrongLetters)
    {
        var builder = new StringBuilder(52);
        var hasLetters = false;
        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            var isWrong = game.IsLetterWrong(letter);
            var shouldInclude = includeWrongLetters
                ? isWrong
                : game.IsLetterGuessed(letter) && !isWrong;

            if (!shouldInclude)
            {
                continue;
            }

            if (hasLetters)
            {
                builder.Append(", ");
            }

            builder.Append(letter);
            hasLetters = true;
        }

        return hasLetters ? builder.ToString() : "-";
    }

    private static void DrawCenteredText(ImDrawListPtr drawList, Vector2 center, string text, uint color)
    {
        var size = ImGui.CalcTextSize(text);
        var pos = center - (size * 0.5f);
        drawList.AddText(pos, color, text);
    }

    private static string GetLetterLabel(char letter)
    {
        var index = letter - 'A';
        return index is >= 0 and < 26 ? LetterLabels[index] : letter.ToString();
    }

    private static string GetCharLabel(char character)
    {
        return character is >= '\0' and < '\x80'
            ? CharLabelCache[character]
            : character.ToString();
    }

    private static string[] BuildLetterLabels()
    {
        var labels = new string[26];
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = ((char)('A' + i)).ToString();
        }

        return labels;
    }

    private static string[] BuildCharLabelCache()
    {
        var cache = new string[128];
        for (var i = 0; i < cache.Length; i++)
        {
            cache[i] = ((char)i).ToString();
        }

        return cache;
    }

    private static string BuildFeedback(HangmanGuessResult result, char letter)
    {
        return result switch
        {
            HangmanGuessResult.Invalid => "Invalid guess.",
            HangmanGuessResult.AlreadyGuessed => $"You already guessed '{letter}'.",
            HangmanGuessResult.Correct => $"'{letter}' is in the word.",
            HangmanGuessResult.Incorrect => $"'{letter}' is not in the word.",
            HangmanGuessResult.Won => "Correct. You won this round.",
            HangmanGuessResult.Lost => "Incorrect. You lost this round.",
            _ => string.Empty,
        };
    }

    private static int GetBodyPartsToDraw(int wrongGuesses, int maxWrongGuesses)
    {
        if (wrongGuesses <= 0)
        {
            return 0;
        }

        var ratio = wrongGuesses / (float)Math.Max(1, maxWrongGuesses);
        return Math.Clamp((int)MathF.Ceiling(ratio * 6.0f), 0, 6);
    }

    private static Vector4 Lerp(Vector4 start, Vector4 end, float amount)
    {
        return start + ((end - start) * Math.Clamp(amount, 0.0f, 1.0f));
    }
}
