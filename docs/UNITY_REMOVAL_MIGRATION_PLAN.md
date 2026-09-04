# Jellyfin for RayNeo 去 Unity 迁移计划

> 完成状态：2026-09-04 已通过用户真机验收并执行阶段六、阶段七。旧 Unity
> `Assets/`、`Packages/`、`ProjectSettings/`、LibVLC 安装链路及本地生成缓存均已
> 从项目中移除；当前唯一生产入口为 `AndroidApp/`。

## 1. 决策与目标

本项目不再规划 Unity 3D 场景、空间物体或头部追踪交互。目标是将当前应用迁移为纯 Android 应用，由原生 Android 宿主承载手机端和眼镜端两个 WebView，并直接调用 RayNeo Android SDK 管理眼镜显示模式。

迁移完成后：

- 手机端继续使用 `CompanionUI`。
- 眼镜端继续使用 `GlassesUI`。
- Jellyfin 浏览、详情、HTML 视频/HLS 播放和 WebVTT 字幕继续由 `GlassesUI` 负责。
- Android 负责外接屏、RayNeo 2D/3D 模式、会话、遥控、生命周期和 WebView 桥接。
- 不再启动 Unity Player，不再显示 Unity `Main` 场景。
- 不再保留 Unity 原生浏览、详情和播放器作为运行时后备。

## 2. 迁移原则

1. 先证明原生 Android 可以稳定控制眼镜，再删除 Unity。
2. 迁移期间保留现有 Unity APK 作为可运行基线。
3. 不同时重写两个 React 前端。
4. 初期继续使用 Java，复用已经验证过的 Android WebView 代码，暂不引入 Compose、Flutter 或 React Native。
5. 每个完成并验证的阶段独立提交，使用 Conventional Commits。
6. 删除 Unity 必须是最后的独立阶段，确保此前任一阶段都可以回退。
7. 不记录或提交 Jellyfin 地址、密码、Token、局域网信息和开发环境 JSON。

## 3. 目标架构

```text
Android MainActivity
├── CompanionWebViewController
│   └── CompanionUI（手机登录、设置、触控遥控器）
├── SessionRepository
│   └── Android private SharedPreferences
├── JellyfinAuthenticationService
├── RemoteCommandRouter
├── RayNeoDisplayController
│   └── RayNeo AirApi（连接状态、Mirror 2D、SBS 3D）
└── GlassesPresentationController
    └── Android Presentation（眼镜外接 Display）
        ├── BlackTransitionView
        └── StereoMirrorLayout
            └── Glasses WebView
                └── GlassesUI（浏览、详情、播放、字幕）
```

### 3.1 单 WebView 双眼规则

`StereoVirtualScreen` 必须继续只使用一个眼镜 WebView：

- WebView 按单眼宽度测量。
- 同一帧绘制到 SBS 左右两半。
- 只创建一个 HTML `<video>`。
- 只进行一次视频解码、一次音频输出和一次 Jellyfin 播放状态上报。

禁止通过创建两个 WebView 实现左右眼画面。

### 3.2 会话规则

迁移后 Android 私有存储是唯一原生会话源。允许持久化并发送给眼镜端的字段只有：

- `serverUrl`
- `serverName`
- `serverVersion`
- `serverId`
- `accessToken`
- `userId`
- `userName`
- `deviceId`

密码只允许在一次认证请求的内存中短暂存在，认证完成后必须清除。任何日志、异常消息、测试输出和截图都不得包含密码或 Token。

## 4. 阶段一：原生硬件可行性验证

### 4.1 实施内容

在仓库中新建独立的 `AndroidApp/` Gradle Application。验证版本使用临时 application ID，以便和当前 Unity APK 同时安装。

只实现以下最小链路：

- 引入 `ffalcon-sdk-client-1.0.3.aar`，不依赖 Unity Player。
- 使用普通 Android Activity 初始化 `AirApi`。
- 使用 `DisplayManager` 发现 RayNeo 外接 Display。
- 使用原生 `Presentation` 在眼镜端显示一个本地测试 WebView。
- 调用 `switchTo2DMode()` 和 `switchTo3DMode()`。
- 监听眼镜连接、断开和 Activity 生命周期。

### 4.2 验收条件

- APK 中不包含 Unity Player。
- 插入眼镜后，眼镜能稳定显示本地 WebView。
- Mirror 2D 和 SBS 3D 均可切换。
- 拔出并重新插入眼镜后可以恢复显示。
- 应用暂停、恢复后状态正确。
- SDK 调用失败时能回到可见的 2D 安全状态。

