# Jellyfin for RayNeo 开发和构建指南

这份文档集中记录源码构建、前端调试、发布签名和验证流程。日常安装与使用见 [使用指南](USER_GUIDE.md)；修改会话、WebView 桥、播放、诊断、遥控或显示模式前，请先阅读 [Android 架构说明](ANDROID_ARCHITECTURE.md)。

## 技术基线

| 项目 | 当前配置 |
| --- | --- |
| Android Gradle Plugin / Gradle | 8.7.3 / 8.10.2 |
| Java | Java 11 源码，使用 JDK 17+ 构建 |
| Android SDK | compile SDK 35，build tools 34.0.0 |
| Android 兼容范围 | min SDK 26，target SDK 29 |
| Application ID | `com.jellyfinforrayneo.client` |
| ABI | `arm64-v8a` |
| RayNeo SDK | Air Android SDK 1.0.3 |
| 嵌入式前端 | React 19、Vite、TypeScript（眼镜端）与 `hls.js` |

`targetSdk 29` 是 RayNeo Air SDK 1.0.3 的兼容选择，适合配套设备侧载，但不满足当前 Google Play 的 target SDK 要求。

## 仓库结构

```text
AndroidApp/                         # 原生 Android Gradle application
└── app/src/
    ├── main/java/.../client/       # Activity、WebView、会话、显示与桥接
    ├── main/assets/                # 两套已构建的 production bundle
    └── test/java/.../client/       # JVM 单元测试
GlassesUI/                          # 眼镜 React/TypeScript 客户端与播放器
CompanionUI/                        # 手机 React 登录、设置与触控板
docs/                               # 使用、架构、路线图和复现规格
scripts/install-rayneo-sdk.sh       # 下载、校验并安装 RayNeo AAR
scripts/build-android.sh            # 可复现构建入口
scripts/verify-no-unity.sh          # 源码与 APK 边界检查
```

运行时是一个原生 Android 应用和两个本地 React/Vite 前端。生产 bundle 会生成到：

```text
AndroidApp/app/src/main/assets/GlassesUI/
AndroidApp/app/src/main/assets/CompanionUI/
```

这两个目录中的生产资源随源码提交。修改前端后，应有意检查并提交对应 bundle 的变化。

## 环境准备

安装以下工具：

- JDK 17 或更高版本；
- Android SDK platform 35、build tools 34.0.0 和 platform-tools；
- Node.js 与 npm；
- `curl`、`unzip`、`zipinfo`、`rg`、`strings`；
- `md5` 或 `md5sum`，以及 `shasum` 或 `sha256sum`。

通过 `ANDROID_HOME` 或 `ANDROID_SDK_ROOT` 指向 Android SDK。也可以创建被 Git 忽略的 `AndroidApp/local.properties`：

```properties
sdk.dir=/path/to/Android/sdk
```

如果想先单独安装依赖，可以运行：

```bash
./scripts/install-rayneo-sdk.sh
npm --prefix GlassesUI ci
npm --prefix CompanionUI ci
```

RayNeo 二进制不会进入仓库。安装脚本从官方地址下载 SDK 归档，校验归档 MD5：

```text
0ae0fb9de5dffae6cb0344535e20c454
```

脚本只安装 SHA-256 匹配以下值的 `ffalcon-sdk-client-1.0.3.aar`：

```text
505551d383db80d7852612e67f9158d4c67382304d22619c796abdc0365f15b6
```

如需重新下载并覆盖已存在的本地副本，运行：

```bash
./scripts/install-rayneo-sdk.sh --force
```

## 构建 Android 应用

日常 Debug 验证与构建：

```bash
./scripts/build-android.sh debug
```

其他范围：

```bash
./scripts/build-android.sh release
./scripts/build-android.sh all
```

构建脚本会依次：

1. 安装经过校验的 RayNeo AAR；
2. 对两个前端运行 `npm ci`；
3. 检查眼镜端 TypeScript；
4. 生成两套 production bundle；
5. 运行 JVM 测试和对应的 Android lint；
6. 组装所选 APK；
7. 检查 APK 不包含 Unity、LibVLC、开发配置或错误 ABI。

输出位置：

| 构建 | 输出 |
| --- | --- |
| Debug | `AndroidApp/app/build/outputs/apk/debug/app-debug.apk` |
| 未签名 Release | `AndroidApp/app/build/outputs/apk/release/app-release-unsigned.apk` |
| 已签名 Release | `AndroidApp/app/build/outputs/apk/release/app-release.apk` |

Debug 包自动添加 `.debug` application ID 后缀，可以与正式包并存。Release 保留正式 application ID；没有配置签名时只生成 unsigned APK。

Gradle 的 `preBuild` 会重新构建两个前端、确认 production bundle 存在，并拒绝把开发用 Jellyfin 配置打入 APK。

## 前端开发

### 眼镜端

连接真实的开发 Jellyfin 服务器时：

```bash
cp .jellyfin-dev.example.json .jellyfin-dev.json
npm --prefix GlassesUI run dev
```

只在本机填写 `.jellyfin-dev.json`，然后打开 `http://127.0.0.1:4175/`。Vite 开发中间件会在运行时读取配置；production bundle 不会读取或包含该文件。

该配置包含开发服务器和账号信息，已被 Git 忽略。不要把它的内容、真实媒体截图或请求日志提交到仓库。

常用命令：

```bash
npm --prefix GlassesUI run check
npm --prefix GlassesUI run build
```

### 手机端

独立浏览器预览：

```bash
npm --prefix CompanionUI run dev
```

浏览器中没有 Android 原生桥，因此页面使用展示数据，只能验证视觉与普通交互，不能代表真机会话、诊断或硬件状态。

生成生产资源：

