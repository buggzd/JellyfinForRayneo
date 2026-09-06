# Native Android architecture

Jellyfin for RayNeo is a native Android application with two local React/Vite
frontends. Android owns lifecycle, session persistence, RayNeo display control,
the external `Presentation`, and the bounded JavaScript bridges.

## Runtime topology

```text
MainActivity
├── CompanionWebViewController
│   └── CompanionUI on the phone display
├── SessionRepository
├── JellyfinAuthenticationService
├── JellyfinDiscoveryService
├── RemoteCommandRouter
├── RayNeoDisplayController
│   └── RayNeoUsbDisplayClient (Android USB Host)
└── GlassesPresentationController
    └── Presentation on the selected external display
        └── GlassesWebViewController
            ├── black transition view
            └── StereoMirrorLayout
                └── one GlassesUI WebView
```

The phone owns discovery, credentials, Quick Connect, settings, and the
OLED-black touchpad. The glasses own catalog browsing, details, HTML video/HLS
playback, subtitles, and Jellyfin playback reports. A visible glasses frame
already implies a phone connection, so `GlassesUI` has no waiting-for-phone
screen.

## Session boundary

`SessionRepository` is the only native session source. Its private
`SharedPreferences` file remains named `jellyfin_companion`, allowing an
upgrade from the former Android activity to reuse its already validated
session. The legacy `session_json` entry migrates once into the bounded
`accounts_v1` registry, then is removed. The registry stores up to 12 validated
server/user sessions and exactly one active account ID in one JSON value.
Each entry has an opaque 32-character local ID; the server URL and user ID
identify an existing login when its token is renewed. A non-persistent login
is switchable only in process memory and never enters the persisted registry.
If the active login is transient, a cold launch has no active account but still
allows choosing the other saved accounts.

Every restored or newly authenticated session is rebuilt from exactly these
eight string fields:

- `serverUrl`
- `serverName`
- `serverVersion`
- `serverId`
- `accessToken`
- `userId`
- `userName`
- `deviceId`

Required values, field lengths, JSON size, URL scheme, and URL authority are
validated before the session is accepted. Extra fields are discarded. The
password is never persisted or included in a state payload; the authentication
request removes it from the JSON object and wipes its byte and character
buffers in `finally` blocks. Logs contain only generic failure categories.

Server URLs support IPv4, host names resolved through A/AAAA records, bracketed
IPv6 literals with an optional port, and Jellyfin subpaths. A bare IPv6 literal
without a port is bracketed during normalization. A literal with a port must use
the standard `http://[address]:port` form. Scoped link-local literals containing
a zone identifier are rejected because Java and Chromium WebView do not share a
reliable URL representation for them. Jellyfin UDP discovery remains IPv4
broadcast based, so IPv6-only endpoints are entered manually through an AAAA
host name or a global/ULA literal.

Server selection and an in-progress login leave the active account unchanged.
Only successful authentication or explicit activation of an existing account
replaces it and publishes a new bootstrap directly to the glasses WebView.
Switching clears previous playback, remote commands and search state; glasses
navigation and the player reset for the new identity. Logout, removal of the
active account and a Jellyfin `401`/`403` remove that account, pending remote
commands, playback snapshot, and in-memory glasses bootstrap. Other saved
accounts remain available. Unauthorized events carry a bounded catalog
generation; a late response from a previous account cannot clear the new one. There is no polling loop or second
session replica. Native bootstrap payloads are compared with the last payload
before injection, and `GlassesUI` compares their normalized value again before
notifying React. A bounded `catalogGeneration` changes only after login or an
explicit catalog retry, so display-state publications cannot cancel and restart
the catalog requests.

## WebView bridges

Both WebViews load only their own `file:///android_asset/...` root. Main-frame
navigation outside that root, traversal segments, backslashes, NULs, and
percent-encoded paths are rejected. JavaScript interface inputs are normalized,
length-limited, and whitelisted before use.

