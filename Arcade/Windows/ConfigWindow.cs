using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Arcade.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin)
        : base("Arcade Settings###ArcadeConfigWindow")
    {
        configuration = plugin.Configuration;
        Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(420, 180);
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
        ImGui.TextDisabled("Window");

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Allow moving this settings window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Separator();
        ImGui.TextDisabled("Commands");
        ImGui.BulletText($"{PluginCommands.Primary} - Toggle Arcade main window");

        ImGui.Separator();
        ImGui.TextDisabled("Gameplay defaults are configured inside each game module.");
    }
}
