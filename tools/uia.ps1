# 用 UI Automation 操作 FormaToys 窗口，做端到端验收（点按钮、读状态、截图）。
# 用法：
#   pwsh -File tools\uia.ps1 -Action list
#   pwsh -File tools\uia.ps1 -Action invoke -Name "开始转换"
#   pwsh -File tools\uia.ps1 -Action select -Name "队列"
#   pwsh -File tools\uia.ps1 -Action set -Name "目标格式" -Value "MKV"   # ComboBox 选项
param(
    [ValidateSet("list", "invoke", "set")]
    [string]$Action = "list",
    [string]$Name = "",
    [string]$Value = "",
    [string]$ProcessName = "FormaToys",
    [int]$WaitSeconds = 10
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-AppRoot {
    $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowHandle -ne 0 } |
        Select-Object -First 1
    if (-not $proc) { return $null }
    return [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)
}

function Find-Target($root, $name) {
    $all = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    $loose = $null
    foreach ($element in $all) {
        $current = $element.Current.Name
        if ($current -eq $name) { return $element }
        if (-not $loose -and $current -and $current.StartsWith($name, [StringComparison]::Ordinal)) {
            $loose = $element
        }
    }
    return $loose
}

$root = Get-AppRoot
if (-not $root) { Write-Error "未找到 $ProcessName 窗口"; exit 1 }

if ($Action -eq "list") {
    $all = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($element in $all) {
        $controlType = $element.Current.ControlType.ProgrammaticName -replace "ControlType\.", ""
        $label = $element.Current.Name
        if ($controlType -in @("Button", "Text", "ToggleButton", "RadioButton", "CheckBox", "ListItem", "ProgressBar", "ComboBox", "ComboBoxItem", "Edit", "MenuItem")) {
            "{0,-14} {1}" -f $controlType, $label
        }
    }
    exit 0
}

# UI 刚切换页面时自动化树可能还没建好，轮询等待。
$target = $null
$deadline = (Get-Date).AddSeconds($WaitSeconds)
while (-not $target -and (Get-Date) -lt $deadline) {
    $target = Find-Target $root $Name
    if (-not $target) { Start-Sleep -Milliseconds 300; $root = Get-AppRoot }
}
if (-not $target) { Write-Error "找不到名为 '$Name' 的元素"; exit 1 }

$pattern = $null
if ($Action -eq "invoke") {
    if ($target.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        $pattern.Invoke()
        "invoked: $($target.Current.Name)"
    } elseif ($target.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
        $pattern.Toggle()
        "toggled: $($target.Current.Name)"
    } elseif ($target.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
        $pattern.Select()
        "selected: $($target.Current.Name)"
    } else {
        Write-Error "元素 '$Name' 不支持 Invoke/Toggle/Select"
        exit 1
    }
} elseif ($Action -eq "set") {
    # ComboBox 展开后点同名选项。
    if ($target.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$pattern)) {
        $pattern.Expand()
        Start-Sleep -Milliseconds 300
        $option = Find-Target (Get-AppRoot) $Value
        if (-not $option) { Write-Error "找不到选项 '$Value'"; exit 1 }
        $optionPattern = $null
        if ($option.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$optionPattern)) {
            $optionPattern.Select()
            "selected: $Value"
        } elseif ($option.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$optionPattern)) {
            $optionPattern.Invoke()
            "picked: $Value"
        } else {
            Write-Error "选项 '$Value' 不可选"
            exit 1
        }
    } else {
        Write-Error "元素 '$Name' 不能展开"
        exit 1
    }
}
