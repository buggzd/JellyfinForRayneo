# Jellyfin for RayNeo 使用指南

这份文档面向安装和使用应用的人。源码环境、构建参数、签名和测试命令见 [开发和构建指南](DEVELOPMENT.md)。

## 使用前准备

你需要：

- RayNeo Air 系列眼镜及其配套 Android 手机；
- 一台已经配置好媒体库和用户的 Jellyfin 服务器；
- 手机能够访问 Jellyfin 服务器，自动发现时两者应位于同一局域网；
- 自行构建的 APK。仓库当前不提供预编译安装包。

应用只构建 ARM64 版本，并依赖手机上的 Android System WebView。

## 安装

完成 Debug 构建后，APK 位于：

```text
AndroidApp/app/build/outputs/apk/debug/app-debug.apk
```

### 通过手机文件管理器安装

1. 把 APK 复制或发送到配套手机。
2. 在文件管理器中打开 APK。
3. 按 Android 提示允许当前文件来源安装未知应用，然后完成安装。

Debug 包使用 `.debug` application ID 后缀，可以和正式包同时安装。

### 通过 ADB 安装

```bash
adb install -r AndroidApp/app/build/outputs/apk/debug/app-debug.apk
adb shell am start \
  -n com.jellyfinforrayneo.client.debug/com.jellyfinforrayneo.client.MainActivity
```

如果系统返回 `INSTALL_FAILED_USER_RESTRICTED`，请在手机开发者选项中允许当前 ADB/USB 安装请求后重试。不要通过脚本绕过系统安全设置。

## 首次连接

1. 将 RayNeo Air 接到配套手机并启动应用。
2. 在手机上从发现结果中选择 Jellyfin 服务器，或手动填写服务器地址。
3. 推荐使用 Quick Connect；也可以输入用户名和密码登录。
4. 登录成功后，眼镜端会自动刷新并进入媒体库。
5. 在手机上进入触控板：滑动移动焦点，单击确认，双击返回。

眼镜由手机供电，因此眼镜端不会显示“等待手机连接”页面。手机首页会分别显示会话、眼镜画面和媒体库是否已准备就绪。

## 填写服务器地址

局域网自动发现使用 Jellyfin 的 IPv4 UDP 广播。发现不到服务器时，可以手动填写完整的 HTTP 或 HTTPS 地址，例如：

```text
http://jellyfin.example.test:8096
https://media.example.test/jellyfin
http://[2001:db8::20]:8096
```

应用支持 IPv4、具有 A/AAAA 记录的域名、带方括号的 IPv6 字面量，以及安装在子路径中的 Jellyfin。

使用 IPv6 时请注意：

- 带端口的 IPv6 必须写成 `http://[address]:port`；
- 不带端口的裸 IPv6 会自动补方括号；
- 带 `%wlan0` 一类 zone identifier 的链路本地地址无法在 Android WebView 中可靠复用，会被拒绝；
- 对 IPv6-only 服务器，请使用域名、全局 IPv6 或 ULA 地址手动连接。

公网连接应使用证书受信任的 HTTPS。应用允许明文 HTTP，是为了兼容局域网中的 Jellyfin 服务器。

## 登录与会话

- Quick Connect 是推荐登录方式，授权操作在 Jellyfin Web 中完成。
- 密码只用于本次认证请求，不会持久化。
- 选择记住登录后，会话保存在 Android 应用私有存储中，冷启动可以恢复。
- 注销、切换账号或 Jellyfin 返回未授权状态时，手机和眼镜两端的会话都会清理。

应用私有存储不是 Android Keystore，因此请把配套手机视为持有 Jellyfin 会话的可信设备。应用已经关闭系统备份，避免会话随备份迁移。

## 眼镜显示模式

手机设置中可以选择：

- **Mirror 2D**：在眼镜外接显示上呈现完整的单幅 2D 画面；
- **Stereo Virtual Screen**：把同一画面输出到 SBS 左右两半，形成双眼立体虚拟屏幕。

两种模式始终只使用一个眼镜界面、一个视频和一条音频。模式切换失败或超时时，应用会恢复到可见的安全 2D 画面，不会自动重复切换；你可以重新选择模式、重连眼镜或让应用重新回到前台后再次尝试。

## 浏览与播放

眼镜端支持媒体库首页、继续观看、下一集、最近添加、搜索、筛选、文件夹和剧集浏览。电影、剧集、季和单集详情可以显示元数据、播放能力及用户状态。

播放时可以使用手机触控板或方向键完成：

- 播放与暂停；
- 拖动进度、快退和快进；
- 切换上一集或下一集；
- 选择音轨和字幕；
- 调整音量并返回详情。

应用优先尝试 WebView 可用的直放格式；不兼容的媒体会请求 Jellyfin 生成 H.264/AAC HLS。文字字幕由眼镜端显示，位图字幕通过服务端烧录。是否能顺利回退取决于 Jellyfin 服务器的转码配置和可用资源。

## 诊断与隐私

如果眼镜画面或媒体库没有就绪，手机首页会显示网络、HTTP、响应格式或未知错误对应的安全提示。你也可以打开“设置 → 诊断 → 分享诊断日志”，通过 Android 分享面板发送报告。

诊断报告只包含应用、Android、WebView 和设备版本，网络能力，眼镜与显示状态，以及固定类型的事件。它不会包含：

- 完整服务器地址或局域网地址；
- 用户名、媒体标题或 Quick Connect 代码；
- Token、密码、会话内容或服务器响应正文；
- 任意原始异常文本。

分享前仍建议快速检查报告内容，并只发送给你信任的接收方。

## 常见问题

| 现象 | 建议 |
| --- | --- |
| 自动发现没有结果 | 确认手机和服务器处于同一局域网，或直接手动填写完整地址。IPv6-only 服务器不会出现在 UDP 发现结果中。 |
| 已登录但眼镜没有媒体库 | 查看手机首页的“眼镜画面”和“媒体库”状态，确认 WebView 网络可访问服务器，然后使用重试入口或重新连接眼镜。 |
| 3D 模式没有生效 | 应用会保留安全 2D 画面。重新选择模式、重连眼镜，或让应用重新进入前台后再试。 |
| 视频无法直放 | 确认 Android System WebView 已更新，并检查 Jellyfin 是否允许为该用户转码及服务器是否有可用转码资源。 |
| 字幕没有显示 | 尝试切换字幕轨；位图字幕需要服务端转码烧录，文字字幕需要服务器能提供 WebVTT。 |
| 安装时报用户限制 | 在手机开发者选项或安装确认界面中允许当前来源/USB 安装，然后重试。 |

如果仍无法定位问题，请附上脱敏诊断报告、应用版本、手机型号、WebView 版本、复现步骤和预期行为提交 Issue；不要上传 Token、密码、真实服务器地址或媒体隐私信息。

## 当前限制

- 仅面向 RayNeo Air 系列配套设备与 ARM64 Android 环境；
- 当前 `targetSdk 29` 不满足 Google Play 的上架要求，以侧载为主；
- 暂不支持多服务器配置、离线下载和播放列表编辑；
- 没有 LibVLC 或其他原生备用播放器，不兼容媒体依赖 Jellyfin 转码；
- 自动发现只覆盖 IPv4 UDP，IPv6 端点需要手动输入。

后续范围与优先级见 [功能路线图](JELLYFIN_FEATURE_ROADMAP.md)。
