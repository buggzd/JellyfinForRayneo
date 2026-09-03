# Jellyfin for RayNeo Air

面向 RayNeo Air 系列眼镜的第三方 Jellyfin 客户端原型。眼镜端完整采用 Lucent 展示模板的 React/WebView 界面，Unity 负责 RayNeo 外接显示、会话和输入桥接；手机端也使用独立 WebView，承载登录、设置和 OLED 盲操触控板。Air 3S 不依赖头部追踪，主要通过上下左右焦点导航和确认/返回完成交互。

> 本项目不是 Jellyfin、RayNeo 或 Apple 的官方产品，与这些公司不存在隶属或背书关系。

界面和功能取舍以 [Jellyfin Web 复现规格](docs/Jellyfin-Web-Reproduction-Spec.md) 为对照，并按 RayNeo Air 的双显示与盲操场景调整；后续范围见 [功能路线图](docs/JELLYFIN_FEATURE_ROADMAP.md)。

## 当前能力

- Jellyfin UDP 局域网自动发现、Quick Connect 快速登录、帐号密码登录，以及手机 Activity、Unity 与眼镜 WebView 之间一致的本地会话恢复
- 手机端 Lucent 风格配置 WebView；负责服务器发现、登录、设置和 OLED 纯黑盲操触控板
- 眼镜端 Lucent 风格展示 WebView；不显示多余的“等待手机连接”页，获得画面后直接恢复会话或进入媒体库
- Unity Editor 手机伴侣模拟器与原生 UI 回退，可用于会话、场景和显示控制器测试
- 加载“我的媒体”、继续观看、下一集和各媒体库最近添加内容
- 电影/剧集海报网格、收藏、搜索、服务端分页、排序与观看状态筛选
- 通用文件夹逐层浏览，支持网课库中目录和普通视频混排，并保留返回栈
- 海报、背景图和用户观看进度展示，带内存图片缓存与并发限制
- 电影/剧集/季/单集详情、按季选集、相似内容、收藏和看过状态同步
- Android 硬件解码器与 System WebView 共同支持的 MP4/H.264、MP4/HEVC、WebM/VP9 或 WebM/AV1 优先直放；仅软件解码或容器不兼容的路径由 Jellyfin 输出 H.264/AAC HLS
- 播放、暂停、拖动进度、遥控快退/快进、上一集/下一集和音轨/字幕切换；文字字幕统一请求为 WebVTT 并由 Lucent 自绘字幕层显示，位图字幕由服务器烧录
- 向 Jellyfin 同步播放开始、进度和停止事件，用于继续观看与历史记录
- 眼镜播放状态、标题和进度实时同步到手机触控板；眼镜上的“管理登录”可直接打开手机设置页
- RayNeo 官方双显示链路；可选铺满双眼的 2D 镜像，或将同一个 WebView 渲染复制到 SBS 左右半帧的立体虚拟屏幕模式

这是可运行的 MVP，而不是完整播放器。目前尚未实现多服务器切换、离线下载、播放列表编辑和安全凭据存储。

## 技术基线

- Unity `2022.3.62f3c1`
- RayNeo Air SDK `1.0.3`
- RayNeo 提供的 Cardboard XR Plugin 包 `1.0.3`（包内版本 `1.17.0`）
- Jellyfin API：按 `10.10.7` 与 `12.0.0` 的公共接口设计
- Android：API 26+、ARM64、IL2CPP、系统 WebView
- 前端：React 19、Vite、TypeScript（眼镜端）与 `hls.js`

## 首次安装

先安装 Unity Hub、Unity `2022.3.62f3c1`，并为该版本添加以下模块：

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

然后克隆项目并安装官方 XR 依赖：

```bash
git clone git@github.com:buggzd/JellyfinForRayneo.git
cd JellyfinForRayneo
./scripts/install-rayneo-sdk.sh
./scripts/install-libvlc-android.sh
npm --prefix GlassesUI ci
npm --prefix CompanionUI ci
npm --prefix GlassesUI run build
npm --prefix CompanionUI run build
```

