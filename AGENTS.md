# Repository Guidelines

## Project Structure & Module Organization

This is a Unity `2022.3.62f3c1` project with two React/Vite frontends. `GlassesUI/` is the Lucent glasses experience and owns catalog, details, HTML video/HLS playback, and rendered WebVTT subtitles. `CompanionUI/` owns phone login, settings, and the OLED-black touchpad. Their production bundles are committed under `Assets/StreamingAssets/GlassesUI/` and `Assets/StreamingAssets/CompanionUI/`. Unity orchestration lives under `Assets/JellyfinForRayNeo/Runtime/`: `Api/`, `Core/`, `Companion/`, `Services/`, and `UI/` contain the native fallback, session bridge, and display controller. Android WebView hosts are under `Assets/Plugins/Android/com/jellyfinforrayneo/companion/`. Editor tooling is under `Assets/JellyfinForRayNeo/Editor/`; the main scene is `Scenes/Main.unity`, and tests live under `Assets/JellyfinForRayNeo/Tests/`.

## Architecture & Interaction Rules

The phone owns discovery, credentials, Quick Connect, login settings, and the OLED-black touchpad. The glasses own browsing, details, and playback. Do not add a glasses-side “waiting for phone” page: the glasses are phone-powered, so a visible glasses frame already implies a phone connection. The Android activity hosts both WebViews and Unity only coordinates sessions, commands, and RayNeo display state. Keep WebView messages validated and bounded before forwarding them to Android.

Preserve both modes managed by `Air3SDisplayController`. `Mirror2D` displays one full-width WebView frame. `StereoVirtualScreen` measures one WebView at per-eye width and replays that same render into both SBS halves; do not create two player WebViews, because that duplicates decoding, audio, and Jellyfin playback reports. During hardware mode transitions the WebView stays hidden behind Unity's black transition frame.

Directional input goes to the active glasses WebView first and uses its scoped DOM focus logic. Android-injected keyboard events must originate from `document.activeElement` (falling back to `document.body`) so they bubble with an element target, and DOM handlers must type-check `EventTarget` before calling element APIs. The native Unity fallback still uses `DirectionalFocusNavigator`. While video is active, underlying pages must remain non-interactable in either path.

## Build, Test, and Development Commands

Install ignored binary dependencies before opening Unity:

```bash
./scripts/install-rayneo-sdk.sh
./scripts/install-libvlc-android.sh
```

Install and build both embedded frontends before an Android build:

```bash
npm --prefix GlassesUI ci
npm --prefix CompanionUI ci
npm --prefix GlassesUI run check
npm --prefix GlassesUI run build
npm --prefix CompanionUI run build
```

For browser development, copy `.jellyfin-dev.example.json` to the ignored `.jellyfin-dev.json`, fill it locally, and run `npm --prefix GlassesUI run dev`. That JSON is served only by the Vite development middleware and must never enter a production bundle or Git.

Open `Main.unity`, then use `Jellyfin for RayNeo > Configure Project and Scene` only when XR configuration needs regeneration. For dual-endpoint Editor testing, pair the `RayNeo Phone` simulator with Game View. Build Android to `Builds/Android/JellyfinForRayNeo.apk`.

Run automated tests headlessly:

```bash
UNITY=/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile /tmp/editmode.log
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults /tmp/playmode.xml -logFile /tmp/playmode.log
```

## Coding Style & Naming Conventions

Use four-space indentation and Allman braces in C# and Java. Use `PascalCase` for types/public members, `camelCase` for locals/parameters, and `_camelCase` for private fields. Prefer one primary type per file. Preserve Unity `.meta` files, and keep Editor APIs under `Editor/` or `#if UNITY_EDITOR`.

## Testing Guidelines

Tests use NUnit and the Unity Test Framework. Name fixtures `*Tests` and methods after observable behavior, for example `PlayerSeek_PreservesRewindTargetUntilEngineConfirms`. Use EditMode for parsing/state logic and PlayMode for scenes, focus scopes, scrolling, layout, and lifecycle. For WebView changes, run the glasses TypeScript check, build both frontends, refresh Unity, inspect the Console, and run relevant Unity tests. Android Java changes also require an APK build because Editor compilation does not compile plugin Java.

## Commit & Pull Request Guidelines

Use Conventional Commits, such as `feat: add ...`, `fix: prevent ...`, or `docs: update ...`. Keep commits focused and commit each verified implementation stage instead of accumulating unrelated stages in one working tree. Pull requests should describe user-visible behavior, list browser/Editor/device verification, link issues, and include screenshots or recordings for UI changes.

## Security & Local Configuration

Never commit RayNeo/LibVLC binaries, credentials, LAN addresses, `.jellyfin-dev.json`, `Library/`, `Builds/`, or absolute local package paths. Do not put passwords or access tokens in docs, source, fixtures, screenshots, or generated bundles. `ProjectSettings/PackageManagerSettings.asset` and local Unity MCP configuration are machine-specific. Preserve unrelated edits to tracked scenes, packages, and project settings; `.gitignore` cannot hide changes to already tracked files. Preserve Unity `.meta` files when rebuilding `StreamingAssets`, remove Unity-generated trailing whitespace, and stage generated bundles deliberately.
