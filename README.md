# Jellyfin for RayNeo Air

面向 RayNeo Air 系列眼镜的第三方 Jellyfin 客户端原型。项目使用 Unity 构建空间海报墙；Air 3S 不依赖头部追踪，手机在佩戴眼镜后切换为盲操触控板，以上下左右焦点导航和确认/返回完成交互，节奏参考 Apple TV 在 Vision Pro 中的远距浏览方式。

> 本项目不是 Jellyfin、RayNeo 或 Apple 的官方产品，与这些公司不存在隶属或背书关系。

界面和功能取舍以 [Jellyfin Web 复现规格](docs/Jellyfin-Web-Reproduction-Spec.md) 为对照，并按 RayNeo Air 的双显示与盲操场景调整；后续范围见 [功能路线图](docs/JELLYFIN_FEATURE_ROADMAP.md)。

## 当前能力

- Jellyfin UDP 局域网自动发现、Quick Connect 快速登录、帐号密码登录和本地会话恢复
- 手机端原生 Android 配置页；连接成功后切换为 OLED 纯黑盲操触控板，RayNeo Air 眼镜端显示媒体内容
- Unity Editor 手机伴侣模拟器，可与 Game View 组成双端调试环境
- 加载“我的媒体”、继续观看、下一集和各媒体库最近添加内容
- 电影/剧集海报网格、收藏、搜索、服务端分页、排序与观看状态筛选
- 通用文件夹逐层浏览，支持网课库中目录和普通视频混排，并保留返回栈
- 海报、背景图和用户观看进度展示，带内存图片缓存与并发限制
- 电影/剧集/季/单集详情；按季选集、相似内容、完整音视频/字幕轨道信息与章节直达
- 动态探测 Android `MediaCodecList`，按“系统硬解 → LibVLC MediaCodec 兼容容器硬件优先 → LibVLC 本地软解 → Jellyfin H.264/AAC HLS 转码”自动降级
- 播放、暂停、拖动进度、遥控快退/快进、独占播放焦点和音轨切换；VTT/SRT/ASS/SSA 文字字幕后台加载并本地渲染，PGS/DVD/DVB 字幕由服务器烧录
- 向 Jellyfin 同步播放开始、进度和停止事件，用于继续观看与历史记录
- RayNeo 官方双显示链路；可选单目铺满的 2D 镜像或双目立体虚拟屏幕模式

这是可运行的 MVP，而不是完整播放器。目前尚未实现多服务器切换、离线下载、播放列表编辑和安全凭据存储。

## 技术基线

- Unity `2022.3.62f3c1`
- RayNeo Air SDK `1.0.3`
- RayNeo 提供的 Cardboard XR Plugin 包 `1.0.3`（包内版本 `1.17.0`）
- Jellyfin API：按 `10.10.7` 与 `12.0.0` 的公共接口设计
- Android：API 26+、ARM64、IL2CPP

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
```

RayNeo 二进制 SDK 不进入本仓库。安装脚本从 RayNeo 官方地址下载并在解压前校验：

| 依赖 | 版本 | MD5 |
| --- | --- | --- |
| RayNeo Air SDK | 1.0.3 | `0ae0fb9de5dffae6cb0344535e20c454` |
| Cardboard XR Plugin | 1.0.3 | `fddf7e51544a4e43201f90c499fef428` |

兼容容器硬件优先与软件解码使用 LGPL-2.1-or-later 的 `VideoLAN.LibVLC.Android 3.7.0-beta`（LibVLC 3.x ABI、Android 15 兼容的 16 KB ELF 对齐）。安装脚本会校验 NuGet 包 SHA-256 `7b36d95f3bfe928d89b1d1cffb6b029e45a3379c125db89cdf2c8d8a20a32a64`，只提取 ARM64 `libvlc.so` 与 `libc++_shared.so`；二进制由 Git 忽略。Unity 2022 仍可能误报这两个库未对齐；以 `llvm-objdump` 中各 `LOAD` 段的 `align 2**14` 为准。

脚本依赖 `curl`、`unzip`、`zipinfo` 以及 `md5` 或 `md5sum`。若需要覆盖本地包，运行 `./scripts/install-rayneo-sdk.sh --force`。

使用 Unity Hub 打开仓库根目录。主场景已经位于 `Assets/JellyfinForRayNeo/Scenes/Main.unity`；如需重建场景或重新写入 XR 配置，执行菜单：

```text
Jellyfin for RayNeo > Configure Project and Scene
```

## 运行与登录

应用采用两个显示端协作：配套手机运行原生 Android 配置页，负责键盘输入；RayNeo Air 运行 Unity XR 画面，负责连接等待、海报墙、详情和播放。登录成功且眼镜画面就绪后，手机配置层切换为本项目的 OLED 盲操触控板。

真机流程：

1. 将 RayNeo Air 接到配套手机并启动应用。
2. 手机会通过 Jellyfin UDP 发现协议自动搜索同一局域网的服务器；点击结果即可选择，也可以手动输入地址。反向代理子路径同样受支持，例如 `https://media.example.com/jellyfin`。
3. 推荐点击“使用 Jellyfin 快速登录”：应用显示 6 位码，可复制或直接打开服务器授权页；在已登录的 Jellyfin App/网页确认后，眼镜端会自动进入媒体库。
4. 也可以输入用户名和密码并点击“连接并在眼镜中打开”。密码只在进程内传递本次登录，不会保存。
5. 连接成功后戴上眼镜，手机自动变成盲操触控板：除中央约 4dp 的低亮反馈点外，窗口、系统栏和背景均为不透明 `#000000`。OLED 屏幕的黑色像素不会发光；上下左右滑动移动唯一焦点，单击确认，双击返回。