RayNeo 二进制 SDK 不进入本仓库。安装脚本从 RayNeo 官方地址下载并在解压前校验：

| 依赖 | 版本 | MD5 |
| --- | --- | --- |
| RayNeo Air SDK | 1.0.3 | `0ae0fb9de5dffae6cb0344535e20c454` |
| Cardboard XR Plugin | 1.0.3 | `fddf7e51544a4e43201f90c499fef428` |

兼容容器硬件优先与软件解码使用 LGPL-2.1-or-later 的 `VideoLAN.LibVLC.Android 3.7.0-beta`（LibVLC 3.x ABI、Android 15 兼容的 16 KB ELF 对齐）。安装脚本会校验 NuGet 包 SHA-256 `7b36d95f3bfe928d89b1d1cffb6b029e45a3379c125db89cdf2c8d8a20a32a64`，只提取 ARM64 `libvlc.so` 与 `libc++_shared.so`；二进制由 Git 忽略。Unity 2022 仍可能误报这两个库未对齐；以 `llvm-objdump` 中各 `LOAD` 段的 `align 2**14` 为准。

脚本依赖 `curl`、`unzip`、`zipinfo` 以及 `md5` 或 `md5sum`。若需要覆盖本地包，运行 `./scripts/install-rayneo-sdk.sh --force`。

两套前端的生产文件会写入 `Assets/StreamingAssets/GlassesUI/` 与 `Assets/StreamingAssets/CompanionUI/`，Android 构建直接打包这些目录。生成文件需要与源码一起提交。使用 Unity Hub 打开仓库根目录；主场景已经位于 `Assets/JellyfinForRayNeo/Scenes/Main.unity`。如需重建场景或重新写入 XR 配置，执行菜单：

```text
Jellyfin for RayNeo > Configure Project and Scene
```

## 运行与登录

应用采用两个 WebView 协作：手机的 `CompanionUI` 负责发现、凭据、Quick Connect、设置与触控；眼镜的 `GlassesUI` 负责媒体浏览、详情和播放。Unity 与 Android Activity 在两者之间同步会话、遥控命令、播放状态和显示模式。眼镜由手机供电，只有接入手机后才会出现画面，因此眼镜端没有“等待手机连接”页面。

登录会话保存在应用私有空间。手机登录后，Activity 会把会话交给 Unity；Unity 从本地恢复或完成登录时，也会把经过字段白名单和长度校验的会话同步回 Activity，并立即刷新眼镜 WebView 的启动状态。因此手机已经进入触控板时，眼镜不应再停留在“请在手机端登录”。这条桥只包含服务器与会话必需字段，不包含密码。

真机流程：

1. 将 RayNeo Air 接到配套手机并启动应用。
2. 手机会通过 Jellyfin UDP 发现协议自动搜索同一局域网的服务器；点击结果即可选择，也可以手动输入地址。反向代理子路径同样受支持，例如 `https://media.example.com/jellyfin`。
3. 推荐点击“使用 Jellyfin 快速登录”：应用显示 6 位码，可复制或直接打开服务器授权页；在已登录的 Jellyfin App/网页确认后，眼镜端会自动进入媒体库。
4. 也可以输入用户名和密码并点击“连接并在眼镜中打开”。密码只在进程内传递本次登录，不会保存。
5. 登录成功后，眼镜直接加载 Lucent 媒体库；手机可进入盲操触控板。OLED 触控面保持低亮黑色，滑动移动焦点、单击确认、双击返回，并在顶部显示当前标题、播放/缓冲/暂停状态与进度。当前焦点始终以 Lucent 的放大、亮边和光晕效果显示，包括 Android 注入的合成方向键事件。

