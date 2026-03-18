using System;

namespace Arcade.Modules;

public interface IArcadeModule : IDisposable
{
    string Name { get; }
    void Draw();
}
