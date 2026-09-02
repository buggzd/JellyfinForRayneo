# Jellyfin for RayNeo Air

面向 RayNeo Air 系列眼镜的第三方 Jellyfin 客户端原型。项目使用 Unity 构建空间海报墙，以头部姿态控制射线、手机触摸确认，交互节奏参考 Apple TV 在 Vision Pro 中的远距浏览方式。

> 本项目不是 Jellyfin、RayNeo 或 Apple 的官方产品，与这些公司不存在隶属或背书关系。

## 当前能力

- Jellyfin UDP 局域网自动发现、Quick Connect 快速登录、帐号密码登录和本地会话恢复
- 手机端原生 Android 登录页；RayNeo Air 眼镜端只显示连接状态与媒体内容
- Unity Editor 手机伴侣模拟器，可与 Game View 组成双端调试环境
- 加载用户媒体库、继续观看、下一集、最新电影和最新剧集
- 按媒体库与流派生成横向海报架
- 海报、背景图和用户观看进度展示，带内存图片缓存与并发限制
- 电影/剧集详情；电视剧按季分组浏览并选择具体分集
- Jellyfin PlaybackInfo 协商：优先 Android 友好的 MP4 直放，回退至 H.264/AAC HLS 转码
- Unity `VideoPlayer` 播放、暂停、拖动进度与返回
- 向 Jellyfin 同步播放开始、进度和停止事件，用于继续观看与历史记录
- RayNeo 官方 XR Rig、双眼世界空间 UI、凝视聚焦放大、横向/纵向滚动

这是可运行的 MVP，而不是完整播放器。目前尚未实现搜索、字幕/音轨切换、多服务器管理、离线下载和安全凭据存储。

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
```

RayNeo 二进制 SDK 不进入本仓库。安装脚本从 RayNeo 官方地址下载并在解压前校验：

| 依赖 | 版本 | MD5 |
| --- | --- | --- |
| RayNeo Air SDK | 1.0.3 | `0ae0fb9de5dffae6cb0344535e20c454` |
| Cardboard XR Plugin | 1.0.3 | `fddf7e51544a4e43201f90c499fef428` |

脚本依赖 `curl`、`unzip`、`zipinfo` 以及 `md5` 或 `md5sum`。若需要覆盖本地包，运行 `./scripts/install-rayneo-sdk.sh --force`。

使用 Unity Hub 打开仓库根目录。主场景已经位于 `Assets/JellyfinForRayNeo/Scenes/Main.unity`；如需重建场景或重新写入 XR 配置，执行菜单：

```text
Jellyfin for RayNeo > Configure Project and Scene
```

## 运行与登录

应用采用两个显示端协作：配套手机运行原生 Android 登录页，负责键盘输入；RayNeo Air 运行 Unity XR 画面，负责连接等待、海报墙、详情和播放。登录成功后手机登录层自动隐藏，露出 RayNeo SDK 原有遥控界面。

真机流程：

1. 将 RayNeo Air 接到配套手机并启动应用。
2. 手机会通过 Jellyfin UDP 发现协议自动搜索同一局域网的服务器；点击结果即可选择，也可以手动输入地址。反向代理子路径同样受支持，例如 `https://media.example.com/jellyfin`。
3. 推荐点击“使用 Jellyfin 快速登录”：应用显示 6 位码，可复制或直接打开服务器授权页；在已登录的 Jellyfin App/网页确认后，眼镜端会自动进入媒体库。
4. 也可以输入用户名和密码并点击“连接并在眼镜中打开”。密码只在进程内传递本次登录，不会保存。
5. 连接成功后戴上眼镜，使用 RayNeo 头控射线与手机遥控区浏览海报墙。

Unity Editor 流程：

1. 打开 `Main` 场景并进入 Play Mode。
2. `RayNeo Phone` 模拟器窗口会自动打开，也可通过 `Jellyfin for RayNeo > Companion Simulator` 手动打开。
3. 在模拟器窗口输入 Jellyfin 地址后，可申请并测试 Quick Connect，也可以使用帐号密码；`Game View` 同时作为眼镜画面。UDP 自动发现仅在 Android 手机端运行。
4. 点击 `Game View`，按一次左 Ctrl 捕获鼠标。系统光标会隐藏并固定在窗口中心，这是相对鼠标输入的正常表现；此时观察 RayNeo 激光点的位置。
5. 移动鼠标瞄准，使用左键点击菜单；按住左键可从黑色空白处或海报区域横向/纵向拖拽。再次按左 Ctrl 或按 Esc 释放鼠标。

局域网 HTTP 服务器需要同时放行 Android 明文网络和 Unity Player 的非安全 HTTP 选项，本项目已在 Manifest 与 Player Settings 中开启。跨公网使用时仍应配置 HTTPS。

## Android 构建

1. 在 Unity 中打开 `File > Build Settings`，选择 `Android` 并执行 `Switch Platform`。
2. 确认已运行依赖安装脚本，且主场景已勾选。
3. 项目已预设 ARM64、IL2CPP、最低 API 26、自定义 Manifest、Gradle 模板和 Android XR Loader。
4. 点击 `Build` 生成 APK，或 `Build And Run` 安装到 RayNeo 配套设备。

