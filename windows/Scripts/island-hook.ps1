# island-hook.ps1 - Claude Code relay for Dynamic Island
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
