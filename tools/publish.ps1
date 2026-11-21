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

    # ffmpeg 现在以 Brotli 嵌入资源随 FormaToys.Core.dll 分发，发布目录里不再有 ffmpeg\ 子目录。
    foreach ($required in @("assets\ffmpeg\ffmpeg.exe.br", "assets\ffmpeg\ffprobe.exe.br", "assets\ffmpeg\LICENSE.txt")) {
        if (-not (Test-Path $required)) {
            Write-Error "缺少 $required，先按 docs\BUILD.md 准备 ffmpeg 资源（tools\pack-ffmpeg.ps1）。"
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

    foreach ($expected in @("FormaToys.exe", "FormaToys.dll", "FormaToys.Core.dll", "FormaToys.pri", "LICENSE.txt")) {
        if (-not (Test-Path (Join-Path $Out $expected))) {
            Write-Error "发布目录缺少 $expected"
        }
    }

    $core = Get-Item (Join-Path $Out "FormaToys.Core.dll")
    Write-Host ("内嵌 ffmpeg 的内核：{0:N1} MB" -f ($core.Length / 1MB))

    $size = (Get-ChildItem $Out -Recurse -File | Measure-Object -Property Length -Sum).Sum
    Write-Host ("发布完成：{0}  共 {1:N0} 个文件，{2:N1} MB" -f (Resolve-Path $Out).Path, (Get-ChildItem $Out -Recurse -File).Count, ($size / 1MB))
}
finally {
    Pop-Location
}
