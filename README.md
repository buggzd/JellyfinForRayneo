# Jellyfin for RayNeo Air

面向 RayNeo Air 系列眼镜的第三方 Jellyfin 客户端。项目现为纯原生 Android
应用：手机端 `CompanionUI` 和眼镜端 `GlassesUI` 均由 Android WebView 直接
承载，RayNeo Android SDK 负责外接显示与 2D/3D 模式。运行时不包含 Unity、
Cardboard 或 LibVLC。

> 本项目不是 Jellyfin 或 RayNeo 的官方产品，与这些公司不存在隶属或背书关系。

界面与功能范围参考 [Jellyfin Web 复现规格](docs/Jellyfin-Web-Reproduction-Spec.md)，
后续范围见 [功能路线图](docs/JELLYFIN_FEATURE_ROADMAP.md)。原生运行时、消息协议和
显示状态机详见 [Android 架构说明](docs/ANDROID_ARCHITECTURE.md)。

## 当前能力

- 手机端 UDP 局域网发现、Quick Connect、帐号密码登录、设置与 OLED 黑色触控板
- Jellyfin 的 IPv4、AAAA 域名和 IPv6 字面量地址，以及无 ADB 的手机端故障提示与脱敏日志分享
- Android 私有存储中的单一白名单会话，以及冷启动恢复、注销和未授权清理
- 眼镜端媒体库、继续观看、下一集、最近添加、搜索、筛选、文件夹与剧集浏览
- 电影、剧集、季和单集详情，收藏与看过状态同步
- HTML `<video>` 直放和 Jellyfin H.264/AAC HLS 回退
- 播放、暂停、拖动、快退/快进、上下集、音轨和字幕切换
- WebVTT 自绘字幕、位图字幕服务端烧录，以及播放进度上报
- 手机遥控焦点、确认、返回、音量反馈与眼镜播放状态回传
- RayNeo 外接 `Presentation` 的 Mirror 2D 与单 WebView SBS 立体虚拟屏幕
- 模式切换黑帧、硬件确认、超时/失败安全回退和 renderer 崩溃重建
- 硬件 `MediaCodec` 枚举与 WebView 能力交集，避免宣告软件解码直放

这是可运行的 MVP，尚未实现多服务器配置、离线下载和播放列表编辑。

## 技术基线

- Android Gradle Plugin `8.7.3`、Gradle `8.10.2`
- Java 11 源码；构建 JDK 17 或更高版本
- compile SDK 35、build tools 34.0.0、min SDK 26、target SDK 29
- 正式 application ID：`com.jellyfinforrayneo.client`
- ARM64；Android System WebView
- RayNeo Air Android SDK `1.0.3`
- React 19、Vite、TypeScript（眼镜端）与 `hls.js`

target SDK 29 是 RayNeo Air SDK `1.0.3` 的兼容选择，适合配套设备侧载，但不满足
当前 Google Play 的 target SDK 要求。

## 首次安装依赖

准备以下工具：

- Android SDK platform 35、build tools 34.0.0 与 platform-tools
- JDK 17+
- Node.js/npm
- `curl`、`unzip`、`zipinfo`、`rg`、`strings`，以及 `md5`/`md5sum` 和
  `shasum`/`sha256sum`

设置 `ANDROID_HOME` 或 `ANDROID_SDK_ROOT`，也可以在被 Git 忽略的
`AndroidApp/local.properties` 中设置 `sdk.dir`。然后运行：

```bash
./scripts/install-rayneo-sdk.sh
npm --prefix GlassesUI ci
npm --prefix CompanionUI ci
```

RayNeo 二进制不进入仓库。安装脚本从官方地址下载 SDK 压缩包，先校验归档 MD5
`0ae0fb9de5dffae6cb0344535e20c454`，再只安装 SHA-256 为
`505551d383db80d7852612e67f9158d4c67382304d22619c796abdc0365f15b6`
的 `ffalcon-sdk-client-1.0.3.aar`。使用 `--force` 可重新安装已存在的本地副本。

## 构建

完整 Debug 验证与构建：

```bash
./scripts/build-android.sh
```

也可以明确选择构建范围：

```bash
./scripts/build-android.sh debug
./scripts/build-android.sh release
./scripts/build-android.sh all
```

脚本会安装经过校验的 RayNeo AAR、执行两端 `npm ci`、眼镜 TypeScript 检查、
两套 production bundle 构建、JVM 测试、Android lint 和对应 APK 组装。

输出位置：

- Debug：`AndroidApp/app/build/outputs/apk/debug/app-debug.apk`
- Release：`AndroidApp/app/build/outputs/apk/release/`

