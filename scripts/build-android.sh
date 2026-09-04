#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly BUILD_VARIANT="${1:-debug}"

case "${BUILD_VARIANT}" in
    debug|release|all)
        ;;
    *)
        echo "Usage: $0 [debug|release|all]" >&2
        exit 2
        ;;
esac

if [[ -z "${ANDROID_HOME:-}" \
        && -z "${ANDROID_SDK_ROOT:-}" \
        && ! -f "${PROJECT_DIR}/AndroidApp/local.properties" ]]; then
    echo "Android SDK not found. Set ANDROID_HOME/ANDROID_SDK_ROOT or create AndroidApp/local.properties." >&2
    exit 1
fi

cd "${PROJECT_DIR}"
"${SCRIPT_DIR}/install-rayneo-sdk.sh"
npm --prefix GlassesUI ci
npm --prefix CompanionUI ci
npm --prefix GlassesUI run check
npm --prefix GlassesUI run build
npm --prefix CompanionUI run build
cd AndroidApp

case "${BUILD_VARIANT}" in
    debug)
        ./gradlew :app:testDebugUnitTest :app:lintDebug :app:assembleDebug
        ;;
    release)
        ./gradlew :app:testDebugUnitTest :app:lintRelease :app:assembleRelease
        ;;
    all)
        ./gradlew \
            :app:testDebugUnitTest \
            :app:lintDebug \
            :app:lintRelease \
            :app:assembleDebug \
            :app:assembleRelease
        ;;
esac

if [[ "${BUILD_VARIANT}" == debug || "${BUILD_VARIANT}" == all ]]; then
    "${SCRIPT_DIR}/verify-no-unity.sh" --apk-only \
        "AndroidApp/app/build/outputs/apk/debug/app-debug.apk"
fi

if [[ "${BUILD_VARIANT}" == release || "${BUILD_VARIANT}" == all ]]; then
    release_apk="AndroidApp/app/build/outputs/apk/release/app-release-unsigned.apk"
    if [[ -f "${PROJECT_DIR}/AndroidApp/keystore.properties" \
            || ( -n "${ANDROID_KEYSTORE_PATH:-}" \
                && -n "${ANDROID_KEYSTORE_PASSWORD:-}" \
                && -n "${ANDROID_KEY_ALIAS:-}" \
                && -n "${ANDROID_KEY_PASSWORD:-}" ) ]]; then
        release_apk="AndroidApp/app/build/outputs/apk/release/app-release.apk"
    fi
    "${SCRIPT_DIR}/verify-no-unity.sh" --apk-only "${release_apk}"
fi
