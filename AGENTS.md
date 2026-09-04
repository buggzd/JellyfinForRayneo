# Repository Guidelines

## Project Structure & Module Organization

This is a native Android application with two React/Vite frontends.
`AndroidApp/` is the Gradle application. Native orchestration lives under
`AndroidApp/app/src/main/java/com/jellyfinforrayneo/client/`; JVM tests mirror
that package under `src/test/`. `GlassesUI/` owns catalog browsing, details,
HTML video/HLS playback, and rendered WebVTT subtitles. `CompanionUI/` owns
phone discovery, login, settings, and the OLED-black touchpad. Production
bundles are committed under `AndroidApp/app/src/main/assets/GlassesUI/` and
`AndroidApp/app/src/main/assets/CompanionUI/`. Architecture and bridge details
are documented in `docs/ANDROID_ARCHITECTURE.md`.

The application does not use Unity, Cardboard, or LibVLC. Do not reintroduce a
Unity Player, scene, Unity activity/adapter, duplicate native UI, or native
fallback player.

## Architecture & Interaction Rules

The phone owns discovery, credentials, Quick Connect, login settings, and the
OLED-black touchpad. The glasses own browsing, details, and playback. Do not
add a glasses-side “waiting for phone” page: the glasses are phone-powered, so
a visible glasses frame already implies a phone connection. Android directly
coordinates sessions, commands, playback state, and RayNeo display state. Keep
all WebView messages validated and bounded.

`SessionRepository` is the only native session source. Every restored or newly
created session must be rebuilt from exactly `serverUrl`, `serverName`,
`serverVersion`, `serverId`, `accessToken`, `userId`, `userName`, and
`deviceId`. Validate lengths and required values before persisting or sending
the session to the glasses. Clear the repository and glasses bootstrap on
logout or unauthorized restore. Passwords exist only for one authentication
request and must be removed from JSON and wiped from mutable buffers in
`finally` paths. Never log a payload, token, password, Quick Connect secret, or
Jellyfin address.

Support Jellyfin through IPv4, A/AAAA host names, and standard bracketed IPv6
literals. A literal with a port must use `http://[IPv6]:port`. Jellyfin UDP
discovery is IPv4 broadcast only; do not present it as IPv6 discovery. Keep the
glasses catalog state separate from a restored phone session and from WebView
page readiness. `runtime_state.errorCode` is restricted to `none`, `network`,
`http`, `response`, and `unknown`; map it to fixed safe phone diagnostics and
never forward server response bodies or arbitrary exception text.

Phone-exported diagnostics must remain in memory, accept only fixed event enum
values, and expose only bounded derived state. Never include a full server
address, account name, media title, Quick Connect code, request/response body,
session JSON, token, or password in the report or Android share intent.

Preserve both modes managed by `RayNeoDisplayController`. `Mirror2D` displays
one full-width WebView frame. `StereoVirtualScreen` measures one WebView at
per-eye width and draws the same render into both SBS halves. Never create two
glasses WebViews or two `<video>` elements: that duplicates decode, audio, and
Jellyfin playback reports. During a hardware mode transition, keep the WebView
behind a black transition frame. Keep exact confirmation
(`displayModeApplied`) separate from active transition
(`displayModeTransitioning`); only an active transition may hide the WebView.
A rejected, failed, or timed-out switch must finish in a visible safe
`Mirror2D` layout. Do not retry automatically: another attempt requires an
explicit phone selection, a glasses reconnect, or a lifecycle resume so a
missing SDK callback cannot cause periodic black frames.

The glasses player uses HTML `<video>` as its decode surface. `hls.js`
downloads and demuxes HLS into MSE; it does not decode video. Chromium selects
an Android `MediaCodec` component. Hardware acceleration flags support
composition but do not force video hardware decode. The native bridge must
enumerate hardware video decoders, and `GlassesUI` must intersect that set with
WebView support before advertising Jellyfin direct play. Keep H.264/VP8 at
8-bit and HEVC/VP9/AV1 at 10-bit unless a future profile-aware probe proves a
broader hardware path. Unknown, software-only, or out-of-limit sources use the
H.264/AAC HLS fallback. Device tests must verify the selected `MediaCodec`.

