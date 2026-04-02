using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Sdk;

namespace Arcade.Tests;

public class MouseOnlyUiPolicyTests
{
    private static readonly string[] BannedInputCalls =
    [
        "ImGui.InputInt(",
        "ImGui.InputText(",
        "ImGui.InputTextMultiline(",
        "ImGui.InputFloat(",
        "ImGui.InputDouble(",
        "ImGui.InputScalar(",
    ];

    [Fact]
    public void GameplayModules_DoNotUseKeyboardEntryImGuiCalls()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var modulesPath = Path.Combine(repoRoot, "Arcade", "Modules");
        var moduleFiles = Directory.GetFiles(modulesPath, "*.cs", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(moduleFiles);

        var violations = new List<string>();

        foreach (var filePath in moduleFiles)
        {
            var lines = File.ReadAllLines(filePath);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                foreach (var bannedCall in BannedInputCalls)
                {
                    if (line.Contains(bannedCall, StringComparison.Ordinal))
                    {
                        var relativePath = Path.GetRelativePath(repoRoot, filePath).Replace('\\', '/');
                        violations.Add($"- {relativePath}:{lineIndex + 1} matched {bannedCall}");
                    }
                }
            }
        }

        if (violations.Count > 0)
        {
            throw new XunitException(
                "Mouse-only gameplay UI policy failed. Remove keyboard-entry ImGui calls from Arcade modules.\n"
                + string.Join('\n', violations));
        }
    }
}
