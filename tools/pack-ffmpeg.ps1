# 把 assets\ffmpeg 下的 ffmpeg.exe / ffprobe.exe 压缩成 Brotli 资源（*.br），供内核以嵌入资源形式携带。
# 用法：pwsh -NoProfile -File tools\pack-ffmpeg.ps1 [-Force]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'assets\ffmpeg'

if (-not (Test-Path (Join-Path $src 'ffmpeg.exe'))) {
    throw "缺少 $src\ffmpeg.exe，先按 docs\BUILD.md 准备 ffmpeg。"
}

foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) {
    $input = Join-Path $src $name
    $output = "$input.br"
    if (-not (Test-Path $input)) {
        throw "缺少 $input"
    }

    if ((Test-Path $output) -and -not $Force) {
        $i = Get-Item $input
        $o = Get-Item $output
        if ($o.LastWriteTime -ge $i.LastWriteTime) {
            Write-Host "跳过（已是最新）：$name.br  $([math]::Round($o.Length / 1MB, 1)) MB"
            continue
        }
    }

    $inStream = [System.IO.File]::OpenRead($input)
    $outStream = [System.IO.File]::Create($output)
    try {
        $brotli = [System.IO.Compression.BrotliStream]::new(
            $outStream,
            [System.IO.Compression.CompressionLevel]::SmallestSize,
            $false)
        try {
            $inStream.CopyTo($brotli, 1MB)
        }
        finally {
            $brotli.Dispose()
        }
    }
    finally {
        $inStream.Dispose()
        $outStream.Dispose()
    }

    $o = Get-Item $output
    Write-Host ("压缩完成：{0}.br  {1:N1} MB（原 {2:N1} MB）" -f $name, ($o.Length / 1MB), ((Get-Item $input).Length / 1MB))
}

# 记录版本串，运行期只用于日志与缓存目录标识。
$versionFile = Join-Path $src 'ffmpeg-version.txt'
& (Join-Path $src 'ffmpeg.exe') -version | Select-Object -First 1 | Set-Content $versionFile -Encoding utf8
Write-Host ("版本串：{0}" -f (Get-Content $versionFile -TotalCount 1))