播放期间使用独立输入域：左右滑动分别快退/快进 10 秒，可连续累计；上下滑动只唤出并移动播放器控制焦点。控制栏在继续播放约 3.2 秒后自然隐藏并清空焦点，详情页和其他后台页面在播放器退出前都不能接收点击或遥控命令。

眼镜 WebView 的浏览器开发流程：

```bash
cp .jellyfin-dev.example.json .jellyfin-dev.json
npm --prefix GlassesUI run dev
```

只在本机填写 `.jellyfin-dev.json`。Vite 开发中间件会在运行时读取它并登录 Jellyfin；该文件已被 Git 忽略，生产构建不会嵌入其中的地址、帐号或密码。浏览器打开 `http://127.0.0.1:4175/` 即可调试真实目录、详情、播放和字幕。手机模板可另行运行 `npm --prefix CompanionUI run dev`，但浏览器模式没有 Android 原生桥。

Unity Editor 流程用于验证场景、桥接和原生 UI 回退：

1. 打开 `Main` 场景并进入 Play Mode。
2. `RayNeo Phone` 模拟器窗口会自动打开，也可通过 `Jellyfin for RayNeo > Companion Simulator` 手动打开。
3. 在模拟器窗口输入 Jellyfin 地址后，可申请并测试 Quick Connect，也可以使用帐号密码；`Game View` 显示 Unity 回退界面。Android 上的最终 Lucent WebView 覆盖层应通过浏览器和真机验证。UDP 自动发现仅在 Android 手机端运行。
4. 点击 `Game View` 后，使用方向键或 `WASD` 移动焦点，`Enter`/`Space` 确认，`Esc`/`Backspace` 返回；这与手机盲操触控板发送的命令一致。
5. 按 `1` 预览铺满单目视野的 2D 镜像，按 `2` 预览双目立体虚拟屏幕；按 `F1` 显示或隐藏调试快捷键说明。鼠标仍可直接点击和拖拽滚动区。

Unity MCP 是可选的本机 Editor 工具。通过 `file:/绝对路径` 安装时，`Packages/manifest.json`、`packages-lock.json` 和部分 `ProjectSettings` 可能产生只适用于当前电脑的修改；不要把这些本机路径或无关设置提交到仓库。

局域网 HTTP 服务器需要同时放行 Android 明文网络和 Unity Player 的非安全 HTTP 选项，本项目已在 Manifest 与 Player Settings 中开启。跨公网使用时仍应配置 HTTPS。

## Android 构建

1. 在 Unity 中打开 `File > Build Settings`，选择 `Android` 并执行 `Switch Platform`。
2. 确认已运行两个依赖安装脚本，并执行过眼镜端类型检查及两套前端生产构建。
3. 确认主场景已勾选。项目已预设 ARM64、IL2CPP、最低 API 26、自定义 Manifest、Gradle 模板和 Android XR Loader。
4. 点击 `Build` 生成 APK，或 `Build And Run` 安装到 RayNeo 配套设备。

默认输出路径为 `Builds/Android/JellyfinForRayNeo.apk`（构建产物已被 Git 忽略），可通过 ADB 侧载：

```bash
adb install -r Builds/Android/JellyfinForRayNeo.apk
```

项目按 RayNeo Air SDK `1.0.3` 的兼容要求固定为 target SDK 29，适合向配套设备侧载，但不满足当前 Google Play 的 target SDK 上架要求。正式分发前还需要在 Unity Player Settings 中配置自有 keystore；未配置时 Unity 会使用 Android Debug 证书签名。

启动 Activity 为 `com.jellyfinforrayneo.companion.JellyfinRayNeoActivity`，它继承 RayNeo 文档要求的 `UnityXRSupportActivity`：手机窗口承载 `CompanionUI`，外接 `UnityPresentation` 上方承载 `GlassesUI`；原生 Android 布局只作为 WebView 加载失败时的回退。最终发布前，应在目标 RayNeo Air 型号和配套手机上验证登录、触摸遥控、双眼显示、HLS 转码、字幕和长时间播放。

