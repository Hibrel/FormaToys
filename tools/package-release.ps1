# 把已发布的 artifacts\dist 打成发布物：
#   1) FormaToys-<ver>-win-x64.zip      便携版（解压即用）
#   2) FormaToys-<ver>-win-x64.exe      单文件启动器（内嵌同一份载荷，双击即用）
# 用法：pwsh -NoProfile -File tools\package-release.ps1 [-SkipPublish]
param(
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'artifacts\dist'
$release = Join-Path $root 'artifacts\release'

$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Directory.Build.props 里没有 Version。' }
Write-Host "版本：$version"

if (-not $SkipPublish) {
    & (Join-Path $root 'tools\publish.ps1')
}

if (-not (Test-Path (Join-Path $dist 'FormaToys.exe'))) {
    throw "缺少 $dist\FormaToys.exe，先发布。"
}

New-Item -ItemType Directory -Force $release | Out-Null

# 1) 便携版 zip
$zip = Join-Path $release "FormaToys-$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $dist,
    $zip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)
Write-Host ("便携版：{0}  {1:N1} MB" -f (Split-Path -Leaf $zip), ((Get-Item $zip).Length / 1MB))

# 2) 单文件启动器：把同一份 zip 作为载荷嵌入，并打成单文件 exe
$setupProject = Join-Path $root 'tools\FormaToys.Setup\FormaToys.Setup.csproj'
$assets = Join-Path $root 'tools\FormaToys.Setup\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null
Copy-Item $zip (Join-Path $assets 'payload.zip') -Force

$setupOut = Join-Path $root 'artifacts\setup-single'
if (Test-Path $setupOut) { Remove-Item $setupOut -Recurse -Force }
dotnet publish $setupProject -c Release -r win-x64 `
    -p:PublishSingleFile=true -p:SelfContained=false -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $setupOut --nologo | Out-Host
if ($LASTEXITCODE -ne 0) { throw '启动器构建失败。' }

$setupExe = Join-Path $release "FormaToys-$version-win-x64.exe"
Copy-Item (Join-Path $setupOut 'FormaToys-Setup.exe') $setupExe -Force
Write-Host ("单文件版：{0}  {1:N1} MB" -f (Split-Path -Leaf $setupExe), ((Get-Item $setupExe).Length / 1MB))

Remove-Item (Join-Path $assets 'payload.zip') -Force

Write-Host "`n发布物目录：$release"
Get-ChildItem $release | Select-Object Name, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize | Out-String | Write-Host