默认输出路径为 `Builds/Android/JellyfinForRayNeo.apk`（构建产物已被 Git 忽略），可通过 ADB 侧载：

```bash
adb install -r Builds/Android/JellyfinForRayNeo.apk
```

项目按 RayNeo Air SDK `1.0.3` 的兼容要求固定为 target SDK 29，适合向配套设备侧载，但不满足当前 Google Play 的 target SDK 上架要求。正式分发前还需要在 Unity Player Settings 中配置自有 keystore；未配置时 Unity 会使用 Android Debug 证书签名。

启动 Activity 为 `com.jellyfinforrayneo.companion.JellyfinRayNeoActivity`，它继承 RayNeo 文档要求的 `UnityXRSupportActivity`：手机显示原生登录层，外接眼镜仍由 SDK 的 `UnityPresentation` 承载 Unity 画面。最终发布前，应在目标 RayNeo Air 型号和配套手机上验证登录、触摸射线、双眼显示、HLS 转码和长时间播放。

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

不同手机系统或 scrcpy 版本可能不允许捕获外接 `Presentation`；此时保留手机 scrcpy 窗口，并直接观察眼镜画面。Android Studio 的 Layout Inspector 用于检查手机原生登录层，Unity Profiler 用于检查眼镜端 Unity 帧与内存，`adb logcat` 用于定位 Activity、网络和 RayNeo SDK 问题。

开发机上运行 Jellyfin 时，可通过 USB 端口反向映射让手机访问：

```bash
adb reverse tcp:8096 tcp:8096
```

随后在手机登录页使用 `http://127.0.0.1:8096`。需要脱离 USB 时，可使用 Android 开发者选项中的无线调试；手机、开发机和 Jellyfin 服务器应处于可互访网络。

## 测试

EditMode 测试覆盖 URL、认证响应、媒体元数据、会话和播放设备配置；PlayMode 测试会实际加载主场景并检查登录 UI、世界空间 Canvas 与 RayNeo 头控射线 Rig。

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

当前验证结果：EditMode `25/25`、PlayMode `9/9`；Android ARM64 IL2CPP APK 构建成功。产物为约 43 MB，包名 `com.jellyfinforrayneo.client`，min SDK 26、target SDK 29，并使用 APK Signature Scheme v2 调试签名。

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
└── com/jellyfinforrayneo/companion/ # 手机原生登录 Activity
```

## 安全与已知边界

- 密码从不落盘，也不会进入状态快照或日志；Android/Editor 桥完成派发后会清空请求对象中的密码。Quick Connect secret 仅存在于 Unity 登录任务内存中，手机只接收用户可见的 6 位码。MVP 为恢复登录将 Jellyfin access token 存在 Unity `PlayerPrefs` 中，它不等同于系统安全存储。
- Android Manifest 允许明文 HTTP，以支持常见局域网 Jellyfin 部署；公网环境请使用受信任的 HTTPS 反向代理。
- 图片缓存仅在内存中，默认最多 192 张、最多 4 个并发下载；选集页只加载当前季海报。
- 单次选集请求最多加载 500 集，超大型剧集库后续需要分页。
- 播放能力受 Unity Android `VideoPlayer`、设备解码器和 Jellyfin 转码配置影响。
- RayNeo 官方导入指南要求将 `Active Input Handling` 设为 `Both`；SDK `1.0.3` 的 XR 输入模块同时使用旧版 `StandaloneInputModule` 与新版触摸处理，因此 Unity 会在 Android 构建时提示 `Both` 性能警告。在替换官方输入模块前不要改为单一后端。
- RayNeo SDK `1.0.3` 的编辑器环境检查窗口在 Unity 批处理模式下可能打印空引用告警；它来自官方 SDK 的 Editor 代码，不影响本项目 PlayMode 测试结果。

## 第三方声明

- [Jellyfin](https://jellyfin.org/) 名称和商标归其各自权利人所有。本客户端仅使用 Jellyfin 公共 API。
- [RayNeo 开发文档](https://rayneo.gitbook.io/rayneo-devdoc/air-xi-lie/unity-kai-fa/kuai-su-kai-shi) 与 RayNeo Air SDK 归其权利人所有。SDK 的 `package.json` 标注许可证为 `FFALCON`，请在分发应用前自行确认适用条款；本仓库不重新分发其二进制文件。
- Google Cardboard XR Plugin 采用 Apache License 2.0；其名称和商标不包含在该许可证授权中。
- Unity、Apple TV 与 Vision Pro 是其各自权利人的商标；Apple 产品仅作为交互设计参考。

相关参考：[Jellyfin 文档](https://jellyfin.org/docs/) · [Jellyfin OpenAPI](https://api.jellyfin.org/) · [RayNeo Air Unity 快速开始](https://rayneo.gitbook.io/rayneo-devdoc/air-xi-lie/unity-kai-fa/kuai-su-kai-shi)