## 双显示端调试

Editor 中无法真实创建 Android 外接显示器的 `Presentation` 或 Android WebView，因此用两个窗口替代：`RayNeo Phone` 模拟手机，`Game View` 验证 Unity 场景和显示控制器；Lucent 页面本身在 Vite 浏览器中调试。登录桥、状态切换和密码清理逻辑与 Android 真机共用。

真机的 `Mirror2D` 模式让一个 WebView 铺满外接显示帧，由眼镜硬件向双眼显示同一画面。`StereoVirtualScreen` 模式只创建一个播放器 WebView，把它按单眼宽度布局，再将同一 Android 渲染结果分别绘制到 SBS 左右半帧；不会启动第二个视频、第二路音频或第二组 Jellyfin 播放上报。2D/3D 硬件切换期间，`displayModeTransitioning` 会暂时隐藏 WebView，让 Unity 黑帧遮住中间状态；切换确认或安全回退结束后必须重新显示 WebView。`displayModeApplied` 只表示用户所选模式得到硬件确认，不能用于长期控制可见性。若切换结束后眼镜仍稳定显示 Unity `Main` 场景，应按眼镜 WebView 挂载故障处理。

若手机已经显示触控板、眼镜却仍提示登录，优先检查 Activity 私有首选项中是否存在 `session_json`，以及眼镜 WebView bootstrap 的 `session` 是否非空；调试时只检查字段是否存在，不要打印完整 JSON 或 token。Unity 成功恢复 PlayerPrefs 会话后应自动补写 Activity 会话并推送新 bootstrap，无需重新输入密码。

在 macOS 上首次连接局域网 Jellyfin 前，还要到 `系统设置 > 隐私与安全性 > 本地网络` 允许 Unity 访问本地网络。若 curl 可以访问服务器、Unity 却报告 `Cannot connect to destination host`，可用以下命令确认是否被系统权限拦截：

```bash
/usr/bin/log show --last 5m --style compact \
  --predicate 'process == "Unity" AND eventMessage CONTAINS[c] "Local network prohibited"'
```

真机可同时观察手机和眼镜显示：

```bash
adb devices
scrcpy --list-displays
scrcpy --display-id=0 --window-title="Phone"
scrcpy --display-id=<RayNeo-display-id> --window-title="RayNeo Air"
```

如果外接显示 ID 不明确，可检查 Android 显示拓扑：

```bash
adb shell dumpsys display | rg 'DisplayDeviceInfo|displayId|FLAG_PRESENTATION'
```

不同手机系统或 scrcpy 版本可能不允许捕获外接 `Presentation`；此时保留手机 scrcpy 窗口，并直接观察眼镜画面。开发包已启用 WebView 调试，可通过 Chromium DevTools 检查两端页面；Unity Profiler 用于检查外接显示帧与内存，`adb logcat` 用于定位 Activity、网络和 RayNeo SDK 问题。

开发机上运行 Jellyfin 时，可通过 USB 端口反向映射让手机访问：

```bash
adb reverse tcp:8096 tcp:8096
```

随后在手机登录页使用 `http://127.0.0.1:8096`。需要脱离 USB 时，可使用 Android 开发者选项中的无线调试；手机、开发机和 Jellyfin 服务器应处于可互访网络。

## 测试

先验证并构建两套 WebView 前端：

```bash
npm --prefix GlassesUI run check
npm --prefix GlassesUI run build
npm --prefix CompanionUI run build
```

EditMode 测试覆盖 URL、认证响应、浏览查询、媒体元数据、会话、字幕解析和播放设备配置；PlayMode 测试会实际加载主场景并检查消息桥、登录回退、分页网格、详情分区、焦点与 Air 3S 双目显示模式。

```bash
/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults /tmp/jellyfin-rayneo-editmode.xml \
  -logFile /tmp/jellyfin-rayneo-editmode.log

/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testResults /tmp/jellyfin-rayneo-playmode.xml \
  -logFile /tmp/jellyfin-rayneo-playmode.log
```