`CompanionUI` calls `window.JellyfinNative`:

| Method | Purpose |
| --- | --- |
| `getState`, `ready` | Initial state and receiver readiness |
| `scan`, `selectServer` | UDP discovery and login target selection, retaining the active account |
| `activateSession`, `removeSession` | Activate/remove an existing opaque account ID; arbitrary or oversized IDs are rejected |
| `login`, `startQuickConnect`, `cancelQuickConnect` | Authentication |
| `clearSession` | Logout and forget the active account |
| `retryGlasses` | Republish the session bootstrap after a catalog failure |
| `shareDiagnostics` | Open Android's share sheet with a redacted diagnostic report |
| `selectDisplayMode` | Save and request 2D/3D mode |
| `selectUiTheme` | Persist exactly `liquid-glass` or `simpleUI` and publish it to both surfaces |
| `setStereoScreen` | Save a bounded flat-screen disparity/size preference without switching hardware mode |
| `setStereoTestPattern` | Enable/disable the temporary L/R reference overlay while stereo is applied |
| `remoteCommand`, `searchText`, `previewHaptic` | Bounded touchpad input and the active Series-search query |
| `copyQuickConnectCode`, `openQuickConnectAuthorization` | Phone helpers |
| `screenChanged` | Phone surface and back-navigation state |

Android pushes phone state through `window.LumaNative.receiveState`. This state
includes connection, display, discovery, playback, and bounded search UI data,
but never the access token or password. Its account list contains only the local
ID, server metadata, username, persistence flag and active flag. `serverUrl` and
`username` describe the active account; separate `loginServerUrl` and
`loginServerName` describe the pending login target. Browsing the `accounts`,
`connect` and `auth` surfaces never clears the active account, and state updates
cannot redirect a pending login back to the current connection. Phone
localStorage does not store native account/session metadata.
While the glasses search page is open,
`searchInputActive=true` moves the phone to its touchpad, focuses its search
field, and requests the system QWERTY keyboard; `searchQuery` mirrors at most 48
lowercase ASCII letters, digits, and spaces. `glassesPresentationReady` means
only that the local glasses page is running; `glassesRuntimeState=ready` and
`mediaReady=true` mean that page actually loaded the Jellyfin catalog. The phone
keeps a glasses-side failure visible so field testing does not require ADB.

`GlassesUI` calls `window.RayNeoGlasses`:

| Method | Purpose |
| --- | --- |
| `getBootstrapState`, `ready` | Receive the whitelisted session and display state |
| `getHardwareVideoCodecs` | Enumerate hardware video decoder families |
| `postMessage` | Send validated runtime/playback/session events to Android |

Accepted glasses messages are `manage_login`, `logout`, `unauthorized`,
`runtime_state`, `playback_state`, and `search_state`. The whole message and
every individual field have fixed limits. `search_state` carries only
`active`/`inactive` plus the bounded ASCII query; leaving search, logout, a lost
glasses WebView, or an unauthorized restore clears the phone input and hides
the IME. Remote commands are limited to direction, enter, back, a bounded
volume percentage, bounded `search-text`, search submit, and keyboard visibility
signals; the pending queue holds at most 32 items.

`runtime_state.errorCode` accepts only `none`, `network`, `http`, `response`, or
`unknown`. Android maps those categories to fixed Chinese diagnostics and never
forwards a Jellyfin response body, URL, token, or arbitrary exception text to
the phone. A `loading` or `ready` transition clears an older runtime error.

Directional keyboard events originate at `document.activeElement`, falling
back to `document.body`, and bubble from an element target. `GlassesUI` owns the
single `data-spatial-focus="true"` marker. While video is active, the player
scope prevents underlying pages from receiving input.

