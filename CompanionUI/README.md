# Embedded companion UI

This frontend is the phone-side interface packaged into the Android APK. It is
based on the supplied Luma Link presentation template, while its data and
actions are provided by the native Android `MainActivity` through the local
`JellyfinNative` JavaScript bridge.

Run a browser preview:

```bash
npm install
npm run dev
```

Rebuild the APK assets after changing the frontend:

```bash
npm run build
```

The production output is written to
`AndroidApp/app/src/main/assets/CompanionUI`. The UI is fully local at runtime;
it does not load a remote web application and the phone state payload
deliberately excludes passwords and access tokens. The settings page can ask
Android to share an in-memory redacted diagnostic report; the report omits the
full server address, account, media names, Quick Connect code, credentials, and
response bodies.

The device page keeps connection status and the touchpad entry; display controls
live in Settings. The settings account card opens the server/account list.
Android retains validated logins, supports multiple users per server, and keeps
the current connection active while another login is attempted. The browser
preview uses account metadata only; the dual-UI harness keeps real development
sessions only in memory.

Settings groups appearance and haptics, glasses output, and About/Help. Theme
and display controls expand in place; closing display controls ends the eye
reference overlay. About shows the installed APK version and build number, with
public project, issue and guide links opened in the system browser. Preview
version information comes from the root `version.properties` during Vite builds.

Liquid phone backgrounds can be selected, replaced and reset under Appearance.
Android imports a single JPG/PNG/WebP through the system picker into a private
resized JPEG, preserving image orientation and omitting source metadata. Neither
the original URI nor image bytes enter the bridge, diagnostics or glasses UI.
Standalone and dual-UI browser previews use IndexedDB for their own image. The
background survives reloads and theme changes; simpleUI and the touchpad do not
render it. Restoring all preferences also restores the default background.

Remote background is a separate Settings choice: `texture` or `black`, valid in
both themes. Until explicitly selected, Liquid defaults to texture and simpleUI
to black. Android persists the choice in `SessionRepository` and acknowledges
it through phone state only; it never republishes the glasses bootstrap. Black
mode removes ambient art, grain, glow and decorative rings, and sets every
background layer and native system bar to opaque zero RGB. Foreground controls
remain visible; the OS keyboard and overlays are outside this background setting.

Account regression tests for the development harness run without a browser:

```bash
node --test ../DevHarness/*.test.mjs
```
