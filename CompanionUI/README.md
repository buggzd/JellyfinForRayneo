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

The background editor saves non-destructive crop metadata: transparency (0–100,
default 65), aspect preset (screen, 9:16, 3:4, 1:1 or original), zoom (100–300%)
and normalized X/Y position (0–1000). Drag, arrow keys and sliders adjust the
same source-image crop used by the wallpaper renderer. The interface preview
shows the crop filling the phone viewport. Apply waits for persistence; cancel
or Android Back preserves the previous layout and restores focus to the opener.
Replacing an image resets its crop while retaining transparency. Resetting the
background clears both image and layout. Android owns these preferences in
`SessionRepository`; the bridge rejects invalid values and stale image revisions,
and publishes only phone state. Browser previews atomically save their image and
layout together in IndexedDB, including support for older image-only records.

Remote background is a separate Settings choice: `texture` or `black`, valid in
both themes. Until explicitly selected, Liquid defaults to texture and simpleUI
to black. Android persists the choice in `SessionRepository` and acknowledges
it through phone state only; it never republishes the glasses bootstrap. Black
mode removes ambient art, grain, glow and decorative rings, and sets every
background layer and native system bar to opaque zero RGB. Foreground controls
remain visible; the OS keyboard and overlays are outside this background setting.

Crop and account regression tests run without a browser:

```bash
npm test
node --test ../DevHarness/*.test.mjs
```
