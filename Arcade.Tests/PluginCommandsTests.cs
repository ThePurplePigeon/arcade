using Xunit;

namespace Arcade.Tests;

public class PluginCommandsTests
{
    [Fact]
    public void PrimaryCommand_IsArcade()
    {
        Assert.Equal("/arcade", PluginCommands.Primary);
    }

    [Fact]
    public void LegacyAlias_RemainsSampleCommand()
    {
        Assert.Equal("/pmycommand", PluginCommands.LegacyAlias);
        Assert.NotEqual(PluginCommands.Primary, PluginCommands.LegacyAlias);
    }
}
