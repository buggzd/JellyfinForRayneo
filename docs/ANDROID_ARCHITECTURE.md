# Native Android architecture

Jellyfin for RayNeo is a native Android application with two local React/Vite
frontends. Android owns lifecycle, session persistence, RayNeo display control,
the external `Presentation`, and the bounded JavaScript bridges. There is no
Unity Player or native fallback player in the runtime.

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
│   └── RayNeo AirApi
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
session. A non-persistent login is held only in process memory.

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
buffers in `finally` blocks. Logs contain only generic SDK failure messages.

Server URLs support IPv4, host names resolved through A/AAAA records, bracketed
IPv6 literals with an optional port, and Jellyfin subpaths. A bare IPv6 literal
without a port is bracketed during normalization. A literal with a port must use
the standard `http://[address]:port` form. Scoped link-local literals containing
a zone identifier are rejected because Java and Chromium WebView do not share a
reliable URL representation for them. Jellyfin UDP discovery remains IPv4
broadcast based, so IPv6-only endpoints are entered manually through an AAAA
host name or a global/ULA literal.

Login publishes a new bootstrap directly to the glasses WebView. Logout and a
Jellyfin `401`/`403` clear the repository, pending remote commands, playback
snapshot, and in-memory glasses bootstrap. There is no polling loop or second
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
| `scan`, `selectServer` | UDP discovery and server selection |
| `login`, `startQuickConnect`, `cancelQuickConnect` | Authentication |
| `clearSession` | Logout/change account |
| `retryGlasses` | Republish the session bootstrap after a catalog failure |
| `shareDiagnostics` | Open Android's share sheet with a redacted diagnostic report |
| `selectDisplayMode` | Save and request 2D/3D mode |
| `remoteCommand`, `previewHaptic` | Bounded touchpad input |
| `copyQuickConnectCode`, `openQuickConnectAuthorization` | Phone helpers |
| `screenChanged` | Phone surface and back-navigation state |

Android pushes phone state through `window.LumaNative.receiveState`. This state
includes connection, display, discovery, and playback UI data, but never the
access token or password. `glassesPresentationReady` means only that the local
glasses page is running; `glassesRuntimeState=ready` and `mediaReady=true` mean
that page actually loaded the Jellyfin catalog. The phone keeps a glasses-side
failure visible so field testing does not require ADB.

`GlassesUI` calls `window.RayNeoGlasses`:

| Method | Purpose |
| --- | --- |
| `getBootstrapState`, `ready` | Receive the whitelisted session and display state |
| `getHardwareVideoCodecs` | Enumerate hardware video decoder families |
| `postMessage` | Send validated runtime/playback/session events to Android |

Accepted glasses messages are `manage_login`, `logout`, `unauthorized`,
`runtime_state`, and `playback_state`. The whole message and every individual
field have fixed limits. Remote commands are limited to direction, enter,
back, and a bounded volume percentage; the pending queue holds at most 32
items.

`runtime_state.errorCode` accepts only `none`, `network`, `http`, `response`, or
`unknown`. Android maps those categories to fixed Chinese diagnostics and never
forwards a Jellyfin response body, URL, token, or arbitrary exception text to
the phone. A `loading` or `ready` transition clears an older runtime error.

Directional keyboard events originate at `document.activeElement`, falling
back to `document.body`, and bubble from an element target. `GlassesUI` owns the
single `data-spatial-focus="true"` marker. While video is active, the player
scope prevents underlying pages from receiving input.

## RayNeo display state

`RayNeoDisplayController` calls `AirApi.init(Activity)` directly and listens for
command responses. It deliberately does not use the SDK's Unity adapter path.
`GlassesPresentationController` selects an active non-default external display,
preferring a RayNeo/TCL presentation display.

The state machine keeps these values separate:

- `requestedMode`: saved phone preference
- `activeMode`: layout currently safe to show
- `displayModeApplied`: exact hardware mode was confirmed
- `displayModeTransitioning`: a hardware command is in flight

```text
request mode
  -> show black transition view and hide WebView
  -> issue RayNeo 2D/3D command
     -> matching success callback: apply layout and reveal WebView
     -> rejection/timeout/SDK error: use visible Mirror2D and stop retrying
```

Only an active transition hides the WebView. A failed switch therefore ends in
a visible single-frame 2D layout even though the requested mode remains
unconfirmed. A new attempt occurs only after an explicit phone selection,
glasses reconnect, or lifecycle resume. Pause, destroy, and disconnect also
request or settle on safe 2D.

## Field diagnostics without ADB

The phone settings screen can open Android's `ACTION_SEND` chooser with a
plain-text report suitable for QQ or another sharing app. The report is held
only in memory and contains app/Android/WebView versions, device model, boolean
session state, derived server shape, derived active-network capabilities,
glasses/WebView/runtime/display state, and at most 160 fixed event enum values.

The server is represented only as `http`/`https`, host kind
(`hostname`/`ipv4-literal`/`ipv6-literal`), and whether it has a subpath.
Network addresses, DNS server values, server URL, account, media titles,
Quick Connect code, session payload, credentials, response bodies, and
arbitrary exception text are never exported.

## Single-WebView stereo rendering

`StereoVirtualScreen` does not create a second WebView. `StereoMirrorLayout`
measures the one glasses WebView at per-eye width, draws its frame into the left
half, and draws the same frame again into the right half. This preserves one
HTML `<video>`, one decoder, one audio stream, and one set of Jellyfin playback
reports.

The native bridge enumerates hardware-accelerated `MediaCodec` decoders.
`GlassesUI` intersects those codec families with WebView container support and
advertises only bounded direct-play profiles to Jellyfin. H.264/VP8 are limited
to 8-bit and HEVC/VP9/AV1 to 10-bit. Unknown, software-only, incompatible, or
out-of-limit media uses the H.264/AAC HLS fallback. `hls.js` handles transport
and MSE demuxing; Chromium still selects the actual Android decoder.

## Verification

Desktop verification covers TypeScript, both production bundles, JVM tests,
Android lint, Debug/Release assembly, and APK inspection. Device acceptance
must additionally cover display attach/detach, 2D/3D callbacks and timeout,
login/session restore/logout, browse and playback flows, remote focus,
renderer recovery, one audio/report stream, and the selected `MediaCodec`
component during representative playback.