### 4.3 提交

```text
feat: prove native RayNeo display hosting
```

如果本阶段无法在真机稳定通过，暂停后续迁移并保留现有 Unity 方案。

## 5. 阶段二：迁移双 WebView 宿主

### 5.1 手机端

- `MainActivity` 直接承载 Companion WebView。
- 继续从 APK assets 加载 `CompanionUI/index.html`。
- 保留硬件加速、DOM Storage、文件 assets 访问和 WebView renderer 恢复。
- 只允许应用自己的本地资源导航。

### 5.2 眼镜端

- 新增 `GlassesPresentationController` 管理原生 `Presentation`。
- 将现有 `GlassesWebViewHost` 改为挂载到 `Presentation` 的根容器。
- 去掉 `UnityPlayer`、`mUnityPlayer.getView()` 和 Unity Surface 父节点依赖。
- 保留硬件视频 Codec 枚举和 WebView 能力探测。
- 保留 `StereoMirrorLayout` 的单 WebView SBS 重放逻辑。

### 5.3 验收条件

- 手机端与眼镜端 WebView 可以同时运行。
- 眼镜 WebView 不依赖 Unity Surface 才能出现。
- 眼镜端加载失败和 renderer 崩溃后可以重建。
- WebView 始终覆盖黑色背景，不出现默认白屏。
- 眼镜端不增加“等待手机连接”页面。

### 5.4 提交

```text
feat: host companion and glasses WebViews natively
```

## 6. 阶段三：迁移会话、登录和遥控桥

### 6.1 移除 Unity 中转

将以下链路改为 Android 内部直接通信：

```text
CompanionUI → MainActivity → SessionRepository → GlassesUI
CompanionUI → RemoteCommandRouter → GlassesUI
GlassesUI → MainActivity → CompanionUI
```

移除：

- `UnitySendMessage`。
- Unity PlayerPrefs 会话副本。
- Activity 与 Unity 之间的会话轮询。
- Unity 遥控命令队列。
- Unity 播放状态转发。

### 6.2 会话行为

- 手机登录成功后，Android 验证并保存白名单会话 JSON。
- 眼镜 WebView 创建或恢复时立即接收 bootstrap state。
- 登录状态变化后主动刷新眼镜 bootstrap。
- 注销时同时清除 Android 会话和眼镜 WebView 内存状态。
- Jellyfin 返回未授权时清除会话并引导用户回到手机登录。
- 不允许已发布或已消费的会话再次循环回写。

### 6.3 遥控行为

- 方向、确认、返回和音量直接发送给当前眼镜 WebView。
- 注入键盘事件时以 `document.activeElement` 为事件源，找不到时回退到 `document.body`。
- 保持唯一 `data-spatial-focus="true"`。
- 视频播放期间禁止底层页面获得交互。
- 遥控队列必须有固定上限，避免眼镜尚未就绪时无限增长。

### 6.4 验收条件

- 手机登录后眼镜无需重复登录。
- 冷启动可以恢复已保存会话。
- 注销与未授权恢复会清除唯一会话源。
- 手机遥控可以移动唯一焦点并显示聚焦效果。
- 播放状态、标题和进度可以回传手机端。
- 日志中没有会话 JSON、密码或 Token。

### 6.5 提交

```text
feat: move session and remote bridges to Android
```

## 7. 阶段四：迁移 RayNeo 显示状态机

将 `Air3SDisplayController` 的必要行为迁移为 Java `RayNeoDisplayController`。

### 7.1 状态

必须分别维护：

- `requestedMode`
- `activeMode`
- `displayModeApplied`
- `displayModeTransitioning`

选择了某个模式不代表硬件已经成功应用该模式。

### 7.2 切换流程

```text
收到模式请求
→ 显示黑色过渡层
→ 隐藏 Glasses WebView
→ 调用 RayNeo AirApi
→ 等待硬件确认
├── 成功：应用对应布局并显示 WebView
└── 超时/失败：切回 Mirror 2D，显示 WebView，并报告失败状态
```

只有 `displayModeTransitioning == true` 时允许隐藏眼镜 WebView。模式未确认但切换已经结束时，必须显示安全的 Mirror 2D 画面。

### 7.3 生命周期

- 眼镜断开时结束切换并回到 Mirror 2D 状态。
- 眼镜重连时重新应用手机保存的请求模式。
- Activity 暂停或销毁时尽力恢复 2D。
- 失败后稳定停留在可见 Mirror 2D，不自动重试；仅允许手机显式选择、眼镜重连或生命周期恢复触发下一次尝试。

