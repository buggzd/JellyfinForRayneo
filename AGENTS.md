# Repository Guidelines

## Project Structure & Module Organization

This is a Unity `2022.3.62f3c1` project. Product code is under `Assets/JellyfinForRayNeo/Runtime/`: `Api/` contains Jellyfin HTTP models and clients, `Core/` owns sessions and utilities, `Companion/` bridges phone state and commands, `Services/` assembles catalog and playback behavior, and `UI/` builds the glasses interface. Editor tooling is under `Assets/JellyfinForRayNeo/Editor/`; the main scene is `Scenes/Main.unity`. Tests live under `Assets/JellyfinForRayNeo/Tests/`. The native phone companion and Android templates are under `Assets/Plugins/Android/`.

## Architecture & Interaction Rules

The phone owns discovery, credentials, Quick Connect, and the OLED-black touchpad. The glasses own browsing, details, and playback. Route directional input through a scoped `DirectionalFocusNavigator`; while video is active, underlying pages must remain non-interactable. Preserve both display modes managed by `Air3SDisplayController`: full per-eye 2D mirror and stereo virtual screen.

## Build, Test, and Development Commands

Install ignored binary dependencies before opening Unity:

```bash
./scripts/install-rayneo-sdk.sh
./scripts/install-libvlc-android.sh
```

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

Tests use NUnit and the Unity Test Framework. Name fixtures `*Tests` and methods after observable behavior, for example `PlayerSeek_PreservesRewindTargetUntilEngineConfirms`. Use EditMode for parsing/state logic and PlayMode for scenes, focus scopes, scrolling, layout, and lifecycle. Run both suites before committing interaction changes.

## Commit & Pull Request Guidelines

Use Conventional Commits, such as `feat: add ...`, `fix: prevent ...`, or `docs: update ...`. Keep commits focused. Pull requests should describe user-visible behavior, list Editor/device verification, link issues, and include screenshots or recordings for UI changes.

## Security & Local Configuration

Never commit RayNeo/LibVLC binaries, credentials, LAN addresses, `Library/`, `Builds/`, or absolute local package paths. `ProjectSettings/PackageManagerSettings.asset` and local Unity MCP configuration are machine-specific. Preserve unrelated edits to tracked package and project settings; `.gitignore` cannot hide changes to already tracked files.
