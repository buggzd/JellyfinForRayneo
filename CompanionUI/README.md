# Embedded companion UI

This frontend is the phone-side interface packaged into the Android APK. It is
based on the supplied Luma Link presentation template, while its data and
actions are provided by `JellyfinRayNeoActivity` through the local
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
`Assets/StreamingAssets/CompanionUI`. The UI is fully local at runtime; it does
not load a remote web application and the native state payload deliberately
excludes credentials and access tokens.