### 7.4 提交

```text
feat: manage RayNeo display modes natively
```

## 8. 阶段五：构建链路与功能对齐

### 8.1 前端产物

将两个 Vite 前端构建到 Android assets：

```text
GlassesUI/dist   → AndroidApp/app/src/main/assets/GlassesUI
CompanionUI/dist → AndroidApp/app/src/main/assets/CompanionUI
```

Gradle 构建前必须确认两个 production bundle 已更新。开发用 `.jellyfin-dev.json` 不得复制到 Android assets。

### 8.2 自动检查

```bash
npm --prefix GlassesUI ci
npm --prefix CompanionUI ci
npm --prefix GlassesUI run check
npm --prefix GlassesUI run build
npm --prefix CompanionUI run build
./gradlew :app:test
./gradlew :app:assembleDebug
```

根据实现补充 Android 单元测试和 instrumentation tests，覆盖：

- 会话 JSON 白名单和长度限制。
- 遥控命令白名单和队列上限。
- Display 选择规则。
- 显示模式状态转换。
- 未授权和注销清理。
- WebView URL 导航限制。

### 8.3 真机回归矩阵

- 首次启动、冷启动和后台恢复。
- 手机登录、Quick Connect、会话恢复和注销。
- 眼镜插入、拔出和重新插入。
- Mirror 2D 和 StereoVirtualScreen。
- 模式切换成功、超时和 SDK 失败。
- 首页、详情、搜索、收藏和剧集浏览。
- HTML 视频、HLS、暂停、拖动、音轨和字幕。
- 手机遥控焦点、返回键和音量反馈。
- WebView renderer 崩溃恢复。
- 代表性视频播放时实际选中的 `MediaCodec` 组件。
- 只产生一次声音和一次 Jellyfin 播放上报。

### 8.4 提交

```text
test: verify native Android feature parity
```

## 9. 阶段六：正式切换与删除 Unity

只有前五个阶段全部通过真机验收后才能执行本阶段。

### 9.1 正式切换

- 使用正式 application ID 和签名配置。
- 确认升级安装策略和旧版会话迁移策略。
- 确认 Android APK 可以独立完成全部生产构建。

### 9.2 删除内容

- Unity `Assets/` 中的场景、C# 运行时、Editor 工具和 Unity 测试。
- `Packages/`、`ProjectSettings/` 和 Unity 专用配置。
- `ffalcon-unity-adapter`。
- `UnityXRSDKCore.dll`。
- UnityPlayer Activity、Surface 和消息桥。
- Unity 原生 UI、播放器及其 LibVLC 后备路径。
- Unity 安装脚本、构建脚本和无效 `.gitignore` 条目。

删除前应确认没有前端资源、图标、签名配置或 Android 清单仍只存在于 Unity 目录中。

### 9.3 提交

```text
refactor: remove Unity runtime
```

该提交只负责删除 Unity 和完成正式入口切换，不夹带新功能。

## 10. 阶段七：文档与维护说明

更新：

- `README.md`
- `AGENTS.md`
- Android 构建与安装命令
- 两个前端的开发和 production bundle 流程
- RayNeo 真机调试步骤
- 会话与 WebView 消息协议
- 2D/3D 模式状态机说明
- APK 发布和签名说明

提交：

```text
docs: document native Android architecture
```

## 11. 最终完成标准

满足以下全部条件后，去 Unity 迁移才算完成：

- 构建系统不再安装或调用 Unity。
- APK 不包含 UnityPlayer、Unity 场景或 Unity 原生库。
- 手机端和眼镜端两个 WebView 均由 Android 直接管理。
- 眼镜端稳态画面永远不是 Unity Main 场景。
- 2D/3D 切换和失败回退在真机稳定工作。
- StereoVirtualScreen 只使用一个 WebView 和一个视频解码实例。
- 会话只有一个原生持久化来源。
- 登录、浏览、播放、字幕、音轨和遥控功能与迁移前对齐。
- 生产包不包含开发服务器配置、密码、Token 或局域网地址。
- README 和 AGENTS.md 已切换到纯 Android 项目说明。

## 12. 预期收益

- 消除 Unity 启动和 Main 场景闪现。
- 显著降低 APK 体积、运行内存和启动耗时。
- 删除重复的 Jellyfin API、播放器和会话实现。
- 减少 Android、Unity、手机 WebView、眼镜 WebView 四方同步问题。
- 将运行时状态收敛为 Android 与两个 WebView，降低后续维护成本。
