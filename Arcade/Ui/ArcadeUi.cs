using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Arcade.Ui;

internal static class ArcadeUi
{
    public static void DrawSectionLabel(string text)
    {
        ImGui.TextDisabled(text);
    }

    public static void DrawSecondaryText(string text)
    {
        ImGui.TextDisabled(text);
    }

    public static void DrawCompactStatusRow(params (string Label, string Value)[] items)
    {
        if (items is null || items.Length == 0)
        {
            return;
        }

        var style = ImGui.GetStyle();
        var availableWidth = MathF.Max(1.0f, ImGui.GetContentRegionAvail().X);
        var rowWidth = 0.0f;

        for (var index = 0; index < items.Length; index++)
        {
            var (label, value) = items[index];
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var chipText = $"{label}: {value}";
            var chipSize = ImGui.CalcTextSize(chipText) + new Vector2(12.0f, 8.0f);
            var drawWidth = chipSize.X + (rowWidth > 0.0f ? style.ItemSpacing.X : 0.0f);

            if (rowWidth > 0.0f && (rowWidth + drawWidth) > availableWidth)
            {
                ImGui.NewLine();
                rowWidth = 0.0f;
            }

            if (rowWidth > 0.0f)
            {
                ImGui.SameLine();
            }

            ImGui.BeginDisabled();
            ImGui.Button(chipText, chipSize);
            ImGui.EndDisabled();

            rowWidth += drawWidth;
        }
    }
}
