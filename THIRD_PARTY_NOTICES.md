# Third-Party Notices

## LibVLC

The optional Android hardware-preferred container compatibility path and
software-decoding fallback use VideoLAN LibVLC `3.7.0-beta`, licensed under the
GNU Lesser General Public License, version 2.1 or later.

- Project: <https://www.videolan.org/vlc/libvlc.html>
- Source: <https://code.videolan.org/videolan/vlc/-/tree/3.0.x>
- License: <https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html>

The repository does not redistribute LibVLC binaries. The verified installer
downloads the official `VideoLAN.LibVLC.Android` NuGet package and copies the
ARM64 shared libraries into the local Unity project. Applications distributed
with those libraries must retain this notice and satisfy the LGPL, including
allowing recipients to replace the dynamically linked library.
