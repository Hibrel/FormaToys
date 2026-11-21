# 许可证与第三方组件

## FormaToys

本项目以 **GNU General Public License v3.0** 发布。全文见仓库根目录的 `LICENSE`，
副本会随发布包一起分发（`LICENSE.txt`）。

- 可自由使用、修改与再分发。
- 衍生作品需保持同一许可证，并提供对应源码。

## FFmpeg（GPL v3）

程序内嵌的 ffmpeg 为 GPL 构建，通过独立进程调用（`Process.Start`），不链接、不静态包含其代码。

- 使用构建：`ffmpeg-master-latest-win64-gpl`，来自 <https://github.com/BtbN/FFmpeg-Builds>
- 实测版本字符串：`ffmpeg version N-129150-g7ddf7bc8a4-20261120`（`--enable-gpl` 已启用）
- 分发形式：Brotli 压缩后作为嵌入资源随 `FormaToys.Core.dll` 发布，首次运行时解压到
  `%LOCALAPPDATA%\FormaToys\tools\<版本>-<资源长度>\`；对应的二进制压缩文件同时保存在本仓库的
  `assets/ffmpeg/ffmpeg.exe.br` 与 `assets/ffmpeg/ffprobe.exe.br`，许可证全文为 `assets/ffmpeg/LICENSE.txt`
- 源码获取：
  - FFmpeg 官方仓库 <https://git.ffmpeg.org/ffmpeg.git>（对应版本见上方版本串）
  - BtbN 构建脚本与其原始源码包：<https://github.com/BtbN/FFmpeg-Builds>
- GPL v3 全文：<https://www.gnu.org/licenses/gpl-3.0.txt>

按 GPL v3 §6 的要求，分发者需提供“对应源码”的获取方式：本仓库与下载页均给出上面两条源码链接，
并可另行索取对应源码压缩包。

## Windows App SDK / WinUI 3

- `Microsoft.WindowsAppSDK` 1.8.251106002，MIT 许可
- <https://github.com/microsoft/WindowsAppSDK>

## .NET 8

- MIT 许可，<https://github.com/dotnet/runtime>
