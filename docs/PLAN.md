# FormaToys 方案（0.4 设计稿）

一句话：**拖入文件，为每个文件选择目标格式，开始转换。** 其余一概不做。

“格式工厂”好用是因为它不需要你先学会 ffmpeg；难用是因为它十几年没换过界面、装完一堆捆绑、
参数表能盖住半个屏幕。FormaToys 只保留这条主干，界面骨架照 PowerToys 的样子重做过一次：
左侧导航栏、右侧卡片、任务按状态分段。

- 平台：Windows 11 独占（Mica、圆角、WinUI 3 控件依赖 Win11 的渲染与 DWM 行为）
- 技术：C# / .NET 8 / WinUI 3（Windows App SDK 1.8），**零 WebView、零 Electron**
- 引擎：内嵌 GPL 版 ffmpeg.exe / ffprobe.exe，进程调用，不链接、不封装
- 体积目标：程序本体约 4 MB，其余是 ffmpeg 二进制（约 330 MB，后续换裁剪构建）

## 1. 页面结构

```
┌──────────┬──────────────────────────────────────────────────────────────┐
│ ☰        │  转换                                                        │
│  → 转换  │  ┌──────────────────────────────────────────────────────┐  │
│          │  │ ☐全选  [批量设置格式⌄]                    [取消] [▶ 开始转换·3]│  │
│          │  └──────────────────────────────────────────────────────┘  │
│          │  ┌─ 总进度 ───────────────────────────────  已完成 1/3 · 68% ┐│
│          │  └──────────────────────────────────────────────────────┘  │
│          │  待转换 · 1                                                │
│          │  ┌ ☐ 🎬 sample.mp4  1280×720 · 30fps · 0:06 · 2.3MB  等待 [MP4⌄] …┐
│          │  转换中 · 1                                                │
│          │  ┌ ☐ 🎵 song.mp3  0:05 · MP3   ▓▓▓▓░░ 42%·0:02/0:05  转换中 [FLAC] 取消 …┐
│          │  已完成 · 1                                                │
│          │  ┌ ☐ ▣ pic.png  800×600 · PNG         完成 → JPG · 49.5 KB 📂 🗀 …┐
│          │                    [ 添加文件 ]                            │
│          │  3 个文件 · 2.3 MB · 已完成 1                               │
├──────────┴──────────────────────────────────────────────────────────────┤
│ ⚙ 设置                       （页脚，不再放右上角齿轮）                    │
│ ⓘ 关于                                                                  │
└─────────────────────────────────────────────────────────────────────────┘
```

三个页面，全部在同一个窗口里换内容，不做多窗口：

| 页面 | 内容 | 入口 |
| --- | --- | --- |
| 转换 | 工具条 + 总进度 + 三段任务列表 | 侧栏第一项（默认） |
| 设置 | 输出位置 / 输出质量 / 转换 / 转换引擎 / 配置文件 五张卡 | 侧栏页脚 |
| 关于 | 身份卡 + 开源与许可证 | 侧栏页脚 |

所有页面统一：外边距 24，标题与内容左边距 24，卡片横向铺满，卡片间距 12；页面不写副标题。

## 2. 交互规则（决定“不臃肿”）

1. **格式跟着文件走，不是跟着页面走**。每个文件行右侧自带一个下拉框，选项只包含该文件所属
   类别的格式（视频 MP4/MKV/WebM/GIF，音频 MP3/M4A/WAV/FLAC/OGG，图片 JPG/PNG/WebP/BMP）。
   同一个列表里，mp4 可以转 WebM、mp3 转 FLAC、png 转 JPG，互不干扰。
2. **但批量不能靠手点**。工具条上有“批量设置格式”，按类型给出子菜单：
   `所有视频 → MP4 / MKV / WebM / GIF`、`所有音频 → …`、`所有图片 → …`，
   直接作用于该类型的全部文件，不做勾选、不弹提示。
   列表不做勾选：文件本来就少，勾选只会多一层心智负担。
3. **三段是同一份数据的不同视图**，不是三个列表：文件状态变了就自动搬到对应段，空段整段隐藏
   （包括标题），所以只有 1 个文件时界面上只有一段，不会出现两个空标题。
4. **质量只有三档**：文件小 / 均衡 / 高画质，放在**设置 → 输出质量**的下拉框里，转换页不再重复出现。
   不暴露 CRF、preset、码率；三档在 `FfmpegCommandBuilder` 里映射成一张固定参数表，
   不支持的格式（WAV/FLAC/PNG/BMP）自动忽略质量档。
