using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Arcade.Modules;
using Arcade.Stats;

namespace Arcade.Windows;

public class MainWindow : Window, IDisposable
{
    private const float HeaderActionButtonWidth = 118.0f;
    private const float SidebarExpandedWidth = 190.0f;
    private const float SidebarCollapsedWidth = 56.0f;

    private readonly Plugin plugin;
    private readonly IArcadeModule[] modules;
    private int activeModuleIndex;
    private bool isSidebarCollapsed;
    private string? lastModuleDrawErrorKey;

    public MainWindow(Plugin plugin, IAccountStatsService accountStatsService)
        : base("Arcade##MainWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
        modules =
        [
            new MinesweeperModule(accountStatsService),
            new HangmanModule(plugin.Configuration, accountStatsService),
            new SudokuModule(plugin.Configuration, accountStatsService),
            new PlaceholderModule("More Games Soon", "Additional game modules will be added here."),
        ];
        activeModuleIndex = 0;
    }

    public void Dispose()
    {
        foreach (var module in modules)
        {
            module.Dispose();
        }
    }

    public override void Draw()
    {
        DrawShellHeader();

        using var layout = ImRaii.Child("ArcadeLayout", Vector2.Zero, true);
        if (!layout.Success)
        {
            return;
        }

        var sidebarWidth = (isSidebarCollapsed ? SidebarCollapsedWidth : SidebarExpandedWidth) * ImGuiHelpers.GlobalScale;
        using (var sidebar = ImRaii.Child("ArcadeSidebar", new Vector2(sidebarWidth, 0), true))
        {
            if (!sidebar.Success)
            {
                return;
            }

            for (var i = 0; i < modules.Length; i++)
            {
                var isSelected = i == activeModuleIndex;
                var label = isSidebarCollapsed ? (i + 1).ToString() : modules[i].Name;
                if (isSelected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.24f, 0.38f, 0.62f, 0.90f));
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.28f, 0.44f, 0.70f, 0.95f));
                    ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.22f, 0.36f, 0.56f, 1.0f));
                }

                var selectableWidth = MathF.Max(1.0f, ImGui.GetContentRegionAvail().X);
                if (ImGui.Selectable(label, isSelected, ImGuiSelectableFlags.None, new Vector2(selectableWidth, 0.0f)))
                {
                    activeModuleIndex = i;
                }

                if (isSelected)
                {
                    ImGui.PopStyleColor(3);
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(modules[i].Name);
                    ImGui.EndTooltip();
                }
            }

            var toggleLabel = isSidebarCollapsed ? ">>" : "<<";
            var toggleTooltip = isSidebarCollapsed ? "Expand module list" : "Collapse module list";
            var remainingHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeight();
            if (remainingHeight > 0.0f)
            {
                ImGui.Dummy(new Vector2(0.0f, remainingHeight));
            }

            if (ImGui.Button(toggleLabel, new Vector2(-1.0f, 0.0f)))
            {
                isSidebarCollapsed = !isSidebarCollapsed;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(toggleTooltip);
            }
        }

        ImGui.SameLine();

        using (var content = ImRaii.Child("ArcadeContent", Vector2.Zero, false))
        {
            if (content.Success)
            {
                DrawActiveModuleSafely();
            }
        }
    }

    private void DrawShellHeader()
    {
        var style = ImGui.GetStyle();
        var buttonWidth = HeaderActionButtonWidth * ImGuiHelpers.GlobalScale;
        var actionsWidth = (buttonWidth * 2.0f) + style.ItemSpacing.X;

        ImGui.AlignTextToFramePadding();
        ImGui.Text("Arcade");

        var rightStartX = MathF.Max(ImGui.GetCursorPosX() + style.ItemSpacing.X, ImGui.GetWindowContentRegionMax().X - actionsWidth);
        ImGui.SameLine(rightStartX);
        if (ImGui.Button("Settings", new Vector2(buttonWidth, 0.0f)))
        {
            plugin.ToggleConfigUi();
        }

        ImGui.SameLine();
        if (ImGui.Button("Account Stats", new Vector2(buttonWidth, 0.0f)))
        {
            plugin.ToggleAccountStatsUi();
        }

        ImGui.Separator();
    }

    private void DrawActiveModuleSafely()
    {
        var module = modules[activeModuleIndex];
        try
        {
            module.Draw();
            lastModuleDrawErrorKey = null;
        }
        catch (Exception ex)
        {
            var errorKey = $"{activeModuleIndex}:{ex.GetType().FullName}:{ex.Message}";
            if (!string.Equals(lastModuleDrawErrorKey, errorKey, StringComparison.Ordinal))
            {
                Plugin.Log.Error(ex, $"Unhandled exception while drawing Arcade module '{module.Name}'.");
                lastModuleDrawErrorKey = errorKey;
            }

            ImGui.TextColored(new Vector4(0.95f, 0.30f, 0.30f, 1.0f), $"Module '{module.Name}' hit an error while drawing.");
            ImGui.TextDisabled("Check Dalamud plugin logs for details.");
        }
    }
}