The remote tutorial is a separate glasses React surface, shown once after the
catalog becomes ready and reachable again through the side navigation. It
consumes the existing bubbling keyboard event only, not the paired
`rayneo-remote-command` notification, and owns one spatial focus marker. Catalog
pages and the player are unmounted during practice; search input is inactive.
Wrong gestures cannot pass a lesson, and the success beat consumes repeated
input before advancing. An exit dialog makes the practice surface inert.
Session loss unmounts the tutorial and drops in-progress practice. Only a
versioned `completed`/`skipped` flag is saved in glasses localStorage; no session,
media, or account data is stored there. The tutorial creates no video, native
bridge method, playback report, WebView, or display-mode transition.

The glasses search surface uses one Apple TV-style A-Z/0-9 character strip as
a remote-only fallback and shows Series posters without episode rows. Pressing
down enters the poster grid; up from its first row or left from its first column
returns to the strip. Phone QWERTY input updates results on every edit, and the
phone keyboard's search action focuses the first matching Series. Full pinyin,
pinyin initials, English titles, and optional season/episode hints are resolved
locally from the bounded Series index.

## UI appearance preference

`SessionRepository` stores `ui_theme` independently of accounts and display mode.
Missing or unknown saved values use `liquid-glass`; `selectUiTheme` rejects every
value except the two exact theme names before scheduling work on the UI thread.
Both the phone state and glasses bootstrap include `uiTheme`. A theme edit updates
system bars and the phone WebView background, then publishes the existing state
channels. It does not increment `catalogGeneration`, request hardware mode changes,
recreate a WebView, or remount the video. Logout preserves this device preference.

Both React entry points restore the theme before their first render. The phone
uses native acknowledgements to update its radio selection; it stores no second
copy of the native preference in localStorage. Standalone browser previews and the
dual-UI harness may save only the appearance choice under a preview-specific key.
`SharedUI` supplies the exact enum normalization and simpleUI rendering rules;
both Gradle frontend tasks include that directory in their build inputs.
See [UI themes](UI_THEMES.md) for the visual design and verification boundary.

## RayNeo display state

