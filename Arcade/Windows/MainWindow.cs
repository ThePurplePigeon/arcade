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
        ImGui.Text("Arcade");
        ImGui.SameLine();
        if (ImGui.Button("Settings"))
        {
            plugin.ToggleConfigUi();
        }

        ImGui.SameLine();
        if (ImGui.Button("Account Stats"))
        {
            plugin.ToggleAccountStatsUi();
        }

        ImGui.Separator();

        using var layout = ImRaii.Child("ArcadeLayout", Vector2.Zero, true);
        if (!layout.Success)
        {
            return;
        }

        var sidebarWidth = (isSidebarCollapsed ? 44.0f : 190.0f) * ImGuiHelpers.GlobalScale;
        using (var sidebar = ImRaii.Child("ArcadeSidebar", new Vector2(sidebarWidth, 0), true))
        {
            if (!sidebar.Success)
            {
                return;
            }

            var toggleLabel = isSidebarCollapsed ? ">>" : "<<";
            if (ImGui.Button(toggleLabel))
            {
                isSidebarCollapsed = !isSidebarCollapsed;
            }

            if (!isSidebarCollapsed)
            {
                ImGui.SameLine();
                ImGui.Text("Modules");
            }

            ImGui.Separator();

            for (var i = 0; i < modules.Length; i++)
            {
                var isSelected = i == activeModuleIndex;
                var label = isSidebarCollapsed ? (i + 1).ToString() : modules[i].Name;
                if (ImGui.Selectable(label, isSelected))
                {
                    activeModuleIndex = i;
                }

                if (isSidebarCollapsed && ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(modules[i].Name);
                    ImGui.EndTooltip();
                }
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
