using System;
using System.Collections.Generic;

namespace Arcade.Games.Hangman;

internal readonly record struct HangmanGuessLayout(
    IReadOnlyList<string> Lines,
    float CellSize,
    float CellGap,
    float RowGap,
    float Padding,
    float SpaceGap,
    float UnderlineInset,
    float UnderlineBottomOffset,
    float ContentHeight);

internal static class HangmanGuessLayoutHelper
{
    internal const float DefaultMaxCellSize = 34.0f;
    internal const float DefaultMinCellSize = 22.0f;
    private const float CellSizeStep = 2.0f;
    private const int PreferredMaxLines = 4;

    public static HangmanGuessLayout Build(
        string display,
        float availableWidth,
        float maxCellSize = DefaultMaxCellSize,
        float minCellSize = DefaultMinCellSize)
    {
        display ??= string.Empty;

        if (string.IsNullOrWhiteSpace(display))
        {
            return BuildLayout(display, availableWidth, maxCellSize);
        }

        if (availableWidth <= 0.0f)
        {
            return BuildLayout(display, 1.0f, minCellSize);
        }

        HangmanGuessLayout? bestLayout = null;
        var bestPenalty = int.MaxValue;

        for (var candidateCellSize = maxCellSize; candidateCellSize >= minCellSize; candidateCellSize -= CellSizeStep)
        {
            var evaluation = BuildEvaluatedLayout(display, availableWidth, candidateCellSize);
            var penalty = (evaluation.TokenSplitCount * 100) + Math.Max(0, evaluation.Layout.Lines.Count - PreferredMaxLines);

            if (evaluation.TokenSplitCount == 0 && evaluation.Layout.Lines.Count <= PreferredMaxLines)
            {
                return evaluation.Layout;
            }

            if (bestLayout is null || penalty < bestPenalty)
            {
                bestLayout = evaluation.Layout;
                bestPenalty = penalty;
            }
        }

        return bestLayout ?? BuildLayout(display, availableWidth, minCellSize);
    }

    internal static float MeasureLineWidth(string line, float cellSize, float cellGap, float spaceGap)
    {
        var width = 0.0f;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == ' ')
            {
                width += spaceGap;
                continue;
            }

            width += cellSize;
            if (i < line.Length - 1 && line[i + 1] != ' ')
            {
                width += cellGap;
            }
        }

        return width;
    }

    private static EvaluatedLayout BuildEvaluatedLayout(string display, float availableWidth, float cellSize)
    {
        var layout = BuildLayout(display, availableWidth, cellSize, out var tokenSplitCount);
        return new EvaluatedLayout(layout, tokenSplitCount);
    }

    private static HangmanGuessLayout BuildLayout(
        string display,
        float availableWidth,
        float cellSize,
        out int tokenSplitCount)
    {
        var cellGap = MathF.Max(4.0f, MathF.Round(cellSize * 0.18f));
        var rowGap = MathF.Max(8.0f, MathF.Round(cellSize * 0.35f));
        var padding = MathF.Max(8.0f, MathF.Round(cellSize * 0.30f));
        var spaceGap = MathF.Max(cellSize * 0.60f, cellSize * 0.5f);
        var underlineInset = MathF.Max(5.0f, MathF.Round(cellSize * 0.20f));
        var underlineBottomOffset = MathF.Max(6.0f, MathF.Round(cellSize * 0.20f));
        var innerWidth = MathF.Max(1.0f, availableWidth - (padding * 2.0f));

        var lines = WrapDisplay(display, innerWidth, cellSize, cellGap, spaceGap, out tokenSplitCount);
        var lineCount = Math.Max(1, lines.Count);
        var contentHeight = (lineCount * cellSize) + ((lineCount - 1) * rowGap) + (padding * 2.0f);

        return new HangmanGuessLayout(
            lines,
            cellSize,
            cellGap,
            rowGap,
            padding,
            spaceGap,
            underlineInset,
            underlineBottomOffset,
            MathF.Max(84.0f, contentHeight));
    }

    private static HangmanGuessLayout BuildLayout(string display, float availableWidth, float cellSize)
    {
        return BuildLayout(display, availableWidth, cellSize, out _);
    }

    private static List<string> WrapDisplay(
        string display,
        float maxLineWidth,
        float cellSize,
        float cellGap,
        float spaceGap,
        out int tokenSplitCount)
    {
        tokenSplitCount = 0;
        if (string.IsNullOrWhiteSpace(display))
        {
            return [display];
        }

        var lines = new List<string>();
        var current = string.Empty;
        var currentWidth = 0.0f;
        var maxChunkLength = GetMaxTokenChunkLength(maxLineWidth, cellSize, cellGap);
        var words = display.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            var wordWidth = MeasureTokenWidth(word.Length, cellSize, cellGap);
            if (wordWidth <= maxLineWidth)
            {
                if (string.IsNullOrEmpty(current))
                {
                    current = word;
                    currentWidth = wordWidth;
                }
                else
                {
                    var candidateWidth = currentWidth + spaceGap + wordWidth;
                    if (candidateWidth <= maxLineWidth)
                    {
                        current = string.Concat(current, " ", word);
                        currentWidth = candidateWidth;
                    }
                    else
                    {
                        lines.Add(current);
                        current = word;
                        currentWidth = wordWidth;
                    }
                }

                continue;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
                current = string.Empty;
                currentWidth = 0.0f;
            }

            tokenSplitCount++;
            var start = 0;
            while (start < word.Length)
            {
                var chunkLength = Math.Min(maxChunkLength, word.Length - start);
                current = word.Substring(start, chunkLength);
                currentWidth = MeasureTokenWidth(chunkLength, cellSize, cellGap);
                lines.Add(current);
                current = string.Empty;
                currentWidth = 0.0f;
                start += chunkLength;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            lines.Add(current);
        }

        if (lines.Count == 0)
        {
            lines.Add(display);
        }

        return lines;
    }

    private static int GetMaxTokenChunkLength(float maxLineWidth, float cellSize, float cellGap)
    {
        var denominator = cellSize + cellGap;
        if (denominator <= 0.0f)
        {
            return 1;
        }

        var maxChars = (int)MathF.Floor((maxLineWidth + cellGap) / denominator);
        return Math.Max(1, maxChars);
    }

    private static float MeasureTokenWidth(int tokenLength, float cellSize, float cellGap)
    {
        if (tokenLength <= 0)
        {
            return 0.0f;
        }

        return (tokenLength * cellSize) + ((tokenLength - 1) * cellGap);
    }

    private readonly record struct EvaluatedLayout(HangmanGuessLayout Layout, int TokenSplitCount);
}