`RayNeoDisplayController` controls the verified Air 3s HID interface directly
through Android USB Host APIs. The APK has no RayNeo SDK, XR Space binding,
package query or launcher integration. The historical SDK/official control
implementation was used only to establish the two mode reports; no vendor
binary is bundled. Protocol provenance is recorded in
[SBS geometry analysis](SBS_GEOMETRY_ANALYSIS.md#16-移除-xr-空间依赖直接控制-usb2026-09-06).

`RayNeoUsbDisplayClient` accepts only USB VID/PID `1bbb:af50`, interface 0,
HID class 3/subclass 0/protocol 0, and the verified interrupt endpoints
`01`/`81` with 64-byte packets. Only 64-byte reports `66 06 00…` (SBS) and
`66 07 00…` (2D) are supported. A single worker with a one-item queue discards
obsolete requests; `UsbRequest` has a 750 ms wait and always releases the
claimed interface and connection. It never writes firmware, resets USB, or
sends arbitrary WebView-provided bytes.

Android grants USB access to this application through the standard system
permission dialog. The private receiver checks the actual `UsbManager` grant,
not broadcast extras. Waiting for consent reveals content and does not run a
hardware timeout. Denial leaves the mode unconfirmed, without reopening the
prompt on resume. An explicit selection can request consent again. Permission
dialog lifecycle is kept separate from leaving the application.

The state machine keeps these values separate:

- `requestedMode`: saved phone preference
- `activeMode`: layout currently safe to show
- `displayModeApplied`: exact hardware mode was confirmed
- `displayModeTransitioning`: a bounded hardware transition is in flight

```text
request mode
  -> observe initial Presentation geometry
     -> physical/View output already matches: apply without a USB command
     -> USB permission needed: reveal content and wait for system consent
     -> permission available: send one mode report, then observe physical/View output
        -> requested mode confirmed: apply and reveal WebView
        -> unavailable/timeout: end transition, request safe 2D once, stop retrying
```

Only an active transition hides the WebView. The hardware/geometry deadline is
8 seconds; USB permission waiting is outside it. A completed USB write alone
cannot mark stereo applied. A fresh attempt occurs after an explicit selection,
reconnect or lifecycle resume. Returning to an already-correct physical mode
uses the measured output and avoids another EDID reconnect. The initial window
must be measured before deciding to send a command.

Stereo requires both physical `Display.Mode` and the measured Presentation
root to have a Full-SBS 32:9 aspect, even width, and at most two pixels of width
rounding. A physical 3840×1080 mode may have a uniformly downscaled 1920×540 View;
1920×1080 half-SBS is unsupported. Mirror confirmation requires physical
1920×1080 and a nonempty root. `DisplayOutputGeometry` tracks physical/View
pixels, refresh rate and readiness, including same-ID changes. Losing valid
stereo geometry falls back once; late events do not restart a failed transition.
These conditions do not replace optical calibration or eye-order testing.

`GlassesPresentationController` prefers a valid RayNeo/TCL presentation display.
A named glasses display temporarily reporting OFF remains eligible; the phone's
sleeping rear screen does not. While connected, the phone and glasses windows
keep the screen awake.

A physical switch can remove and recreate Android's logical display because its
EDID changes. During the existing 8-second transition, the controller retains
the one WebView and reparents it to the replacement Presentation without
reloading the document/video. Old-window layout callbacks are ignored. If the
transition ends without a usable display, the retained WebView is released.

The retained WebView uses the phone Activity's stable rendering context. The
Presentation still owns its external window, container and measured pixels.
Creating the renderer with a disappearing external-display context can leave
Chromium's cached display density inconsistent after an EDID reconnect. Merely
replacing a `MutableContextWrapper` base does not repair that cached state.
`GlassesUI` declares a 1440 CSS-pixel viewport without a forced initial scale;
WebView overview fitting maps the complete page to the measured per-eye width.
For a 1920×1080 source View this gives 1440×810 CSS pixels. Renderer
`screen.*` and device pixel ratio may describe the phone; hardware confirmation
continues to use `Display.Mode` and the external root, never those JS values.

On the tested Xiaomi HyperOS device, connecting glasses or changing modes can
require the user to enable the system's **screen mirroring** control before
external content is allowed. `glassesDisplayDisabled` identifies that connected
but disabled output through a read-only display category. The phone asks the
user to enable screen mirroring; it does not open XR Space or treat another
app's startup as a remedy. While the system output is disabled, no further USB
mode reports are sent. App fallback removes its black layer but cannot enable
an OS-disabled screen. The user's system permission action and the eye-mode
command are separate requirements.

## Field diagnostics without ADB

The phone settings screen can open Android's `ACTION_SEND` chooser with a
plain-text report suitable for QQ or another sharing app. The report is held
only in memory and contains app/Android/WebView versions, device model, boolean
session state, derived server shape, derived active-network capabilities,
glasses/WebView/runtime/display state, numeric output dimensions/refresh rate,
stereo settings and test-pattern state, and at most 160 fixed event enum values.

The server is represented only as `http`/`https`, host kind
(`hostname`/`ipv4-literal`/`ipv6-literal`), and whether it has a subpath.
Network addresses, DNS server values, server URL, account, media titles,
Quick Connect code, session payload, credentials, response bodies, and
arbitrary exception text are never exported.

## Single-WebView stereo rendering

`StereoVirtualScreen` does not create a second WebView. `StereoMirrorLayout`
measures the one glasses WebView at per-eye width, draws its frame into the left
half, and draws the same frame again into the right half with a different
horizontal transform. This preserves one
HTML `<video>`, one decoder, one audio stream, and one set of Jellyfin playback
reports.

`StereoScreenGeometry` uses total eye-local disparity `d = uL - uR`: left
translation is `inset + d/2`, right is `inset - d/2`. Positive disparity adds
convergence. Translation occurs before a uniform Canvas scale so disparity does
not change with screen size. Each eye clips to its own viewport; centering and
`s*N + |d| + 2*m <= N` preserve the entire image with at least 1% edge margin.
The black container clears the unused area. No CPU frame readback is introduced.

`StereoScreenSettings` stores only `depthLevel` (integer 0–3) and `sizePercent`
(integer 80–95), in the native repository. The JSON bridge accepts exactly these
two numeric fields within 128 characters. Levels correspond to total disparities
0/8/16/24 at a reference width of 1920 per eye, scaled by actual View eye width.
The initial preference is level 1 and 90%; these are initial engineering values,
not measured comfort limits. Unknown optical zero distance and individual IPD
mean the UI uses relative proximity rather than an uncalibrated distance in metres.
Normal 2D content remains a flat screen and follows head motion.

Settings animate disparity and size over 180 ms (respecting disabled system
animations) using only Canvas transforms. They do not request a new WebView
layout, bootstrap, hardware switch or player. Visibility/attachment callbacks
restart the stereo redraw loop after transitions and stop it when hidden.

`stereoScreen`, `stereoOutput`, `stereoTestPattern` and `glassesDisplayDisabled` are included in native phone
state. The frontend keeps an in-memory editor, ignores older acknowledgements
while the latest edit is pending, and does not persist another copy of these
preferences in localStorage. Settings survive disconnect, Activity recreation
and logout. The pattern is transient: exact bridge values `on`/`off` are accepted;
enabling also requires the phone settings surface, a ready glasses WebView and
applied stereo. White frames have zero added disparity, L/R identify eye channels,
and cyan targets share the content transform. The overlay leaves video visible.
Leaving settings, mode exit/failure, pause, disconnect, logout or renderer loss
clears it. See [SBS geometry analysis](SBS_GEOMETRY_ANALYSIS.md) for derivation,
official parameter sources and optical/device limitations.

The native bridge enumerates hardware-accelerated `MediaCodec` decoders.
`GlassesUI` intersects those codec families with WebView container support and
advertises only bounded direct-play profiles to Jellyfin. Those profiles stop
at 3840×2160 and 120 Mbps; H.264/VP8 are limited to 8-bit and HEVC/VP9/AV1 to
10-bit. Unknown, software-only, incompatible, or out-of-limit media uses the
24 Mbps, two-channel H.264/AAC HLS fallback. `hls.js` handles transport and MSE
demuxing; Chromium still selects the actual Android decoder.

The glasses player's optional video-information overlay reads the existing
playback plan, HTML video dimensions/quality/buffered ranges, and the current
HLS rendition and demuxed codecs. It distinguishes output parameters from the
original media and samples once per second only while enabled and the document
is visible. It adds no server polling, native bridge method, player, or report
stream. Changing sources invalidates previous samples; changing episodes keeps
the toggle within that playback visit, while leaving playback or switching
accounts clears it. Hardware codec enumeration describes capability only:
WebView exposes no public API for the active hardware/software decoder, so the
overlay explicitly reports automatic selection and unreported actual status.

## Verification

Desktop verification covers TypeScript, both production bundles, JVM tests,
Android lint, Debug/Release assembly, and APK inspection. Device acceptance
must additionally cover display attach/detach, USB grants, physical mode observation and timeout,
login/session restore/logout, browse and playback flows, remote focus,
renderer recovery, one audio/report stream, and the selected `MediaCodec`
component during representative playback.

Build commands and APK inspection details live in
[DEVELOPMENT.md](DEVELOPMENT.md#验证). The following matrix is the
minimum device regression set for any device-facing change.

### Device regression matrix

| Area | Required cases | Pass condition |
| --- | --- | --- |
| Install and lifecycle | First launch, cold launch, background/foreground, Activity recreation | Phone UI and glasses Presentation recover without a stale or duplicate session |
| Authentication | IPv4 discovery, manual hostname/IPv6 URL, Quick Connect, password login, remembered and non-persistent login | Exactly one validated session reaches the glasses; passwords never persist |
| Account management | Two servers, two users on one server, remembered/transient accounts, old-version migration, cold restart, failed/cancelled password and Quick Connect login | Switching reuses the selected login, resets old browsing/playback state, and failed/cancelled additions retain the original connection |
| Session cleanup | Logout, remove active/inactive account, restored `401`/`403`, late unauthorized event after switching | Only the affected account is forgotten; active cleanup clears bootstrap, pending commands, playback and both UIs together; other accounts remain usable |
| Display connection | Glasses attached before launch, attached after launch, disconnected and reconnected | The intended external display is selected and phone UI stays on the default display |
| Display modes | Confirmed Mirror 2D and stereo switch, USB permission denied, occupied interface, exception, physical output timeout | Consent waiting stays visible; only a hardware transition hides the WebView; failures end the transition without automatic retries, while OS-disabled output still requires system mirroring |
| SBS geometry | Command response/write before/after actual 3840×1080 output; same-ID resize; EDID display recreation; unsupported half-SBS/rotated/inset viewport | Stereo requires command and physical/View evidence; document/video survive a bounded transition; all four page edges and full playback controls remain visible after both switch directions; invalid output falls back once |
| System display availability | OS disables a recreated external display, enable inside/outside the transition deadline | The phone identifies disabled output, no false applied state or automatic retry loop; app fallback does not claim to enable an OS-disabled display |
| Virtual screen controls | Fixed 90% size at all four depth levels, then fixed depth at 80–95%; rapid edits; pause/resume; cold restart | Left/right offsets are ±d/2, average center and vertical alignment stay fixed, size is independent, full image stays in each eye and saved settings restore |
| Eye reference overlay | Close each eye alternately; compare baseline and increased disparity; leave settings, switch mode, disconnect, logout and kill renderer | Left eye sees L, right sees R; cyan plane moves closer relative to white reference, no persistent overlay after exit/recovery |
| Stereo video composition | Moving frame-number video with DOM controls and text subtitles in both modes, while changing depth/size | Both eyes receive the same frame, video/subtitles/DOM receive identical transforms, no frozen video, duplicate sound/reporting, clipped edge or cross-eye leakage |
| Browse and focus | Home, search, filters, folders, details, long lists, dialogs, remote back | Exactly one visible spatial focus target exists and overlays prevent background input |
| UI themes | Default install, saved simpleUI cold launch, rapid switches during browse/direct play/HLS/tutorial in both 2D and SBS, disconnect/reconnect, renderer recovery, logout, reset preferences | Both surfaces and phone system bars agree; focus, document, video, audio and reporting remain single-instance; theme survives logout/recovery and reset restores liquid-glass; simpleUI has no decorative loops or blur |
| Remote tutorial | First ready catalog, skip/relaunch, six phone gestures, wrong/rapid input, pause/resume/exit, sidebar replay, logout, 2D/SBS switch and renderer recovery | Each real gesture advances once; exactly one focus stays inside practice/dialog; completion or skipping is remembered; no media playback or background navigation; SVG motion and text remain readable in both eyes |
| Playback | Direct play, H.264/AAC HLS fallback, pause, seek, previous/next item, audio track, text and bitmap subtitle | Playback remains controllable, progress is reported once, and the selected track is reflected in UI |
| Single-instance invariants | Mirror and stereo during representative playback | One glasses WebView, one HTML `<video>`, one audio stream, and one Jellyfin reporting stream remain active |
| Renderer recovery | Kill or crash the glasses WebView renderer during browse and playback | The WebView is rebuilt, session bootstrap is republished, and the phone receives a safe state |
| Codec selection | Representative H.264, HEVC/VP9/AV1 where hardware advertises support, plus an unsupported source | The actual Chromium `MediaCodec` component matches expectations; incompatible media requests the bounded HLS fallback |
| Field diagnostics | Network, HTTP, response, and unknown failures; Android share flow | The phone shows the correct fixed category and the exported report contains no URL, account, title, code, token, password, body, or arbitrary exception text |
