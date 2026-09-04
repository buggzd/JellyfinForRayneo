# Embedded glasses UI

This React frontend is the glasses-side Lucent interface bundled into the
Android APK. Its visual implementation is derived directly from the supplied
`media centertemp` presentation template.

For a browser preview, copy `.jellyfin-dev.example.json` to
`.jellyfin-dev.json`, fill in a development-only Jellyfin account, then run:

```bash
npm install
npm run dev
```

Only the Vite development server exposes that local JSON at
`/__jellyfin-dev-config`. Production builds never read or bundle the file.

Rebuild the APK assets after changing the frontend:

```bash
npm run build
```

The generated production output is written to
`AndroidApp/app/src/main/assets/GlassesUI` and loaded locally by the one glasses
WebView inside Android's external-display `Presentation`. In stereo mode the
native `StereoMirrorLayout` draws that same WebView frame into both SBS halves;
do not create a second player WebView.

Native bootstrap values are normalized and deduplicated before notifying
React. Catalog loading is keyed by the session and bounded
`catalogGeneration`, so unrelated display-state updates cannot continuously
cancel and restart the Jellyfin home requests.