5. **进度必须是真的**。ffmpeg 用 `-progress pipe:1 -stats_period 0.5` 输出 `out_time_us`，
   引擎每 **16 ms** 采样一次并上报 `ConversionProgress(Ratio, Elapsed, Total)`；总时长未知时
   `IsDeterminate = false`，界面切不确定态进度条，只显示已处理时长——任何时候都不做假进度。
6. **输出默认落在源文件旁边**，重名自动加 ` (1)`；全局输出目录在**设置 → 输出位置**统一修改，
   单个文件可以在行尾“…”里单独指定（行内覆盖优先级最高，可一键“恢复全局输出位置”）。
7. **失败必须说清楚**：行内第二行贴 ffmpeg 的关键错误（例如
   `Error opening input files: Invalid data found when processing input`），完整 stderr 留在悬停提示，
   行左侧状态条转红。单个失败不影响同批其他文件。
8. **其他细节**：行左侧 3 px 状态色条（转换中＝强调色、失败＝红、完成＝绿、已取消＝琥珀），
   完成行给出 `→ WebM · 2.9 MB`；全部跑完之后主按钮文案变为“重新转换”。
9. **文案规范**：全部界面文字使用书面语，不使用口语化表达（“更换”而非“换一个”，“移除”而非“删掉”），
   句子不加感叹号，操作按钮用动宾结构，说明文字单句不成段。

## 3. 架构：界面不认识 ffmpeg 命令行

```
FormaToys.App  (WinUI 3, net8.0-windows)
  MainWindow      自定义标题栏 + NavigationView + Frame 路由（convert/queue/settings/about）
  Views/          ConvertPage、QueuePage、SettingsPage、AboutPage：只做布局与事件
  Controls/       FileRow（一个文件行：图标 / 源信息 / 格式选择器 / 进度 / 状态 / 动作）
  ViewModels/     ConvertQueueViewModel（三段集合、批量、并发、总进度、取消）
                  FileItemViewModel（单行状态机：Pending/Running/Completed/Failed/Canceled）
  Services/       AppServices（组合根）、ShellService（explorer 调用）
────────────────────────────────────────────────────────────────
FormaToys.Core (net8.0, 无 UI 依赖)
  Media/          MediaProbe（ffprobe→MediaInfo）、FormatCatalog（格式表）、MediaKind
  Conversion/     FfmpegCommandBuilder（参数表）、ConversionEngine（进程 + 16 ms 进度 + 取消）、
                  ConversionProgress、OutputPlanner、ProgressParser
  Tooling/        ToolPaths（5 级查找 ffmpeg）、EmbeddedTools（解压内嵌 ffmpeg）、StartupCheck（版本/GPL 自检）
  Settings/       AppSettings + SettingsStore（%LOCALAPPDATA%\FormaToys\settings.json）
```

**为什么拆 Core**：转换逻辑与 UI 完全解耦，可以脱离窗口跑（`tools/FormaToys.Smoke`
就是这么来的：同一套引擎，命令行把 14 种转换全跑一遍并 ffprobe 校验输出）。

**为什么用进程而不是绑定库**：内嵌官方 GPL 构建的 ffmpeg.exe（Brotli 压缩后作为嵌入资源随程序集分发），升级只需重跑 `tools\pack-ffmpeg.ps1` 替换两个 `.br`；
参数、进度、取消都走标准 IO，交叉验证容易。代价是每次调用多几十毫秒启动开销，
相对一次真实转码可以忽略。

### 关键实现细节

- **进度采样**：stdout 回调只记录 `latestTicks`，由 `System.Threading.Timer` 每 16 ms 采一次样，
  避免 UI 线程被高频事件打满；`progress=end` 直接落到 100%。
- **取消**：`Kill(entireProcessTree: true)`，并删除半成品输出文件，不留垃圾；单行取消用
  `CreateLinkedTokenSource` 与整批取消联动。
- **并发**：`SemaphoreSlim(设置里的并发数)`，默认 1（避免普通笔记本被 ffmpeg 抢满 CPU），可选 1–4。
- **失败判定**：退出码非 0，或退出码 0 但输出文件不存在/为 0 字节。
- **路径**：ffmpeg 进程的 `WorkingDirectory` 指向工具目录，因此**输入输出路径必须先
  `Path.GetFullPath` 固化**，否则相对路径会解析到工具目录下（0.1 开发中真实踩过的坑）。
