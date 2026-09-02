# Repository Guidelines

## Project Structure & Module Organization

This is a Unity `2022.3.62f3c1` project. Product code lives under `Assets/JellyfinForRayNeo/Runtime/`: `Api/` contains Jellyfin HTTP models and clients, `Core/` owns sessions and shared utilities, `Services/` assembles catalog and playback behavior, and `UI/` builds the spatial interface. Editor-only tooling is in `Assets/JellyfinForRayNeo/Editor/`; the main scene is `Scenes/Main.unity`. Tests are split between `Tests/Editor/` and `Tests/PlayMode/`. Android companion code and Gradle templates live under `Assets/Plugins/Android/`. Keep dependency setup in `scripts/` and Unity configuration in `Packages/` or `ProjectSettings/`.

## Build, Test, and Development Commands

Install the untracked RayNeo SDK dependencies before opening the project:

```bash
./scripts/install-rayneo-sdk.sh
./scripts/install-libvlc-android.sh
```

Open the repository in Unity Hub with `2022.3.62f3c1`, then run `Jellyfin for RayNeo > Configure Project and Scene` if the XR scene needs regeneration. Build Android from `File > Build Settings`; the expected output is `Builds/Android/JellyfinForRayNeo.apk`.

Run automated tests headlessly:

```bash
UNITY=/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults /tmp/editmode.xml -logFile /tmp/editmode.log
"$UNITY" -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults /tmp/playmode.xml -logFile /tmp/playmode.log
```

## Coding Style & Naming Conventions

Use four-space indentation and Allman braces in C# and Java. Name types and public members with `PascalCase`, locals and parameters with `camelCase`, and private fields with `_camelCase`. Prefer one primary type per file and match its filename. Preserve Unity `.meta` files whenever assets move or are added. Keep Editor APIs behind an `Editor/` folder or `#if UNITY_EDITOR`.

## Testing Guidelines

Tests use NUnit through the Unity Test Framework. Name fixtures `*Tests` and test methods after observable behavior, such as `HomeShelves_AcceptDragFromTransparentBackground`. Add EditMode tests for parsing and state logic; use PlayMode tests for scenes, ray input, layout, and lifecycle behavior. Never place real Jellyfin credentials or LAN addresses in fixtures.

## Commit & Pull Request Guidelines

Follow the existing Conventional Commit style: `feat: add ...` or `fix: prevent ...`. Keep commits focused and include related tests. Pull requests should explain user-visible behavior, list EditMode/PlayMode and device verification, link relevant issues, and attach screenshots or short recordings for UI changes.

## Security & Local Configuration

Do not commit RayNeo or LibVLC binaries, access tokens, passwords, `Library/`, `Builds/`, or machine-specific package paths. Preserve unrelated local changes in `Packages/manifest.json`, `packages-lock.json`, and `PackageManagerSettings.asset` unless the change explicitly requires them.
