# 发布/打包脚本：dotnet publish + 目录就绪检查
param(
    [string]$Configuration = "Release",
    [string]$Out = "artifacts\dist",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $project = "src\FormaToys.App\FormaToys.App.csproj"

    foreach ($required in @("assets\ffmpeg\ffmpeg.exe", "assets\ffmpeg\ffprobe.exe", "assets\ffmpeg\LICENSE.txt")) {
        if (-not (Test-Path $required)) {
            Write-Error "缺少 $required，先按 docs\BUILD.md 准备 ffmpeg。"
        }
    }

    if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }

    $args = @(
        "publish", $project,
        "-c", $Configuration,
        "-r", "win-x64",
        "-o", $Out,
        "-p:WindowsPackageType=None",
        "-p:EnableMsixTooling=false",
        "-p:SelfContained=$($SelfContained.IsPresent.ToString().ToLower())",
        "--nologo"
    )

    Write-Host "dotnet $($args -join ' ')"
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { Write-Error "publish 失败（退出码 $LASTEXITCODE）" }

    foreach ($expected in @("FormaToys.exe", "FormaToys.dll", "ffmpeg\ffmpeg.exe", "ffmpeg\ffprobe.exe", "ffmpeg\LICENSE.txt")) {
        if (-not (Test-Path (Join-Path $Out $expected))) {
            Write-Error "发布目录缺少 $expected"
        }
    }

    $size = (Get-ChildItem $Out -Recurse -File | Measure-Object -Property Length -Sum).Sum
    Write-Host ("发布完成：{0}  共 {1:N0} 个文件，{2:N1} MB" -f (Resolve-Path $Out).Path, (Get-ChildItem $Out -Recurse -File).Count, ($size / 1MB))
}
finally {
    Pop-Location
}