- **Mica**：`Microsoft.UI.Xaml.Media.MicaBackdrop`（注意不是
  `Microsoft.UI.Composition.SystemBackdrops` 里那个），失败时静默退回纯色背景。
- **标题栏**：`ExtendsContentIntoTitleBar + SetTitleBar`，右侧给系统按钮留 150 px；
  NavigationView 关掉自带的 header 和 settings 项，页脚自己放。

## 4. 格式与质量映射（0.2 已实现）

| 目标 | 质量=文件小 | 均衡 | 高画质 |
| --- | --- | --- | --- |
| MP4 / MKV | x264 crf 30 veryfast | crf 23 medium | crf 18 slow，profile high@4.1 |
| WebM | VP9 crf 36 cpu-used 5 | crf 32 cpu-used 3 | crf 28 cpu-used 1 |
| GIF | fps 12 / 宽 360 / 抖动 bayer | 15 / 480 / sierra2_4a | 24 / 640 / floyd_steinberg |
| MP3 | LAME -q:a 7 | -q:a 2 | -q:a 0 |
| M4A | AAC 96k | 160k | 256k |
| WAV | PCM s16le（无质量档） | ← | ← |
| FLAC | compression_level 8（无质量档） | ← | ← |
| OGG | Vorbis -q:a 3 | -q:a 5 | -q:a 8 |
| JPG | mjpeg -q:v 9 | -q:v 4 | -q:v 2 |
| PNG | compression_level 9（无质量档） | ← | ← |
| WebP | -quality 70 | 85 | 95 |
| BMP | 无损（无质量档） | ← | ← |

GIF 走 `palettegen`/`paletteuse` 两遍调色板滤镜，否则颜色会糊成一片。

## 5. 路线图

**M1（v0.1，已交付）**：拖放/选择文件、ffprobe 探测、右侧按类别分组选格式、三档质量、
输出位置、开始/取消、行级进度与失败原因、设置页、GPL 声明、命令行冒烟测试。

**M2（v0.2，本次交付）**：
- NavigationView 骨架（转换 / 队列 / 设置 / 关于），设置移到侧栏，子页返回
- 转换页三段（待转换 / 转换中 / 已完成）+ 段标题计数 + 空段隐藏
- 每文件独立格式选择器 + 按类型的批量设置入口
- 16 ms 真实进度 + 不确定态 + 顶部总进度（已完成 N / 总数 M）
- 单文件输出目录覆盖、并发数设置、完成行“打开/定位/重新转换”
- 队列页（全部任务平铺）、关于页（身份 + 许可证）

**M3（下一步）**：
- 裁剪版 ffmpeg（只留常用编解码器），整包压到 100 MB 以内
- 单实例 + 拖到任务栏图标接收文件、任务完成 Toast
- 逐文件分辨率/裁剪/静音开关（仍然只给开关，不给参数表）
- 目标大小模式（两遍编码）、硬件编码（NVENC/QSV/AMF）探测与回退
- CLI `--preset`，供右键菜单 / “发送到”集成

**明确不做**：账号、云转换、下载器、捆绑安装、广告、参数专家面板、视频剪辑时间线。

## 6. 风险与已核实结论

| 风险 | 结论 |
| --- | --- |
| 没有 VS，只有 dotnet SDK | 可行：`UseWinUI` 工程用 `dotnet build` 直接出 exe，无需 MSBuild 的 VS 组件 |
| WindowsAppSDK 与 BuildTools 版本冲突 | 不显式引用 `Microsoft.Windows.SDK.BuildTools`，靠传递依赖，否则 NU1605 |
| ffmpeg 体积 | 两个 exe 约 330 MB 的原始构建；GPL 版无更小选择，M3 换裁剪构建 |
| ffmpeg 版本漂移 | 0.2 仍固定 BtbN master 快照 `N-129130-gcd39c36377-20261119`，发版前改为固定 release 资产 |
| 高频进度事件拖慢 UI | 已改为 16 ms 定时采样 + 只保留最新值，实测 1080p VP9 编码期间界面流畅 |
| 非 GPL 需求 | 若将来要闭源分发，需换成 LGPL 构建并去掉 libx264/libx265 依赖（届时 MP4 改用硬件或 openh264） |
