using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DynamicIsland.Core;

/// <summary>
/// Pengelola integrasi hook Claude Code (seperti Coucou).
/// Memasang dan melepas hook di ~/.claude/settings.json secara aman
/// dengan membuat cadangan (.bak) terlebih dahulu.
/// </summary>
public static class ClaudeHookManager
{
    public const string HookIdentifier = "island-hook";

    public static string DefaultSettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            "settings.json");

    public static string DefaultHooksDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DynamicIsland");

    public static string DefaultHookCmdPath =>
        Path.Combine(DefaultHooksDirectory, "island-hook.cmd");

    public static string DefaultHookPs1Path =>
        Path.Combine(DefaultHooksDirectory, "island-hook.ps1");

    public static bool IsInstalled(string? settingsPath = null)
    {
        var path = settingsPath ?? DefaultSettingsPath;
        if (!File.Exists(path)) return false;

        try
        {
            var content = File.ReadAllText(path);
            var root = JsonNode.Parse(content);
            if (root?["hooks"] is not JsonObject hooksObj) return false;

            foreach (var kvp in hooksObj)
            {
                if (kvp.Value is JsonArray matchers)
                {
                    foreach (var matcher in matchers)
                    {
                        if (matcher?["hooks"] is JsonArray innerHooks)
                        {
                            foreach (var inner in innerHooks)
                            {
                                var cmd = inner?["command"]?.ToString() ?? "";
                                if (cmd.Contains(HookIdentifier, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public static (bool Success, string Message) Install(string? hookCommandPath = null, string? settingsPath = null)
    {
        var path = settingsPath ?? DefaultSettingsPath;
        var cmdPath = hookCommandPath ?? DefaultHookCmdPath;

        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Buat cadangan bila berkas sudah ada
            if (File.Exists(path))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var backupPath = $"{path}.{timestamp}.bak";
                File.Copy(path, backupPath, overwrite: true);
            }

            JsonObject root;
            if (File.Exists(path))
            {
                var content = File.ReadAllText(path);
                root = (JsonNode.Parse(content) as JsonObject) ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            if (root["hooks"] is not JsonObject hooksObj)
            {
                hooksObj = new JsonObject();
                root["hooks"] = hooksObj;
            }

            var events = new[] { "PermissionRequest", "PreToolUse", "UserPromptSubmit", "SessionEnd" };
            var commandStr = $"\"{cmdPath}\"";

            foreach (var evt in events)
            {
                if (hooksObj[evt] is not JsonArray matchers)
                {
                    matchers = new JsonArray();
                    hooksObj[evt] = matchers;
                }

                // Hapus entri lama jika sudah ada
                for (int i = matchers.Count - 1; i >= 0; i--)
                {
                    if (matchers[i]?["hooks"] is JsonArray innerHooks)
                    {
                        bool hasIsland = false;
                        foreach (var inner in innerHooks)
                        {
                            var cmd = inner?["command"]?.ToString() ?? "";
                            if (cmd.Contains(HookIdentifier, StringComparison.OrdinalIgnoreCase))
                            {
                                hasIsland = true;
                                break;
                            }
                        }
                        if (hasIsland) matchers.RemoveAt(i);
                    }
                }

                var innerArray = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "command",
                        ["command"] = commandStr
                    }
                };

                var matcherObj = new JsonObject
                {
                    ["matcher"] = ".*",
                    ["hooks"] = innerArray
                };

                matchers.Add(matcherObj);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, root.ToJsonString(options));

            return (true, "Hook Claude Code berhasil dipasang.");
        }
        catch (Exception ex)
        {
            return (false, $"Gagal memasang hook: {ex.Message}");
        }
    }

    public static (bool Success, string Message) Uninstall(string? settingsPath = null)
    {
        var path = settingsPath ?? DefaultSettingsPath;
        if (!File.Exists(path)) return (true, "Berkas pengaturan tidak ditemukan.");

        try
        {
            var content = File.ReadAllText(path);
            var root = JsonNode.Parse(content) as JsonObject;
            if (root?["hooks"] is not JsonObject hooksObj) return (true, "Hook tidak terpasang.");

            foreach (var kvp in hooksObj.ToList())
            {
                if (kvp.Value is JsonArray matchers)
                {
                    for (int i = matchers.Count - 1; i >= 0; i--)
                    {
                        if (matchers[i]?["hooks"] is JsonArray innerHooks)
                        {
                            bool hasIsland = false;
                            foreach (var inner in innerHooks)
                            {
                                var cmd = inner?["command"]?.ToString() ?? "";
                                if (cmd.Contains(HookIdentifier, StringComparison.OrdinalIgnoreCase))
                                {
                                    hasIsland = true;
                                    break;
                                }
                            }
                            if (hasIsland) matchers.RemoveAt(i);
                        }
                    }

                    if (matchers.Count == 0)
                    {
                        hooksObj.Remove(kvp.Key);
                    }
                }
            }

            if (hooksObj.Count == 0)
            {
                root.Remove("hooks");
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, root.ToJsonString(options));

            return (true, "Hook Claude Code berhasil dilepas.");
        }
        catch (Exception ex)
        {
            return (false, $"Gagal melepas hook: {ex.Message}");
        }
    }

    public static void DeployHookScripts(string? targetDir = null)
    {
        var dir = targetDir ?? DefaultHooksDirectory;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var ps1Path = Path.Combine(dir, "island-hook.ps1");
        var cmdPath = Path.Combine(dir, "island-hook.cmd");

        File.WriteAllText(ps1Path, GenerateHookScriptContent());
        File.WriteAllText(cmdPath, GenerateHookCmdContent());
    }

    public static string GenerateHookScriptContent() =>
@"# island-hook.ps1 - Claude Code relay for Dynamic Island
param()

$InputJson = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($InputJson)) {
    exit 0
}

try {
    $data = $InputJson | ConvertFrom-Json
} catch {
    exit 0
}

$eventName = $data.hook_event_name
$toolName = if ($data.tool_name) { $data.tool_name } else { 'Claude Code' }
$toolInput = $data.tool_input
$sessionId = $data.session_id

$appData = [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)
$islandDir = Join-Path $appData 'DynamicIsland'
if (-not (Test-Path $islandDir)) {
    [System.IO.Directory]::CreateDirectory($islandDir) | Out-Null
}

$statusFile = Join-Path $islandDir 'agent-status.json'
$responseFile = Join-Path $islandDir 'agent-response.json'

$summary = ''
$detail = ''

if ($toolName -eq 'Bash' -and $toolInput.command) {
    $summary = 'Bash: ' + $toolInput.command
    $detail = $toolInput.command
} elseif (($toolName -eq 'FileEdit' -or $toolName -eq 'Edit') -and $toolInput.file_path) {
    $summary = 'Edit: ' + $toolInput.file_path
    $detail = $toolInput.file_path
} elseif (($toolName -eq 'Write' -or $toolName -eq 'FileWrite') -and $toolInput.file_path) {
    $summary = 'Write: ' + $toolInput.file_path
    $detail = $toolInput.file_path
} else {
    $summary = $toolName
    if ($toolInput) {
        $detail = ($toolInput | ConvertTo-Json -Compress)
    }
}

if ($eventName -eq 'PermissionRequest') {
    $reqId = 'req-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    
    if (Test-Path $responseFile) {
        Remove-Item $responseFile -Force -ErrorAction SilentlyContinue
    }

    $statusObj = @{
        tool = 'Claude Code'
        state = 'waiting'
        request = @{
            id = $reqId
            summary = $summary
            detail = $detail
        }
        updated_at = (Get-Date).ToString('o')
    }

    $statusObj | ConvertTo-Json -Depth 5 | Set-Content $statusFile -Encoding UTF8

    $deadline = (Get-Date).AddSeconds(25)
    $answered = $false
    $approved = $false

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 100
        if (Test-Path $responseFile) {
            try {
                $respContent = Get-Content $responseFile -Raw -ErrorAction SilentlyContinue
                if ($respContent) {
                    $resp = $respContent | ConvertFrom-Json
                    if ($resp.request_id -eq $reqId) {
                        $answered = $true
                        $approved = [bool]$resp.approved
                        break
                    }
                }
            } catch {}
        }
    }

    $statusObj.state = if ($approved) { 'working' } else { 'idle' }
    $statusObj.request = $null
    $statusObj.updated_at = (Get-Date).ToString('o')
    $statusObj | ConvertTo-Json -Depth 5 | Set-Content $statusFile -Encoding UTF8

    if ($answered) {
        if ($approved) {
            @{
                hookSpecificOutput = @{
                    permissionDecision = 'allow'
                    permissionDecisionReason = 'Disetujui dari Dynamic Island'
                }
            } | ConvertTo-Json -Compress
            exit 0
        } else {
            @{
                hookSpecificOutput = @{
                    permissionDecision = 'deny'
                    permissionDecisionReason = 'Ditolak dari Dynamic Island'
                }
            } | ConvertTo-Json -Compress
            exit 0
        }
    } else {
        @{
            hookSpecificOutput = @{
                permissionDecision = 'ask'
            }
        } | ConvertTo-Json -Compress
        exit 0
    }
} elseif ($eventName -eq 'PreToolUse') {
    $statusObj = @{
        tool = 'Claude Code: ' + $toolName
        state = 'working'
        request = $null
        updated_at = (Get-Date).ToString('o')
    }
    $statusObj | ConvertTo-Json -Depth 5 | Set-Content $statusFile -Encoding UTF8
    exit 0
} elseif ($eventName -eq 'UserPromptSubmit') {
    $statusObj = @{
        tool = 'Claude Code'
        state = 'working'
        request = $null
        updated_at = (Get-Date).ToString('o')
    }
    $statusObj | ConvertTo-Json -Depth 5 | Set-Content $statusFile -Encoding UTF8
    exit 0
} elseif ($eventName -eq 'SessionEnd') {
    $statusObj = @{
        tool = 'Claude Code'
        state = 'idle'
        request = $null
        updated_at = (Get-Date).ToString('o')
    }
    $statusObj | ConvertTo-Json -Depth 5 | Set-Content $statusFile -Encoding UTF8
    exit 0
} else {
    exit 0
}
";

    public static string GenerateHookCmdContent() =>
@"@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""%~dp0island-hook.ps1"" %*
";
}