```bash
npm --prefix CompanionUI run build
```

修改 WebView 或跨端交互后，不能只依赖浏览器预览；必须重新生成两套前端资源并完成 Android 验证。

### 浏览器双端联调

需要同时观察手机和眼镜交互时，在仓库根目录运行：

```bash
./scripts/dev-dual-ui.sh
```

脚本会启动两套 Vite 开发服务器和 `http://127.0.0.1:4177/` 联调页，并自动在浏览器中打开。左侧是具有可选 CSS 视口尺寸的 CompanionUI，右侧是按 1920 × 1080 渲染后等比缩放的 GlassesUI。两个 iframe 均保留自己的真实响应式布局与热更新，不是截图或重新实现的测试 UI。

联调桥只在 Vite DEV、指定 iframe 角色和本机父页面来源同时满足时安装。它模拟 Android 层的有限职责：

- 手机登录后把内存会话发布给眼镜端；
- 眼镜运行与播放状态回写手机端；
- 手机触控板的方向、确认和返回指令控制眼镜焦点；
- 显示模式、重试、退出登录和未授权清理在两端同步；
- 密码只经过本机联调服务转发，Token 只保存在当前联调页内存中。

若仓库根目录存在被 Git 忽略的 `.jellyfin-dev.json`，联调页启动后会自动建立开发会话。点击“清除会话”即可从连接页开始测试完整的手动地址、账号密码或 Quick Connect 流程；点击“读取开发会话”可重新使用该配置。联调服务只监听 `127.0.0.1`，校验 API 来源、限制消息与响应大小，并且不会打印凭据、Token 或服务器响应。

只想启动服务而不自动打开浏览器时：

```bash
RAYNEO_DUAL_UI_NO_OPEN=1 ./scripts/dev-dual-ui.sh
```

这套页面用于快速联调 WebView 消息和响应式 UI，不能模拟 RayNeo SDK、外接 Display、MediaCodec 或 Android WebView 的设备差异；上述部分仍需执行真机回归矩阵。

## Release 签名与旧版本升级

版本号与 Git 标签约束见 [版本与发布规则](VERSIONING.md)，正式发布操作和验收清单见 [Release 发布手册](RELEASE.md)。

在本机创建被 Git 忽略的 `AndroidApp/keystore.properties`：

```properties
storeFile=release.jks
storePassword=<local-only>
keyAlias=<local-only>
keyPassword=<local-only>
```

`storeFile` 相对 `AndroidApp/` 解析，也可以使用绝对路径。keystore 和属性文件都不得提交。正式发布应使用长期保存的自有签名，并在每次发布时提升 `versionCode`。

原 Unity 开发包使用 application ID `com.jellyfinforrayneo.client`、versionCode 1；当前原生版本使用相同的正式 application ID、versionCode 2。只有使用与已安装版本相同的证书签名，Android 才允许原位升级并保留 `jellyfin_companion` 私有会话。证书不一致时，应先导出必要的非敏感配置，再卸载旧包。

不要为了分发方便而使用 Debug 证书签署正式包。

## 验证

### 推荐入口

```bash
./scripts/build-android.sh debug
```

这条命令覆盖前端检查、两套 production bundle、JVM 测试、Debug lint、APK 组装和 APK 边界检查。

前端相关变更还应明确运行：

```bash
npm --prefix GlassesUI run check
npm --prefix GlassesUI run build
npm --prefix CompanionUI run build
```

需要直接运行 Gradle 时（命令仍从仓库根目录执行）：

```bash
./AndroidApp/gradlew -p AndroidApp \
  :app:testDebugUnitTest :app:lintDebug :app:assembleDebug
./AndroidApp/gradlew -p AndroidApp \
  :app:lintRelease :app:assembleRelease
```

检查指定 APK：

```bash
./scripts/verify-no-unity.sh \
  AndroidApp/app/build/outputs/apk/debug/app-debug.apk
```

这个检查会确认源码和 APK 不包含 Unity/LibVLC、开发配置、私网 IPv4、签名材料、非 ARM64 原生库或缺失的前端入口。

JVM 测试覆盖会话白名单与清理、IPv6 URL 规范化、消息边界、URL 导航、遥控队列、Display 选择、显示模式转换和固定容量诊断事件。设备侧变更还必须执行 [Android 架构说明中的真机回归矩阵](ANDROID_ARCHITECTURE.md#device-regression-matrix)。

## 真机调试

查看 Android 显示拓扑：

```bash
adb shell dumpsys display | rg 'DisplayDeviceInfo|displayId|FLAG_PRESENTATION'
```

如果设备与 scrcpy 支持外接屏捕获：

```bash
scrcpy --list-displays
scrcpy --display-id=0 --window-title="Phone"
scrcpy --display-id=<external-display-id> --window-title="RayNeo Air"
```

Debug 包开启 WebView 调试。日志只应筛选通用 Activity、WebView、RayNeo SDK 和 MediaCodec 状态；禁止打印会话 JSON、Token、密码、完整服务器地址或响应正文。

验证播放能力时，Activity/WebView 的硬件加速只说明合成路径可用，不代表 Chromium 一定选择硬件视频解码器。代表性媒体必须在真机上确认实际使用的 `MediaCodec` 组件。

## 仓库卫生

以下内容不得提交：

- 凭据、Token、Quick Connect 代码或真实账号；
- 局域网地址和 `.jellyfin-dev.json`；
- RayNeo SDK 二进制、APK、AAB；
- keystore、签名属性和私钥；
- `local.properties`、绝对 SDK 路径；
- `node_modules`、Gradle/Android 构建输出或 IDE 状态。

保留无关的工作区修改，按需审阅并提交生成的前端 bundle，使用聚焦的 Conventional Commit。
