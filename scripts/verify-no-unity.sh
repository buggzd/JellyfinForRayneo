#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

for command_name in awk git rg strings unzip wc; do
    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Missing required command: ${command_name}" >&2
        exit 1
    fi
done

apk_only=false
apk_path=""

for argument in "$@"; do
    case "${argument}" in
        --apk-only)
            apk_only=true
            ;;
        -* )
            echo "Usage: $0 [--apk-only] [path-to-apk]" >&2
            exit 2
            ;;
        *)
            if [[ -n "${apk_path}" ]]; then
                echo "Usage: $0 [--apk-only] [path-to-apk]" >&2
                exit 2
            fi
            apk_path="${argument}"
            ;;
    esac
done

if [[ -z "${apk_path}" ]]; then
    apk_path="${PROJECT_DIR}/AndroidApp/app/build/outputs/apk/debug/app-debug.apk"
elif [[ "${apk_path}" != /* ]]; then
    apk_path="${PROJECT_DIR}/${apk_path}"
fi

failures=0
fail()
{
    echo "ERROR: $1" >&2
    failures=$((failures + 1))
}

if [[ "${apk_only}" == false ]]; then
    for legacy_path in Assets Packages ProjectSettings; do
        if [[ -e "${PROJECT_DIR}/${legacy_path}" ]]; then
            fail "legacy Unity path still exists: ${legacy_path}/"
        fi
    done

    if [[ -e "${PROJECT_DIR}/scripts/install-libvlc-android.sh" ]]; then
        fail "legacy LibVLC installer still exists"
    fi

    if rg -n \
            'com\.unity3d|UnityPlayer|UnitySendMessage|UnityXRSupportActivity|org\.videolan|libvlc' \
            "${PROJECT_DIR}/AndroidApp" \
            "${PROJECT_DIR}/GlassesUI/src" \
            "${PROJECT_DIR}/CompanionUI/src" >/dev/null; then
        fail "native application source still references a Unity or LibVLC runtime"
    fi

    glasses_webview_count="$(
        { rg -o 'new WebView\(' \
            "${PROJECT_DIR}/AndroidApp/app/src/main/java/com/jellyfinforrayneo/client/GlassesWebViewController.java" \
            || true; } \
        | wc -l \
        | awk '{print $1}'
    )"
    if [[ "${glasses_webview_count}" != 1 ]]; then
        fail "glasses host must construct exactly one WebView"
    fi

    tracked_binaries="$(git -C "${PROJECT_DIR}" ls-files \
        '*.apk' '*.aab' '*.aar' '*.jks' '*.keystore')"
    if [[ -n "${tracked_binaries}" ]]; then
        fail "generated SDK, application, or signing binaries are tracked"
    fi
fi

if [[ ! -f "${apk_path}" ]]; then
    fail "APK not found: ${apk_path}"
else
    apk_entries="$(unzip -Z1 "${apk_path}")"

    if printf '%s\n' "${apk_entries}" | rg -i \
            '(^|/)(libunity\.so|libvlc\.so|globalgamemanagers|sharedassets[0-9]*\.|level[0-9]+$|assets/bin/Data/|\.unity3d$)' >/dev/null; then
        fail "APK contains a Unity or LibVLC runtime artifact"
    fi

    if ! printf '%s\n' "${apk_entries}" | rg -x \
            'assets/GlassesUI/index\.html' >/dev/null; then
        fail "APK is missing the glasses production bundle"
    fi
    if ! printf '%s\n' "${apk_entries}" | rg -x \
            'assets/CompanionUI/index\.html' >/dev/null; then
        fail "APK is missing the companion production bundle"
    fi

    if printf '%s\n' "${apk_entries}" | rg -i \
            '(^|/)(\.jellyfin-dev\.json|keystore\.properties|[^/]+\.(jks|keystore))$' >/dev/null; then
        fail "APK contains development credentials or signing material"
    fi

    if printf '%s\n' "${apk_entries}" \
            | rg '^lib/' \
            | rg -v '^lib/arm64-v8a/' >/dev/null; then
        fail "APK contains a non-ARM64 native library"
    fi

    dex_strings="$(unzip -p "${apk_path}" 'classes*.dex' | strings)"
    if printf '%s\n' "${dex_strings}" | rg \
            '(^Lcom/unity3d/|^Lcom/tcl/unity/unityadapter/|UnityPlayer|UnitySendMessage|^Lorg/videolan/|libvlc)' >/dev/null; then
        fail "APK DEX contains a Unity adapter, Unity Player, or LibVLC class"
    fi

    web_strings="$(unzip -p "${apk_path}" \
        'assets/GlassesUI/*' 'assets/CompanionUI/*' | strings)"
    if printf '%s\n' "${web_strings}" | rg \
            '(^|[^0-9])(10\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}|127\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}|169\.254\.[0-9]{1,3}\.[0-9]{1,3}|192\.168\.[0-9]{1,3}\.[0-9]{1,3}|172\.(1[6-9]|2[0-9]|3[01])\.[0-9]{1,3}\.[0-9]{1,3})([^0-9]|$)' >/dev/null; then
        fail "APK Web assets contain a private IPv4 address"
    fi
    if printf '%s\n' "${web_strings}" | rg \
            '__jellyfin-dev-config|\.jellyfin-dev\.json' >/dev/null; then
        fail "APK Web assets contain the development Jellyfin configuration path"
    fi
fi

if (( failures > 0 )); then
    echo "No-Unity verification failed with ${failures} error(s)." >&2
    exit 1
fi

echo "No-Unity verification passed: ${apk_path}"
wc -c < "${apk_path}" | awk '{printf "APK size: %.1f MiB\n", $1 / 1048576}'
