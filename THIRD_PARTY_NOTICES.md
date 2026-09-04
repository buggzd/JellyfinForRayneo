# Third-Party Notices

## RayNeo Air Android SDK

The application integrates RayNeo's `ffalcon-sdk-client` version `1.0.3` to
communicate with supported glasses and switch display modes.

- Documentation: <https://rayneo.gitbook.io/rayneo-devdoc/>
- Binary source: RayNeo's official SDK download endpoint used by
  `scripts/install-rayneo-sdk.sh`
- License: vendor terms supplied by RayNeo/FFALCON

The SDK binary is not redistributed in this repository. The installer pins the
download archive and extracted AAR checksums. Confirm the vendor's current
distribution terms before publishing an APK.

## Web application dependencies

The embedded frontends use open-source packages recorded in their npm lockfiles,
including:

- React and React DOM — MIT License
- Vite and `@vitejs/plugin-react` — MIT License
- hls.js — Apache License 2.0
- Lucide React — ISC License
- TypeScript — Apache License 2.0

Transitive packages and exact resolved versions are listed in
`GlassesUI/package-lock.json` and `CompanionUI/package-lock.json`.

## Jellyfin

Jellyfin names and trademarks belong to their respective owners. This
third-party client communicates with Jellyfin through its public API and does
not redistribute Jellyfin server software.

The application does not embed or link LibVLC, Google Cardboard, or a Unity
runtime.
