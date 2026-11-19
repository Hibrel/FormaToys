# 截取 FormaToys 主窗口到 PNG，用于开发期人工/自动化验收。
# 先用 PrintWindow(PW_RENDERFULLCONTENT) 直接渲染窗口（不受遮挡影响），失败再退回屏幕拷贝。
param(
    [string]$Out = "artifacts\shot.png",
    [string]$ProcessName = "FormaToys"
)

Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class Win32Cap {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
}
'@

[Win32Cap]::SetProcessDPIAware() | Out-Null

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { Write-Error "未找到窗口进程 $ProcessName"; exit 1 }

$hwnd = $proc.MainWindowHandle
[Win32Cap]::ShowWindow($hwnd, 9) | Out-Null
[Win32Cap]::BringWindowToTop($hwnd) | Out-Null
[Win32Cap]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 600

$rect = New-Object Win32Cap+RECT
[Win32Cap]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
if ($w -le 0 -or $h -le 0) { Write-Error "窗口尺寸无效"; exit 1 }

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$target = Join-Path (Resolve-Path -LiteralPath (Split-Path -Parent $Out)).Path (Split-Path -Leaf $Out)

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$printed = [Win32Cap]::PrintWindow($hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc)

# PrintWindow 拿到全黑说明这个窗口不吃这条路径，退回屏幕上抓。
$needsScreen = $true
if ($printed) {
    $sample = $bmp.GetPixel([int]($w / 2), [int]($h / 2))
    if ($sample.R -gt 0 -or $sample.G -gt 0 -or $sample.B -gt 0) { $needsScreen = $false }
}
if ($needsScreen) {
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size $w, $h))
}

$bmp.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()

"captured $w x $h ($(if ($needsScreen) { 'screen' } else { 'printwindow' })) -> $Out"
