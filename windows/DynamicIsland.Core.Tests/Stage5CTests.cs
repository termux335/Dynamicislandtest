using System.Globalization;
using DynamicIsland.Core;

namespace DynamicIsland.Core.Tests;

public class Stage5CTests
{
    [Fact]
    public void Stage5CDefaultsKeepInternetAndSensitiveModulesOff()
    {
        var settings = new Stage5CSettings();

        Assert.False(settings.AgentStatusEnabled);
        Assert.False(settings.TerminalEnabled);
        Assert.True(settings.ScreenshotEnabled);
        Assert.False(settings.TickerEnabled);
        Assert.False(settings.AssistantEnabled);
        Assert.False(settings.PluginsEnabled);
    }

    [Fact]
    public void AgentStatusReadsUsageAndPermissionRequest()
    {
        var json = """
        {
          "tool": "Claude Code",
          "state": "waiting",
          "used": 120000,
          "limit": 200000,
          "request": { "id": "req-1", "summary": "Izinkan ubah Program.cs", "detail": "Edit berkas" }
        }
        """;

        var snapshot = AgentStatusParser.Parse(json);

        Assert.NotNull(snapshot);
        Assert.Equal("Claude Code", snapshot!.Tool);
        Assert.Equal("waiting", snapshot.State);
        Assert.Equal(120000, snapshot.Used);
        Assert.Equal(200000, snapshot.Limit);
        Assert.Equal("req-1", snapshot.Request!.Id);
        Assert.Equal("Izinkan ubah Program.cs", snapshot.Request.Summary);
    }

    [Fact]
    public void AgentStatusRejectsMissingOrMalformedPayload()
    {
        Assert.Null(AgentStatusParser.Parse(null));
        Assert.Null(AgentStatusParser.Parse("bukan json"));
        Assert.Null(AgentStatusParser.Parse("[]"));
    }

    [Fact]
    public void AgentResponseGoesNextToTheStatusFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), "island-agent-" + Guid.NewGuid().ToString("N"));
        var status = Path.Combine(folder, "agent-status.json");

        try
        {
            Assert.True(AgentStatusFile.Respond(status, "req-9", approved: false));

            var response = Path.Combine(folder, AgentStatusFile.ResponseName);
            Assert.True(File.Exists(response));

            var json = File.ReadAllText(response);
            Assert.Contains("req-9", json);
            Assert.Contains("false", json);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TerminalCatalogOnlyKeepsNamedAndRegisteredCommands()
    {
        var json = """
        [
          { "Name": "Build", "FileName": "dotnet", "Arguments": "build", "Confirm": true },
          { "Name": "", "FileName": "cmd.exe" },
          { "Name": "Kosong", "FileName": "" }
        ]
        """;

        var commands = TerminalCatalog.Parse(json);

        Assert.Single(commands);
        Assert.Equal("Build", commands[0].Name);
        Assert.Equal("dotnet", commands[0].FileName);
        Assert.True(commands[0].Confirm);
        Assert.Null(TerminalCatalog.Find(commands, "tidak ada"));
        Assert.Equal("Build", TerminalCatalog.Find(commands, "build")!.Name);
    }

    [Fact]
    public void TickerParsesRssAndAtomAndRejectsJunk()
    {
        var rss = """
        <rss><channel>
          <item><title>Harga beras turun</title><link>https://example.com/a</link></item>
          <item><title> </title></item>
        </channel></rss>
        """;

        var atom = """
        <feed xmlns="http://www.w3.org/2005/Atom">
          <entry><title>Atom satu</title><link href="https://example.com/b" /></entry>
        </feed>
        """;

        Assert.Equal("Harga beras turun", Assert.Single(TickerFeed.Parse(rss)).Title);
        Assert.Equal("https://example.com/b", Assert.Single(TickerFeed.Parse(atom)).Link);
        Assert.Empty(TickerFeed.Parse("<bukan>"));
        Assert.Empty(TickerFeed.Parse(""));
    }

    [Fact]
    public void TickerRequiresHttpAndRefreshesOnSchedule()
    {
        Assert.True(TickerFeed.ValidUrl("https://example.com/rss"));
        Assert.False(TickerFeed.ValidUrl("ftp://example.com/rss"));
        Assert.False(TickerFeed.ValidUrl("bukan url"));

        var now = DateTimeOffset.Parse("2026-11-03T12:00:00+07:00", CultureInfo.InvariantCulture);
        Assert.True(TickerFeed.ShouldRefresh(null, now, enabled: true));
        Assert.False(TickerFeed.ShouldRefresh(now.AddMinutes(-5), now, enabled: true));
        Assert.True(TickerFeed.ShouldRefresh(now.AddMinutes(-20), now, enabled: true));
        Assert.False(TickerFeed.ShouldRefresh(null, now, enabled: false));
    }

    [Fact]
    public void AssistantOnlyAllowsHttpsOrLoopback()
    {
        Assert.True(AssistantRules.ValidEndpoint("https://api.example.com/v1/chat/completions"));
        Assert.True(AssistantRules.ValidEndpoint("http://localhost:11434/v1/chat/completions"));
        Assert.False(AssistantRules.ValidEndpoint("http://api.example.com/v1/chat"));
        Assert.False(AssistantRules.ValidEndpoint("bukan url"));
    }

    [Fact]
    public void AssistantRequestCarriesTheQuestionWithoutASecret()
    {
        var json = AssistantRules.BuildRequest("Apa itu C#?", "model-lokal");

        Assert.Contains("Apa itu C#?", json);
        Assert.Contains("model-lokal", json);
        Assert.DoesNotContain("api_key", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sk-", json);
    }

    [Fact]
    public void PluginManifestIsRejectedForUnknownPermissions()
    {
        var allowed = """
        { "id": "cuaca", "name": "Cuaca", "version": "1.0.0", "entry": "Cuaca.dll",
          "permissions": ["activity.show"] }
        """;

        var denied = """
        { "id": "jahat", "name": "Jahat", "entry": "Jahat.dll",
          "permissions": ["filesystem.read"] }
        """;

        var manifest = PluginCatalog.Parse(allowed);

        Assert.NotNull(manifest);
        Assert.Equal("cuaca", manifest!.Id);
        Assert.Contains("activity.show", manifest.Permissions);
        Assert.Null(PluginCatalog.Parse(denied));
        Assert.Null(PluginCatalog.Parse("{}"));
    }
}