Directional input goes to the active glasses WebView first. Android-injected
keyboard events originate from `document.activeElement`, falling back to
`document.body`, and DOM handlers type-check `EventTarget` before calling
element APIs. Synthetic events do not reliably activate `:focus-visible`, so
programmatic navigation must use the shared spatial-focus helper, keep exactly
one `data-spatial-focus="true"` marker, and style it alongside
`:focus-visible`. Clear the marker when pointer or non-spatial focus takes
over. While video is active, underlying pages remain non-interactable.

## Build, Test, and Development Commands

Install the ignored RayNeo Android SDK binary and frontend dependencies:

```bash
./scripts/install-rayneo-sdk.sh
npm --prefix GlassesUI ci
npm --prefix CompanionUI ci
```

`ANDROID_HOME`/`ANDROID_SDK_ROOT` or `AndroidApp/local.properties` must point
to an SDK containing platform 35 and build tools 34.0.0. Use JDK 17 or newer.

Run the reproducible Android pipeline:

```bash
./scripts/build-android.sh debug
./scripts/build-android.sh release
./scripts/build-android.sh all
```

The pipeline runs the glasses TypeScript check, builds both frontends, runs
JVM tests and Android lint, then assembles the selected APK. For focused work:

```bash
npm --prefix GlassesUI run check
npm --prefix GlassesUI run build
npm --prefix CompanionUI run build
cd AndroidApp
./gradlew :app:testDebugUnitTest :app:lintDebug :app:assembleDebug
```

For browser development, copy `.jellyfin-dev.example.json` to the ignored
`.jellyfin-dev.json`, fill it locally, and run
`npm --prefix GlassesUI run dev`. That JSON is served only by Vite development
middleware and must never enter a production bundle or Git.

Install the coexistence-safe Debug package from
`AndroidApp/app/build/outputs/apk/debug/app-debug.apk`. It uses the
`com.jellyfinforrayneo.client.debug` application ID. Release retains
`com.jellyfinforrayneo.client`; an in-place upgrade requires the same signing
certificate as the installed package and a higher version code.

## Coding Style & Naming Conventions

Use four-space indentation and Allman braces in Java. Use `PascalCase` for
types/public members, `camelCase` for locals/parameters, and concise lowercase
package names. Prefer one primary type per file. Keep Android framework work on
the UI thread and network/discovery work on bounded executors. In React code,
follow the existing functional-component and hook style; preserve TypeScript
strictness in `GlassesUI`.

## Testing Guidelines

JVM tests use JUnit 4. Name fixtures `*Tests` and methods after observable
behavior, for example `timeout_RevealsSafeMirrorUntilExplicitRetry`.
Cover parsing and state transitions without Android framework dependencies
where practical. WebView changes require the glasses TypeScript check, both
frontend builds, JVM tests, lint, and an APK build.

Run `scripts/verify-no-unity.sh` against the final APK. Device-facing changes
also require the RayNeo regression matrix: phone and glasses WebViews, attach/
detach/reconnect, pause/resume, 2D/3D success and fallback, login/Quick Connect/
restore/logout, browsing, playback, subtitles, tracks, remote focus, renderer
recovery, selected hardware codec, and confirmation that only one audio stream
and one playback report stream exist.

## Commit & Pull Request Guidelines

Use Conventional Commits such as `feat: add ...`, `fix: prevent ...`, or
`docs: update ...`. Keep commits focused and commit each verified stage instead
of accumulating unrelated changes. Pull requests should describe user-visible
behavior, list browser/JVM/lint/device verification, link issues, and include
screenshots or recordings for UI changes.

## Security & Local Configuration

Never commit RayNeo binaries, APK/AAB files, credentials, LAN addresses,
`.jellyfin-dev.json`, `local.properties`, keystores, signing properties, Gradle
state, build output, or absolute local SDK paths. Do not put real passwords or
access tokens in docs, source, fixtures, screenshots, generated bundles, test
output, or logs. The RayNeo installer must keep both archive and extracted AAR
checksums pinned. Preserve unrelated user edits, and stage generated Web bundles
deliberately.