当前验证结果：眼镜端 TypeScript 检查与两套 Vite 生产构建通过，EditMode `73/73`、PlayMode `34/34`，Android ARM64 IL2CPP 开发 APK 构建并安装成功；浏览器连接真实 Jellyfin 后已人工确认播放与文字字幕显示。保留已有 Unity 登录数据、但 Activity 原生会话为空的升级场景已在 Android 真机验证：启动后 Activity 自动获得白名单会话，眼镜 bootstrap 含完整必需字段且不含密码，Lucent 目录直接出现而不再显示登录提示。两端 WebView 调试通道也已确认手机发出的遥控命令会迁移唯一空间焦点，旧标记被清除，新控件显示放大、亮边和光晕；上、下、左、右、确认和返回六类命令均可抵达眼镜 DOM。外接显示的 1920×1080 真机截图已确认 Lucent `GlassesUI` 覆盖 Unity 场景，并能在 RayNeo SDK 模式切换失败后以 `Mirror2D` 安全回退继续显示。包名为 `com.jellyfinforrayneo.client`，min SDK 26、target SDK 29。SBS 双眼观感仍需佩戴 RayNeo Air 验收。

## 播放降级策略

1. Android 宿主通过 `MediaCodecList` 收集硬件视频解码器；API 29+ 使用 `isHardwareAccelerated`，旧系统排除已知软件实现。`GlassesUI` 再与 `HTMLVideoElement.canPlayType` 的结果取交集，并用该结果生成本机专属的 Jellyfin `DeviceProfile`。
2. 符合硬解集合、浏览器容器、分辨率和位深约束的媒体优先使用 HTML `<video>` 直接播放。H.264 与 VP8 限 8-bit，HEVC、VP9 与 AV1 限 10-bit；播放器顶部显示“直接播放”及实际编码信息。
3. 仅有软件解码器、能力无法确认、容器不兼容或超出限制的组合使用 Jellyfin H.264/AAC HLS。浏览器支持 MSE 时，`hls.js` 只负责下载、解复用和送入 MSE，压缩视频仍由 Chromium 调用 Android `MediaCodec` 解码；致命媒体错误只尝试恢复一次。
4. 直放在运行中失败时保留当前位置并切换到服务器返回的 HLS 备用计划，不循环重试失败路径。
5. 音轨或字幕切换会停止旧播放会话、保留当前位置并重新协商。可外置的文字字幕统一请求为 WebVTT，由 React 字幕层按当前时间自绘；必须烧录的位图字幕随转码视频输出。

直放协商上限为 120 Mbps；服务器回退限制为 24 Mbps、双声道。Activity 的 `hardwareAccelerated` 与 WebView 的硬件 Layer 保证 GPU 合成，但它们不是视频硬解开关；Android WebView 没有公开 API 强制指定某个解码器，最终组件由 Chromium/MediaCodec 选择。实际可用路径仍取决于 System WebView、设备解码器和 Jellyfin FFmpeg 配置。Unity/LibVLC 播放代码继续保留为原生回退，但 Lucent WebView 是当前眼镜端主路径。

## 代码结构

```text
GlassesUI/                    # 眼镜端 React/TypeScript、Jellyfin 客户端与播放器
CompanionUI/                  # 手机端 React 登录、设置和 OLED 触控板

Assets/JellyfinForRayNeo/
├── Editor/                    # Unity 项目配置与手机伴侣模拟器
├── Runtime/Api/               # 原生回退使用的 Jellyfin API 与模型
├── Runtime/Companion/         # Android/Editor 登录消息桥与状态快照
├── Runtime/Core/              # 会话、持久化和任务辅助
├── Runtime/Services/          # 原生目录、图片缓存和播放历史上报
├── Runtime/UI/                # WebView Presenter 与原生 UI 回退
├── Scenes/                    # Main.unity
└── Tests/                     # EditMode 与 PlayMode 测试

Assets/StreamingAssets/
├── GlassesUI/                 # 已构建并随 APK 打包的眼镜页面
└── CompanionUI/               # 已构建并随 APK 打包的手机页面

Assets/Plugins/Android/
└── com/jellyfinforrayneo/companion/ # Activity、手机 WebView 与眼镜 WebView 宿主
```

