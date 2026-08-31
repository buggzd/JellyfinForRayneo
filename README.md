# Jellyfin for RayNeo Air

面向 RayNeo Air 系列眼镜的第三方 Jellyfin 客户端原型。项目使用 Unity 构建空间海报墙，以头部姿态控制射线、手机触摸确认，交互节奏参考 Apple TV 在 Vision Pro 中的远距浏览方式。

> 本项目不是 Jellyfin、RayNeo 或 Apple 的官方产品，与这些公司不存在隶属或背书关系。

## 当前能力

- Jellyfin 服务器探测、用户名/密码登录和本地会话恢复
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

1. 打开 `Main` 场景并进入 Play Mode，或将应用安装到 RayNeo 配套手机。
2. 输入 Jellyfin 地址，例如 `http://192.168.1.20:8096`；反向代理子路径也受支持，例如 `https://media.example.com/jellyfin`。
3. 输入 Jellyfin 用户名和密码。密码仅用于本次登录，不会保存。
4. 转动手机/眼镜使射线指向控件，触摸屏幕确认；拖动可浏览海报架。

局域网 HTTP 服务器需要 Android 明文网络访问，本项目已在 Manifest 中开启。跨公网使用时应配置 HTTPS。

## Android 构建

1. 在 Unity 中打开 `File > Build Settings`，选择 `Android` 并执行 `Switch Platform`。
2. 确认已运行依赖安装脚本，且主场景已勾选。
3. 项目已预设 ARM64、IL2CPP、最低 API 26、自定义 Manifest、Gradle 模板和 Android XR Loader。
4. 点击 `Build` 生成 APK，或 `Build And Run` 安装到 RayNeo 配套设备。

启动 Activity 使用 RayNeo 文档要求的 `com.tcl.unity.unityadapter.UnityXRSupportActivity`。最终发布前，应在目标 RayNeo Air 型号和配套手机上验证登录、触摸射线、双眼显示、HLS 转码和长时间播放。

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

当前验证结果：EditMode `11/11`、PlayMode `3/3`。

## 代码结构

```text
Assets/JellyfinForRayNeo/
├── Editor/          # Unity 项目、Android XR 与主场景配置入口
├── Runtime/Api/     # Jellyfin HTTP API、模型、URL 与认证头
├── Runtime/Core/    # 会话、持久化和任务辅助
├── Runtime/Services/# 首页聚合、图片缓存和播放历史上报
├── Runtime/UI/      # 登录、海报墙、详情、选集和播放器
├── Scenes/          # Main.unity
└── Tests/           # EditMode 与 PlayMode 测试
```

## 安全与已知边界

- 密码从不落盘；MVP 为恢复登录将 Jellyfin access token 存在 Unity `PlayerPrefs` 中，它不等同于系统安全存储。
- Android Manifest 允许明文 HTTP，以支持常见局域网 Jellyfin 部署；公网环境请使用受信任的 HTTPS 反向代理。
- 图片缓存仅在内存中，默认最多 192 张、最多 4 个并发下载；选集页只加载当前季海报。
- 单次选集请求最多加载 500 集，超大型剧集库后续需要分页。
- 播放能力受 Unity Android `VideoPlayer`、设备解码器和 Jellyfin 转码配置影响。
- RayNeo SDK `1.0.3` 的编辑器环境检查窗口在 Unity 批处理模式下可能打印空引用告警；它来自官方 SDK 的 Editor 代码，不影响本项目 PlayMode 测试结果。

## 第三方声明

- [Jellyfin](https://jellyfin.org/) 名称和商标归其各自权利人所有。本客户端仅使用 Jellyfin 公共 API。
- [RayNeo 开发文档](https://rayneo.gitbook.io/rayneo-devdoc/air-xi-lie/unity-kai-fa/kuai-su-kai-shi) 与 RayNeo Air SDK 归其权利人所有。SDK 的 `package.json` 标注许可证为 `FFALCON`，请在分发应用前自行确认适用条款；本仓库不重新分发其二进制文件。
- Google Cardboard XR Plugin 采用 Apache License 2.0；其名称和商标不包含在该许可证授权中。
- Unity、Apple TV 与 Vision Pro 是其各自权利人的商标；Apple 产品仅作为交互设计参考。

相关参考：[Jellyfin 文档](https://jellyfin.org/docs/) · [Jellyfin OpenAPI](https://api.jellyfin.org/) · [RayNeo Air Unity 快速开始](https://rayneo.gitbook.io/rayneo-devdoc/air-xi-lie/unity-kai-fa/kuai-su-kai-shi)