Debug 包自动使用 `.debug` application ID 后缀，可以与正式包并存。Release 保留
正式 application ID；未配置签名时 Gradle 只生成 unsigned APK。

两套 Vite 构建也可单独执行：

```bash
npm --prefix GlassesUI run check
npm --prefix GlassesUI run build
npm --prefix CompanionUI run build
```

产物分别写入：

```text
AndroidApp/app/src/main/assets/GlassesUI/
AndroidApp/app/src/main/assets/CompanionUI/
```

这些生产资源随源码提交。Gradle `preBuild` 会再次确认产物存在，并拒绝把开发用
Jellyfin 配置打进 APK。

## 签名与旧版本升级

本地创建被 Git 忽略的 `AndroidApp/keystore.properties`：

```properties
storeFile=release.jks
storePassword=<local-only>
keyAlias=<local-only>
keyPassword=<local-only>
```

`storeFile` 相对 `AndroidApp/` 解析，也可以使用绝对路径。keystore 和属性文件均
不得提交。正式发布应使用长期保存的自有签名，并提升 `versionCode`。

原 Unity 开发包使用 application ID `com.jellyfinforrayneo.client`、versionCode 1，
本原生版本使用 versionCode 2。只有用与已安装版本相同的证书签名，Android 才会
原位升级并保留 `jellyfin_companion` 私有会话；否则应先导出必要配置并卸载旧包。
不要为方便分发而使用 Debug 证书。

## 安装与启动

没有 ADB 时，可将下面的 Debug APK 复制或发送到配套手机，直接在文件管理器中点开
安装；首次侧载需要按 Android 提示允许当前文件来源安装未知应用：

```text
AndroidApp/app/build/outputs/apk/debug/app-debug.apk
```

Debug 包使用独立的 `.debug` application ID，可与正式包并存。也可以连接配套手机后
通过 ADB 安装并启动：

```bash
adb install -r AndroidApp/app/build/outputs/apk/debug/app-debug.apk
adb shell am start \
  -n com.jellyfinforrayneo.client.debug/com.jellyfinforrayneo.client.MainActivity
```

手机系统若报告 `INSTALL_FAILED_USER_RESTRICTED`，请在手机开发者选项中允许当前
ADB/USB 安装请求后重试；不要通过脚本绕过系统安全设置。

真机流程：

1. 将 RayNeo Air 接到配套手机并启动应用。
2. 从同一 Wi-Fi 的发现结果选择 Jellyfin，也可手动输入 HTTP/HTTPS 地址。
3. 推荐使用 Quick Connect；也可输入用户名和密码。密码只用于本次请求，不落盘。
4. 登录成功后，Android 直接刷新眼镜 bootstrap，`GlassesUI` 进入媒体库。
5. 手机进入触控板后，滑动移动唯一空间焦点，单击确认，双击返回。

眼镜由手机供电，因此眼镜端不显示“等待手机连接”页面。

Jellyfin 的 UDP 自动发现协议使用 IPv4 广播。IPv6 服务器请手动填写带 AAAA 记录的
域名，或使用方括号包住字面量；带端口的示例为
`http://[2001:db8::20]:8096`。不带端口的裸 IPv6 会自动补方括号。带 `%wlan0`
一类 zone identifier 的链路本地地址无法在 Android WebView 中可靠复用，因此会被
拒绝；请改用全局/ULA IPv6 或域名。

手机首页现在分别显示“会话已保存”“眼镜画面已启动”和“媒体库已就绪”。会话恢复
不再被当作眼镜端连接成功；若眼镜 WebView 加载失败，首页会持续显示网络、HTTP、
响应格式或未知错误对应的安全诊断，不需要 ADB 才能看到故障阶段。可在“设置 →
诊断 → 分享诊断日志”打开系统分享面板并发送到 QQ。报告只包含版本、设备、网络
能力、眼镜状态和固定事件，不包含服务器完整地址、账号、媒体标题、快速连接码、
Token、密码或响应正文。

## 浏览器开发

眼镜端连接真实开发服务器时：

```bash
cp .jellyfin-dev.example.json .jellyfin-dev.json
npm --prefix GlassesUI run dev
```

只在本机填写 `.jellyfin-dev.json`。Vite 开发中间件运行时读取它；production
bundle 不读取也不包含该文件。打开 `http://127.0.0.1:4175/` 调试目录、详情、
播放和字幕。

手机 UI 可独立预览：

```bash
npm --prefix CompanionUI run dev
```

浏览器预览没有 Android 原生桥，因此使用展示数据，不代表真机会话或硬件状态。

## 双显示与播放

`Mirror2D` 让一个眼镜 WebView 铺满外接显示帧。`StereoVirtualScreen` 将同一个
WebView 按单眼宽度布局，再把同一帧绘制到 SBS 左右两半；不会创建第二个视频、
第二路音频或第二组 Jellyfin 播放报告。

