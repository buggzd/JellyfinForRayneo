# Third-Party Notices

## RayNeo hardware protocol references

The application controls the verified Air 3s USB display interface directly
through Android USB Host APIs. It does not load, bundle or bind to the RayNeo
Air SDK or XR Space application.

- Documentation: <https://rayneo.gitbook.io/rayneo-devdoc/>
- Historical SDK reference: the pinned download in `scripts/install-rayneo-sdk.sh`
- Protocol analysis and scope: `docs/SBS_GEOMETRY_ANALYSIS.md`

Vendor binaries are not redistributed in this repository or included in the
current APK. The historical SDK download helper remains for reproducible
analysis and is not required by the build.

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

## RayNeo product image

`CompanionUI/public/art/rayneo-air-3s.webp` is cropped and resized from the
RayNeo Air 3S official product artwork supplied by the user. It preserves the
product's original appearance and transparency. The product artwork and RayNeo
marks remain the property of their respective owners and are not covered by
this repository's MIT license.