播放期间使用独立输入域：左右滑动分别快退/快进 10 秒，可连续累计；上下滑动只唤出并移动播放器控制焦点。控制栏在继续播放约 3.2 秒后自然隐藏并清空焦点，详情页和其他后台页面在播放器退出前都不能接收点击或遥控命令。

Unity Editor 流程：

1. 打开 `Main` 场景并进入 Play Mode。
2. `RayNeo Phone` 模拟器窗口会自动打开，也可通过 `Jellyfin for RayNeo > Companion Simulator` 手动打开。
3. 在模拟器窗口输入 Jellyfin 地址后，可申请并测试 Quick Connect，也可以使用帐号密码；`Game View` 同时作为眼镜画面。UDP 自动发现仅在 Android 手机端运行。
4. 点击 `Game View` 后，使用方向键或 `WASD` 移动焦点，`Enter`/`Space` 确认，`Esc`/`Backspace` 返回；这与手机盲操触控板发送的命令一致。
5. 按 `1` 预览铺满单目视野的 2D 镜像，按 `2` 预览双目立体虚拟屏幕；按 `F1` 显示或隐藏调试快捷键说明。鼠标仍可直接点击和拖拽滚动区。

Unity MCP 是可选的本机 Editor 工具。通过 `file:/绝对路径` 安装时，`Packages/manifest.json` 和 `packages-lock.json` 会产生仅适用于当前电脑的修改；不要把该绝对路径提交到仓库。`ProjectSettings/PackageManagerSettings.asset` 也属于本机 UI 状态，已由 Git 忽略。

局域网 HTTP 服务器需要同时放行 Android 明文网络和 Unity Player 的非安全 HTTP 选项，本项目已在 Manifest 与 Player Settings 中开启。跨公网使用时仍应配置 HTTPS。

## Android 构建

1. 在 Unity 中打开 `File > Build Settings`，选择 `Android` 并执行 `Switch Platform`。
2. 确认已运行两个依赖安装脚本，且主场景已勾选。
3. 项目已预设 ARM64、IL2CPP、最低 API 26、自定义 Manifest、Gradle 模板和 Android XR Loader。
4. 点击 `Build` 生成 APK，或 `Build And Run` 安装到 RayNeo 配套设备。

默认输出路径为 `Builds/Android/JellyfinForRayNeo.apk`（构建产物已被 Git 忽略），可通过 ADB 侧载：

```bash
adb install -r Builds/Android/JellyfinForRayNeo.apk
```

项目按 RayNeo Air SDK `1.0.3` 的兼容要求固定为 target SDK 29，适合向配套设备侧载，但不满足当前 Google Play 的 target SDK 上架要求。正式分发前还需要在 Unity Player Settings 中配置自有 keystore；未配置时 Unity 会使用 Android Debug 证书签名。

启动 Activity 为 `com.jellyfinforrayneo.companion.JellyfinRayNeoActivity`，它继承 RayNeo 文档要求的 `UnityXRSupportActivity`：手机显示原生配置/触控层，外接眼镜仍由 SDK 的 `UnityPresentation` 承载 Unity 画面。最终发布前，应在目标 RayNeo Air 型号和配套手机上验证登录、触摸遥控、双眼显示、HLS 转码和长时间播放。

## 双显示端调试

Editor 中无法真实创建 Android 外接显示器的 `Presentation`，因此用两个窗口替代：`RayNeo Phone` 模拟手机，`Game View` 模拟眼镜。登录桥、状态切换和密码清理逻辑与 Android 真机共用；只有手机 UI 的实现不同。

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

不同手机系统或 scrcpy 版本可能不允许捕获外接 `Presentation`；此时保留手机 scrcpy 窗口，并直接观察眼镜画面。Android Studio 的 Layout Inspector 用于检查手机原生配置/触控层，Unity Profiler 用于检查眼镜端 Unity 帧与内存，`adb logcat` 用于定位 Activity、网络和 RayNeo SDK 问题。

开发机上运行 Jellyfin 时，可通过 USB 端口反向映射让手机访问：

```bash
adb reverse tcp:8096 tcp:8096
```

随后在手机登录页使用 `http://127.0.0.1:8096`。需要脱离 USB 时，可使用 Android 开发者选项中的无线调试；手机、开发机和 Jellyfin 服务器应处于可互访网络。

## 测试

