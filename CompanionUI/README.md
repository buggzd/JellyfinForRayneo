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
live in Settings. Both settings management entries open the server/account list.
Android retains validated logins, supports multiple users per server, and keeps
the current connection active while another login is attempted. The browser
preview uses account metadata only; the dual-UI harness keeps real development
sessions only in memory.

Account regression tests for the development harness run without a browser:

```bash
node --test ../DevHarness/*.test.mjs
```
