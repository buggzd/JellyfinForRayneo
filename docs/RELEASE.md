# Release 发布手册

本文档是正式版本的操作清单。版本号和标签的强制规则见[版本与发布规则](VERSIONING.md)，构建环境见[开发和构建指南](DEVELOPMENT.md)。

## 正式发布身份

- Android application ID：`com.jellyfinforrayneo.client`
- 首个正式版本：`v0.2.0`，`versionCode=2`
- 正式签名证书 SHA-256：`71:28:B1:AE:A0:7F:26:9F:15:40:2B:9C:DC:4D:5D:D6:80:5D:79:AC:C1:EF:E6:B7:F9:85:FD:2C:AF:AC:C4:75`

证书指纹和 alias 不是秘密，可以用于核对发布身份。keystore、私钥和密码必须保密；后续 APK 必须继续使用同一私钥，才能覆盖升级 `v0.2.0` 及之后的安装。

## 发布前准备

1. 确认长期 keystore 在仓库外至少有两份受保护的备份，并另外保存 alias、keystore 密码和私钥密码。
2. 确认仓库的 GitHub Actions Secrets 已配置：
   - `ANDROID_KEYSTORE_BASE64`
   - `ANDROID_KEYSTORE_PASSWORD`
   - `ANDROID_KEY_ALIAS`
   - `ANDROID_KEY_PASSWORD`
3. 使用 JDK 17+、Node.js 和 Android platform 35/build tools 34.0.0。
4. 确认 `main` 已同步，且没有混入与本次发布无关的修改。

GitHub Secrets 是只写的 CI 配置，不是可下载的备份。不要仅依赖 GitHub 保存签名材料。

## 更新版本

先编辑根目录的 `version.properties`：

```properties
versionName=<SemVer>
versionCode=<严格递增的正整数>
```

再同步两套前端元数据并校验：

```bash
npm --prefix CompanionUI version <versionName> --no-git-tag-version
npm --prefix GlassesUI version <versionName> --no-git-tag-version
./scripts/verify-version.sh
```

已经分发过的 `versionCode` 和版本标签不得复用。修复已发布版本时提升 PATCH 和 `versionCode`，不要替换原 Release 附件。

## 检查敏感文件

`.gitignore` 会过滤常见环境文件、Android 私钥容器、PEM/PK8 私钥、keystore 的 Base64 副本和构建产物。发布前仍需检查：

```bash
git status --short --ignored
git ls-files | rg -i \
  '(^|/)(\.env($|\.)|keystore\.properties$|signing\.properties$|[^/]+\.(jks|keystore|p12|pfx|pkcs12|pem|key|pk8)(\..*)?$|[^/]*(keystore|signing)[^/]*\.(base64|b64)$)' \
  | rg -v '(^|/)\.env(\.[^/]+)?\.example$' || true
```

第二条命令应没有输出。还要人工检查 staged diff，避免密码、Token、服务器地址或本机绝对路径出现在普通文本中：

```bash
git diff --cached
```

`.gitignore` 不会停止跟踪已经提交过的文件。若敏感文件曾进入提交，立即停止发布、撤销跟踪并轮换相关私钥或密码；若已推送，还必须按泄露事件处理 Git 历史，不能只增加忽略规则。

## 完整构建与提交

```bash
./scripts/build-android.sh all
git diff --exit-code
git status --short
```

构建必须通过两套前端检查与生产 bundle、JVM 测试、Debug/Release lint、APK assembly 和 no-Unity 检查。构建后 `git diff --exit-code` 必须通过，确保已提交的生产 bundle 可复现。

确认 staged 内容后，使用聚焦的 Conventional Commit 并先推送 `main`：

```bash
git add <明确的文件列表>
git diff --cached
git commit -m "chore(release): prepare v<versionName>"
git push origin main
```

## 创建发布标签

只创建指向已推送 `main` 提交的 annotated tag：

```bash
git tag -a v<versionName> -m "Release v<versionName>"
./scripts/verify-version.sh v<versionName>
git push origin v<versionName>
```

标签推送后，[Signed Android release](../.github/workflows/release.yml) 会执行以下步骤：

1. 验证 SemVer、`versionCode`、annotated tag 和 `main` 可达性；
2. 恢复临时 keystore 并构建正式签名的 ARM64 Release APK；
3. 验证生产 bundle 没有变化、APK 不包含 Unity/敏感配置，并使用 `apksigner` 验签；
4. 生成 SHA-256 文件并创建 GitHub Release。

CI 不会发布 unsigned APK，也不会回退到 Debug 证书。

## 验收 GitHub Release

确认 Actions 成功、Release 不是 Draft，并同时存在 APK 与 `.sha256`。下载后校验：

```bash
gh release download v<versionName> --repo buggzd/JellyfinForRayneo
shasum -a 256 --check JellyfinForRayneo-<versionName>-arm64-v8a.apk.sha256
${ANDROID_HOME}/build-tools/34.0.0/apksigner verify \
  --verbose --print-certs JellyfinForRayneo-<versionName>-arm64-v8a.apk
```

Linux 可将 `shasum -a 256 --check` 替换为 `sha256sum --check`。验签结果必须至少显示 v2 scheme 为 `true`、签名者数量为 1，证书 SHA-256 必须与本文记录一致。最后在目标手机完成一次安装或覆盖升级烟雾测试。

## 失败恢复

- 标签尚未推送：修复问题、重新完整验证，再创建标签。
- 标签已经推送但 Release 尚未创建：不得移动或删除标签。先在 `main` 修复 CI，再从 Actions 手动运行 `Signed Android release`，输入原标签。
- Release 已创建：不得覆盖标签或附件。提升 PATCH 与 `versionCode`，创建一个新 Release。
- keystore 私钥丢失：同包名 APK 无法覆盖现有安装。只能要求用户卸载后重装，或更换 application ID 作为另一款应用发布。

## 签名材料轮换

普通密码泄露时，应立即更新相应 GitHub Secret 和离线记录。私钥疑似泄露时不要继续发布，应先评估 Android 版本覆盖范围和密钥轮换方案；不能简单生成新证书后继续使用原包名覆盖安装。

任何签名材料变更都应由仓库所有者明确确认，并在不包含秘密值的情况下记录证书指纹、启用版本和迁移影响。