切换 2D/3D 时，Android 只在 `displayModeTransitioning` 为真期间隐藏 WebView 并
显示黑色过渡层。收到对应硬件确认后应用布局；SDK 拒绝、异常或 1.5 秒超时都会
结束过渡、恢复可见 Mirror 2D，且不会自动重试。只有再次选择模式、眼镜重连或
应用恢复到前台时才会发起新尝试，避免缺失 SDK 回调造成周期黑屏。

播放能力流程：

1. Android 枚举硬件视频解码器。
2. `GlassesUI` 与 `HTMLVideoElement.canPlayType` 求交集并生成 Jellyfin
   `DeviceProfile`。
3. 符合容器、3840×2160、120 Mbps 与位深限制的源优先直放。
4. H.264/VP8 限 8-bit；HEVC/VP9/AV1 限 10-bit。
5. 未知、仅软件、不兼容或超限组合走 24 Mbps、双声道 H.264/AAC HLS。

`hls.js` 负责下载、解复用并送入 MSE，不负责视频解码。Activity/WebView 的硬件
加速用于合成，也不等于强制硬解；真机仍需确认 Chromium 实际选中的
`MediaCodec` 组件。

## 调试与测试

快速检查 Android 显示拓扑：

```bash
adb shell dumpsys display | rg 'DisplayDeviceInfo|displayId|FLAG_PRESENTATION'
```

若设备与 scrcpy 支持外接屏捕获：

```bash
scrcpy --list-displays
scrcpy --display-id=0 --window-title="Phone"
scrcpy --display-id=<external-display-id> --window-title="RayNeo Air"
```

Debug 包开启 WebView 调试。日志只应过滤通用 Activity、WebView、RayNeo SDK 和
MediaCodec 状态，禁止打印会话 JSON、Token 或密码。

直接运行 Gradle 验证：

```bash
cd AndroidApp
./gradlew :app:testDebugUnitTest :app:lintDebug :app:assembleDebug
./gradlew :app:lintRelease :app:assembleRelease
```

检查 APK 不含 Unity/LibVLC、开发配置、私网地址或非 ARM64 原生库：

```bash
./scripts/verify-no-unity.sh \
  AndroidApp/app/build/outputs/apk/debug/app-debug.apk
```

JVM 测试覆盖会话白名单与清理、IPv6 URL 规范化、消息边界、URL 导航、遥控队列、
Display 选择、显示模式转换和固定容量诊断事件。最终设备验收还必须覆盖首次/冷启动、会话恢复、登录/注销、眼镜
插拔、2D/3D 成功与失败、浏览/播放/字幕/音轨、唯一焦点、renderer 恢复、实际
MediaCodec、单路声音和单次播放上报。

## 代码结构

```text
AndroidApp/                         # 原生 Android Gradle application
└── app/src/
    ├── main/java/.../client/       # Activity、WebView、会话、显示与桥接
    ├── main/assets/                # 两套已构建的 production bundle
    └── test/java/.../client/       # JVM 单元测试
GlassesUI/                          # 眼镜 React/TypeScript 客户端与播放器
CompanionUI/                        # 手机 React 登录、设置与触控板
docs/ANDROID_ARCHITECTURE.md        # 状态机和消息边界
scripts/install-rayneo-sdk.sh       # 校验并安装唯一所需 AAR
scripts/build-android.sh             # 可复现构建入口
scripts/verify-no-unity.sh           # 源码与 APK 瘦身检查
```

## 安全边界

- 会话只允许固定八字段；手机状态不返回 Token，密码不持久化。
- SharedPreferences 属于应用私有存储，但不是 Android Keystore；设备备份已关闭。
- 两个 WebView 只允许自己的本地 asset 主导航，桥输入均有类型/长度/白名单边界。
- Manifest 允许明文 HTTP，以兼容局域网 Jellyfin；公网应使用受信任的 HTTPS。
- `.jellyfin-dev.json`、AAR、APK、keystore、本地 SDK 路径和局域网信息均被忽略。
- Android 公开能力只能筛选硬解候选，不能保证 Chromium 绝不回退到软件组件。

## 第三方声明

Jellyfin 名称和商标归其权利人所有，本客户端只使用其公共 API。RayNeo Air SDK
及开发文档归其权利人所有；分发前请自行确认 SDK 条款。其他依赖与许可证见
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

相关参考：[Jellyfin 文档](https://jellyfin.org/docs/) ·
[Jellyfin OpenAPI](https://api.jellyfin.org/) ·
[RayNeo Air 开发文档](https://rayneo.gitbook.io/rayneo-devdoc/)