## 安全与已知边界

- `.jellyfin-dev.json` 只供 Vite 开发服务器读取，已被 Git 忽略，也不会进入生产 bundle；不要把真实服务器地址、帐号、密码或 token 写进源码、文档、测试和截图。
- 密码从不落盘，也不会进入状态快照或日志；Android/Editor 桥完成派发后会清空请求对象中的密码。Quick Connect secret 仅存在于登录任务内存中，手机只接收用户可见的 6 位码。MVP 为恢复登录仍会在应用私有存储中保存 Jellyfin access token，它不等同于系统密钥库。
- Unity 向 Activity 回写会话时只序列化固定白名单字段；Activity 会再次执行必填项和长度校验、丢弃额外字段，再把同一份规范化会话提供给眼镜 bootstrap。调试会话同步只能记录成功/失败或字段存在性。
- Android Manifest 允许明文 HTTP，以支持常见局域网 Jellyfin 部署；公网环境请使用受信任的 HTTPS 反向代理。
- 图片缓存仅在内存中，默认最多 192 张、最多 4 个并发下载；选集页只加载当前季海报。
- 单次选集请求最多加载 500 集，超大型剧集库后续需要分页。
- 播放能力受 Android System WebView、设备解码器和 Jellyfin 转码配置影响；公开 API 只能筛选硬解路径，不能绝对保证 Chromium 不回退。真机播放时可用 `adb shell dumpsys media.player` 或过滤 `MediaCodec` 日志确认组件名；例如本机的 `c2.qti.avc.decoder` 是硬件组件，而 `c2.android.avc.decoder` 是软件实现。字幕自绘只覆盖文本字幕，位图字幕需要服务器烧录。
- RayNeo 官方导入指南要求将 `Active Input Handling` 设为 `Both`；SDK `1.0.3` 的 XR 输入模块同时使用旧版 `StandaloneInputModule` 与新版触摸处理，因此 Unity 会在 Android 构建时提示 `Both` 性能警告。在替换官方输入模块前不要改为单一后端。
- RayNeo SDK `1.0.3` 的 `EnvFix` 在版本未变化时会遗留每帧资产扫描回调。本项目仅在 Editor 中运行一次检查并解除该回调；升级官方 SDK 后需重新验证此兼容层。

## 第三方声明

- [Jellyfin](https://jellyfin.org/) 名称和商标归其各自权利人所有。本客户端仅使用 Jellyfin 公共 API。
- [RayNeo 开发文档](https://rayneo.gitbook.io/rayneo-devdoc/air-xi-lie/unity-kai-fa/kuai-su-kai-shi) 与 RayNeo Air SDK 归其权利人所有。SDK 的 `package.json` 标注许可证为 `FFALCON`，请在分发应用前自行确认适用条款；本仓库不重新分发其二进制文件。
- LibVLC 采用 LGPL-2.1-or-later，运行时以可替换动态库链接；版本、源码和再许可信息见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
- Google Cardboard XR Plugin 采用 Apache License 2.0；其名称和商标不包含在该许可证授权中。
- Unity、Apple TV 与 Vision Pro 是其各自权利人的商标；Apple 产品仅作为交互设计参考。

相关参考：[Jellyfin 文档](https://jellyfin.org/docs/) · [Jellyfin OpenAPI](https://api.jellyfin.org/) · [RayNeo Air Unity 快速开始](https://rayneo.gitbook.io/rayneo-devdoc/air-xi-lie/unity-kai-fa/kuai-su-kai-shi)