EditMode 测试覆盖 URL、认证响应、浏览查询、媒体元数据、会话和播放设备配置；PlayMode 测试会实际加载主场景并检查登录 UI、分页网格、详情分区、世界空间 Canvas 与 Air 3S 双目显示模式。

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

当前验证结果：EditMode `53/53`、PlayMode `18/18`；Android ARM64 IL2CPP APK 构建成功并通过 APK Signature Scheme v2 校验。含本地解码库的开发 APK 约 54 MiB，包名 `com.jellyfinforrayneo.client`，min SDK 26、target SDK 29。

## 播放降级策略

1. 首先根据手机的硬件解码器、容器、分辨率、位深和所选音轨判断能否直放；Unity Android `VideoPlayer` 使用系统 `MediaCodec`。
2. 对 MKV 等 Unity 不直接接受、但视频编码受硬件支持的容器，LibVLC 以 `avcodec-hw=any` 再尝试 MediaCodec 硬件优先解码。
3. 两条硬件路径失败后自动保留进度，切换 LibVLC `avcodec-hw=none` 强制软件解码；覆盖 VP9、DTS 等更多格式，但耗电和 CPU 占用更高。
4. 本地路径均失败时重新请求 PlaybackInfo，禁用视频/音频流复制，强制服务器输出双声道 H.264/AAC HLS。服务端转码能力取决于 Jellyfin 的 FFmpeg 配置。

直放协商允许最高 120 Mbps 和 8 声道，让本地播放器负责下混；服务器转码仍限制为 20 Mbps、双声道，避免回退路径生成过重的流。

播放器顶部会显示当前路径，并提供音轨和字幕菜单。切轨会保留当前位置并重新协商；任何一级在运行中失败都会从下一层继续，不会重试已经失败的层级。

## 代码结构

```text
Assets/JellyfinForRayNeo/
├── Editor/          # Unity 项目配置与手机伴侣模拟器
├── Runtime/Api/     # Jellyfin HTTP API、模型、URL 与认证头
├── Runtime/Companion/ # Android/Editor 登录消息桥与状态快照
├── Runtime/Core/    # 会话、持久化和任务辅助
├── Runtime/Services/ # 首页聚合、图片缓存和播放历史上报
├── Runtime/UI/      # 登录、海报墙、详情、选集和播放器
├── Scenes/          # Main.unity
└── Tests/           # EditMode 与 PlayMode 测试

Assets/Plugins/Android/
└── com/jellyfinforrayneo/companion/ # 手机原生配置与触控 Activity
```

## 安全与已知边界

- 密码从不落盘，也不会进入状态快照或日志；Android/Editor 桥完成派发后会清空请求对象中的密码。Quick Connect secret 仅存在于 Unity 登录任务内存中，手机只接收用户可见的 6 位码。MVP 为恢复登录将 Jellyfin access token 存在 Unity `PlayerPrefs` 中，它不等同于系统安全存储。
- Android Manifest 允许明文 HTTP，以支持常见局域网 Jellyfin 部署；公网环境请使用受信任的 HTTPS 反向代理。
- 图片缓存仅在内存中，默认最多 192 张、最多 4 个并发下载；选集页只加载当前季海报。
- 单次选集请求最多加载 500 集，超大型剧集库后续需要分页。
- 播放能力受 Unity Android `VideoPlayer`、设备解码器和 Jellyfin 转码配置影响。
- RayNeo 官方导入指南要求将 `Active Input Handling` 设为 `Both`；SDK `1.0.3` 的 XR 输入模块同时使用旧版 `StandaloneInputModule` 与新版触摸处理，因此 Unity 会在 Android 构建时提示 `Both` 性能警告。在替换官方输入模块前不要改为单一后端。
- RayNeo SDK `1.0.3` 的 `EnvFix` 在版本未变化时会遗留每帧资产扫描回调。本项目仅在 Editor 中运行一次检查并解除该回调；升级官方 SDK 后需重新验证此兼容层。

## 第三方声明

- [Jellyfin](https://jellyfin.org/) 名称和商标归其各自权利人所有。本客户端仅使用 Jellyfin 公共 API。
- [RayNeo 开发文档](https://rayneo.gitbook.io/rayneo-devdoc/air-xi-lie/unity-kai-fa/kuai-su-kai-shi) 与 RayNeo Air SDK 归其权利人所有。SDK 的 `package.json` 标注许可证为 `FFALCON`，请在分发应用前自行确认适用条款；本仓库不重新分发其二进制文件。
- LibVLC 采用 LGPL-2.1-or-later，运行时以可替换动态库链接；版本、源码和再许可信息见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
- Google Cardboard XR Plugin 采用 Apache License 2.0；其名称和商标不包含在该许可证授权中。
- Unity、Apple TV 与 Vision Pro 是其各自权利人的商标；Apple 产品仅作为交互设计参考。

相关参考：[Jellyfin 文档](https://jellyfin.org/docs/) · [Jellyfin OpenAPI](https://api.jellyfin.org/) · [RayNeo Air Unity 快速开始](https://rayneo.gitbook.io/rayneo-devdoc/air-xi-lie/unity-kai-fa/kuai-su-kai-shi)
