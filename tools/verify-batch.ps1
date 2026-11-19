# 演练批量设置格式：打开「批量设置格式」→ 所有音频 → FLAC，然后开始转换并截图。
param([string]$Out = "artifacts")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$repo = Split-Path -Parent $PSScriptRoot
$outDir = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $repo $Out }

function Shot([string]$name) {
    pwsh -NoProfile -File (Join-Path $PSScriptRoot "capture.ps1") -Out (Join-Path $outDir $name) -ProcessName FormaToys | Out-Null
    "shot: $name"
}

function Get-Root {
    $proc = Get-Process FormaToys | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
    [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
}

function Find-By([string]$name, [switch]$Desktop) {
    $scope = if ($Desktop) { [System.Windows.Automation.AutomationElement]::RootElement } else { Get-Root }
    $hits = $scope.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $name))
    if ($hits.Count -gt 0) { return $hits.Item(0) }
    return $null
}

function Press($element) {
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke(); return "invoked " + $element.Current.Name
    }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) {
        $pattern.Expand(); return "expanded " + $element.Current.Name
    }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select(); return "selected " + $element.Current.Name
    }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
        $pattern.Toggle(); return "toggled " + $element.Current.Name
    }
    return "no pattern: " + $element.Current.Name
}

$batch = Find-By "批量设置格式"
Press $batch
Start-Sleep -Milliseconds 600
Shot "v02-c04-batch-menu.png"

$sub = Find-By "所有音频" -Desktop
if ($sub) { Press $sub; Start-Sleep -Milliseconds 500 }
Shot "v02-c05-batch-audio.png"

foreach ($candidate in @("→ FLAC", "FLAC")) {
    $item = Find-By $candidate -Desktop
    if ($item) { Press $item; break }
}
Start-Sleep -Milliseconds 600
Shot "v02-c06-batch-applied.png"

$start = Find-By "开始转换"
if ($start) { Press $start }
Start-Sleep -Milliseconds 700
Shot "v02-c07-running.png"
Start-Sleep -Seconds 6
Shot "v02-c08-done.png"

$queue = Find-By "队列"
if ($queue) { Press $queue }
Start-Sleep -Milliseconds 800
Shot "v02-c09-queue.png"
"done"
