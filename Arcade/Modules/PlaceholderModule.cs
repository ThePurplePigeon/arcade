using System;
using Dalamud.Bindings.ImGui;

namespace Arcade.Modules;

public sealed class PlaceholderModule : IArcadeModule
{
    private readonly string description;

    public PlaceholderModule(string name, string description)
    {
        Name = name;
        this.description = description;
    }

    public string Name { get; }

    public void Dispose()
    {
    }

    public void Draw()
    {
        ImGui.Text(Name);
        ImGui.Separator();
        ImGui.TextWrapped(description);
    }
}
