# 构建与运行

## 环境

| 项 | 版本 | 说明 |
| --- | --- | --- |
| Windows | 11（25H2 / build 26200 实测） | WinUI 3 + Mica 需要 Win11 |
| .NET SDK | 10.0.403 | `global.json` 已锁定；目标框架仍是 net8.0 |
| .NET 运行时 | Microsoft.WindowsDesktop.App / NETCore 8.0.x | 程序框架依赖部署 |
| Windows App SDK | 1.8.251106002（NuGet） | `WindowsAppSDKSelfContained=true`，无需单独装运行时 |
| Visual Studio | **不需要** | 全部命令走 `dotnet` CLI |

## 一次性准备 ffmpeg

`assets/ffmpeg/` 需要 `ffmpeg.exe`、`ffprobe.exe`、`LICENSE.txt`（不放进版本库，见 `.gitignore`）：

```powershell
# 0.1 使用的构建：BtbN GPL 快照
$url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
Invoke-WebRequest $url -OutFile $env:TEMP\ffmpeg.zip
Expand-Archive $env:TEMP\ffmpeg.zip -DestinationPath $env:TEMP\ffmpeg-x -Force
$src = Get-ChildItem $env:TEMP\ffmpeg-x -Directory | Select-Object -First 1
New-Item -ItemType Directory -Force assets\ffmpeg | Out-Null
Copy-Item "$($src.FullName)\bin\ffmpeg.exe","$($src.FullName)\bin\ffprobe.exe" assets\ffmpeg\
Copy-Item "$($src.FullName)\LICENSE.txt" assets\ffmpeg\
& assets\ffmpeg\ffmpeg.exe -version | Select-Object -First 1
```

`FormaToys.App.csproj` 把这三个文件以 `Link="ffmpeg\*"` 复制到输出目录，所以运行时布局是：

```
FormaToys.exe
FormaToys.dll
ffmpeg\ffmpeg.exe
ffmpeg\ffprobe.exe
ffmpeg\LICENSE.txt
…（Windows App SDK 自包含文件）
```

## 构建 / 运行

```powershell
dotnet build FormaToys.slnx -c Debug
.\src\FormaToys.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\FormaToys.exe
```

也可以带文件参数直接开：

```powershell
$exe = ".\src\FormaToys.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\FormaToys.exe"
& $exe .\.cache\samples\sample.mp4 .\.cache\samples\sample.png
```

## 冒烟测试（不需要开窗口）

```powershell
dotnet run --project tools\FormaToys.Smoke\FormaToys.Smoke.csproj -c Release
```

它会用 `assets\ffmpeg` 现场生成样本（6 s 视频 / 5 s 音频 / 1 张图），
把 14 种目标格式全部转一遍，再用 ffprobe 校验输出，结果落在 `artifacts\smoke\`。
退出码 0 表示全绿。找不到 ffmpeg 时可用 `--ffmpeg-dir <目录>` 或环境变量
`FORMATOYS_FFMPEG_DIR` 指定。

## 发布

```powershell
pwsh -NoProfile -File tools\publish.ps1            # 框架依赖，输出 artifacts\dist
pwsh -NoProfile -File tools\publish.ps1 -SelfContained   # 连 .NET 运行时一起带
```

产物在 `artifacts\dist\`，整个目录拷走即可运行。

## 图标

```powershell
pwsh -NoProfile -File tools\make-icon.ps1   # 重新生成 src\FormaToys.App\Assets\app.ico
```

## 开发期验证脚本

| 脚本 | 用途 |
| --- | --- |
| `tools\capture.ps1 -Out artifacts\shot.png` | 截当前 FormaToys 窗口（先试 `PrintWindow(PW_RENDERFULLCONTENT)`，被遮挡也能拍到；失败退回屏幕拷贝） |
| `tools\uia.ps1 -Action list` | 列出窗口内的 UI Automation 元素（找按钮名） |
| `tools\uia.ps1 -Action invoke -Name "开始转换"` | 用 UIA 点按钮，做端到端验证（找不到元素时会轮询等 10 s） |
| `tools\verify-v02.ps1` | 完整跑一遍：混合文件（含坏文件）→ 转换 → 队列/设置/关于 → 输出位置浮层 → 慢任务进度 → 取消，截图落在 `artifacts\v02-e*.png` |

UIA 实用细节：`MenuFlyout` / `ComboBox` 的弹出层是独立 HWND，从主窗口的
`TreeScope.Descendants` 里找不到，要从 `AutomationElement.RootElement` 去找（`uia.ps1` 的 `-Desktop` 分支就是这么做的）。

## 踩过的坑（别再踩）

1. **`MicaBackdrop` 的命名空间**：在 Windows App SDK 1.8 里它是
   `Microsoft.UI.Xaml.Media.MicaBackdrop`，不是 `Microsoft.UI.Composition.SystemBackdrops`。
   写错会得到 `CS0246`，它还会连带让 XAML 编译器报一句没营养的
   `MSB3073 … XamlCompiler.exe 已退出，代码为 1`（output.json 里其实全是性能标记）。
2. **不要显式引用 `Microsoft.Windows.SDK.BuildTools`**：会出现 `NU1605`
   （WindowsAppSDK.Base 要求 ≥ 10.0.26100.4654）。删掉这条 PackageReference 即可。
3. **相对路径 + `WorkingDirectory`**：`ConversionEngine` 把 ffmpeg 的工作目录设成工具目录，
   所以请求里的输入/输出路径必须在进入引擎时 `Path.GetFullPath` 固化，
   否则 ffmpeg 会去 `…\ffmpeg\.cache\samples\` 里找文件，表现为“全部失败”。
4. **GIF 的类别**：ffprobe 对动图 GIF 报的是单条 video 流，容易被误判成静态图片。
   `MediaProbe` 用 `nb_frames > 1 || duration ≥ 1s` 把动图归到 Video。
5. **`dotnet new sln` 在 SDK 10 上生成 `.slnx`**：不要去找 `.sln`。
6. **XAML 里的 `{Binding}` 与 `{x:Bind}` 别混着猜**：`FileRow` 是 `UserControl`，`DataContext`
   是 `FileItemViewModel`，所以行内一律用 `{Binding}`；页面级用 `{x:Bind Vm.Xxx, Mode=OneWay}`。
7. **截图会拍到旧帧/别的窗口**：普通 `CopyFromScreen` 在目标窗口失焦或被挡住时会拍到别人，
   所以 `capture.ps1` 改用 `PrintWindow(..., 2)`，并用“取中心像素是否全黑”来判定要不要退回屏幕拷贝。
8. **16 ms 进度不能在 stdout 回调里直接上报**：`OutputDataReceived` 频率不可控，
   改成回调只写 `latestTicks`、由 `Timer` 每 16 ms 采样，否则 UI 线程会被打满。

