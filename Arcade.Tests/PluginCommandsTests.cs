using Xunit;

namespace Arcade.Tests;

public class PluginCommandsTests
{
    [Fact]
    public void PrimaryCommand_IsArcade()
    {
        Assert.Equal("/arcade", PluginCommands.Primary);
    }
}
