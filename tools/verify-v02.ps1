# v0.2 完整验收：拖入混合文件 → 转换（含失败项）→ 队列/设置/关于 → 输出位置浮层 → 慢任务进度。
param([string]$Out = "artifacts")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$repo = Split-Path -Parent $PSScriptRoot
$outDir = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $repo $Out }
$exe = Join-Path $repo "src\FormaToys.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\FormaToys.exe"
$samples = Join-Path $repo ".cache\samples"

function Shot([string]$name) {
    $line = pwsh -NoProfile -File (Join-Path $PSScriptRoot "capture.ps1") -Out (Join-Path $outDir $name) -ProcessName FormaToys
    "$name :: $line"
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

function Press([string]$name) {
    $element = Find-By $name
    if (-not $element) { $element = Find-By $name -Desktop }
    if (-not $element) { return "missing: $name" }
    $pattern = $null
    if ($element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); return "invoked $name" }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) { $pattern.Expand(); return "expanded $name" }
    if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { $pattern.Select(); return "selected $name" }
    return "no-pattern: $name"
}

function Launch([string[]]$files) {
    Get-Process FormaToys -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 600
    Start-Process -FilePath $exe -ArgumentList $files -WorkingDirectory (Split-Path $exe)
    Start-Sleep -Seconds 5
}

# ---------- 第 1 幕：混合文件（含一个坏文件） ----------
Launch @("$samples\sample.mp4", "$samples\sample.mp3", "$samples\sample.png", "$samples\broken.mp4")
Shot "v02-e01-pending.png"
Press "开始转换" | Out-Null
Start-Sleep -Seconds 4
Shot "v02-e02-mixed-done.png"

Press "队列" | Out-Null
Start-Sleep -Milliseconds 900
Shot "v02-e03-queue.png"

Press "设置" | Out-Null
Start-Sleep -Milliseconds 900
Shot "v02-e04-settings.png"

Press "关于" | Out-Null
Start-Sleep -Milliseconds 900
Shot "v02-e05-about.png"

Press "返回" | Out-Null
Start-Sleep -Milliseconds 700
Press "转换" | Out-Null
Start-Sleep -Milliseconds 700
Press "输出位置" | Out-Null
Start-Sleep -Milliseconds 700
Shot "v02-e06-output-flyout.png"

# ---------- 第 2 幕：慢任务，看真实进度 ----------
Launch @("$samples\big.mp4")
$combo = (Get-Root).FindAll([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, "目标格式")).Item(0)
$expand = $null
$combo.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$expand) | Out-Null
$expand.Expand()
Start-Sleep -Milliseconds 600
$webm = Find-By "WebM" -Desktop
if ($webm) {
    $select = $null
    $webm.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$select) | Out-Null
    $select.Select()
}
Start-Sleep -Milliseconds 400
Press "开始转换" | Out-Null
Start-Sleep -Milliseconds 2500
Shot "v02-e07-running.png"
Start-Sleep -Milliseconds 3000
Shot "v02-e08-running2.png"
Press "取消转换" | Out-Null
Start-Sleep -Milliseconds 1200
Shot "v02-e09-canceled.png"
"verify done"
