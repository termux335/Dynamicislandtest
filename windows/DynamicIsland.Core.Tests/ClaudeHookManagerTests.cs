using System.Text.Json.Nodes;
using DynamicIsland.Core;
using Xunit;

namespace DynamicIsland.Core.Tests;

public class ClaudeHookManagerTests : IDisposable
{
    private readonly string tempDir;
    private readonly string settingsPath;

    public ClaudeHookManagerTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "IslandClaudeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        settingsPath = Path.Combine(tempDir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
        catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void IsInstalled_WhenFileDoesNotExist_ReturnsFalse()
    {
        Assert.False(ClaudeHookManager.IsInstalled(settingsPath));
    }

    [Fact]
    public void IsInstalled_WhenNoHooksKey_ReturnsFalse()
    {
        File.WriteAllText(settingsPath, "{\"theme\":\"dark\"}");
        Assert.False(ClaudeHookManager.IsInstalled(settingsPath));
    }

    [Fact]
    public void Install_CreatesHooks_AndCreatesBackup()
    {
        File.WriteAllText(settingsPath, "{\"theme\":\"dark\",\"model\":\"opus\"}");

        var hookCmd = Path.Combine(tempDir, "island-hook.cmd");
        var (success, _) = ClaudeHookManager.Install(hookCmd, settingsPath);

        Assert.True(success);
        Assert.True(ClaudeHookManager.IsInstalled(settingsPath));

        // Verifikasi backup ada
        var bakFiles = Directory.GetFiles(tempDir, "settings.json.*.bak");
        Assert.Single(bakFiles);

        // Verifikasi isi JSON asli tetap utuh
        var content = File.ReadAllText(settingsPath);
        var root = JsonNode.Parse(content);
        Assert.Equal("dark", root?["theme"]?.ToString());
        Assert.Equal("opus", root?["model"]?.ToString());
        Assert.NotNull(root?["hooks"]?["PermissionRequest"]);
        Assert.NotNull(root?["hooks"]?["PreToolUse"]);
        Assert.NotNull(root?["hooks"]?["UserPromptSubmit"]);
        Assert.NotNull(root?["hooks"]?["SessionEnd"]);
    }

    [Fact]
    public void Install_WhenRunTwice_DoesNotDuplicateHooks()
    {
        var hookCmd = Path.Combine(tempDir, "island-hook.cmd");
        ClaudeHookManager.Install(hookCmd, settingsPath);
        ClaudeHookManager.Install(hookCmd, settingsPath);

        var content = File.ReadAllText(settingsPath);
        var root = JsonNode.Parse(content);
        var matchers = root?["hooks"]?["PermissionRequest"] as JsonArray;
        Assert.NotNull(matchers);
        Assert.Single(matchers);
    }

    [Fact]
    public void Uninstall_RemovesIslandHook_PreservesOtherHooks()
    {
        // Settings awal memiliki hook custom lain
        var initialJson = @"
{
  ""hooks"": {
    ""PreToolUse"": [
      {
        ""matcher"": ""custom"",
        ""hooks"": [
          { ""type"": ""command"", ""command"": ""custom-checker.bat"" }
        ]
      }
    ]
  }
}";
        File.WriteAllText(settingsPath, initialJson);

        var hookCmd = Path.Combine(tempDir, "island-hook.cmd");
        ClaudeHookManager.Install(hookCmd, settingsPath);
        Assert.True(ClaudeHookManager.IsInstalled(settingsPath));

        var (success, _) = ClaudeHookManager.Uninstall(settingsPath);
        Assert.True(success);
        Assert.False(ClaudeHookManager.IsInstalled(settingsPath));

        // Hook custom lain harus tetap ada
        var content = File.ReadAllText(settingsPath);
        var root = JsonNode.Parse(content);
        var preToolMatchers = root?["hooks"]?["PreToolUse"] as JsonArray;
        Assert.NotNull(preToolMatchers);
        Assert.Single(preToolMatchers);
        var cmd = preToolMatchers[0]?["hooks"]?[0]?["command"]?.ToString();
        Assert.Equal("custom-checker.bat", cmd);
    }

    [Fact]
    public void DeployHookScripts_WritesValidFiles()
    {
        ClaudeHookManager.DeployHookScripts(tempDir);
        var ps1 = Path.Combine(tempDir, "island-hook.ps1");
        var cmd = Path.Combine(tempDir, "island-hook.cmd");

        Assert.True(File.Exists(ps1));
        Assert.True(File.Exists(cmd));

        var ps1Content = File.ReadAllText(ps1);
        Assert.Contains("hook_event_name", ps1Content);
        Assert.Contains("PermissionRequest", ps1Content);
        Assert.Contains("agent-status.json", ps1Content);
    }
}
