#!/usr/bin/env bash
set -euo pipefail

version="3.7.0-beta"
expected_sha256="7b36d95f3bfe928d89b1d1cffb6b029e45a3379c125db89cdf2c8d8a20a32a64"
package_url="https://api.nuget.org/v3-flatcontainer/videolan.libvlc.android/${version}/videolan.libvlc.android.${version}.nupkg"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "${script_dir}/.." && pwd)"
target_dir="${project_dir}/Assets/Plugins/Android/libs/arm64-v8a"
version_marker="${target_dir}/.libvlc-version"
force=false

if [[ "${1:-}" == "--force" ]]; then
    force=true
fi

if [[ -f "${target_dir}/libvlc.so"
    && -f "${target_dir}/libc++_shared.so"
    && -f "${version_marker}"
    && "$(sed -n '1p' "${version_marker}")" == "${version}"
    && "${force}" == false ]]; then
    echo "LibVLC Android ${version} is already installed. Use --force to replace it."
    exit 0
fi

for command_name in curl unzip; do
    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Missing required command: ${command_name}" >&2
        exit 1
    fi
done

temporary_dir="$(mktemp -d)"
trap 'rm -rf "${temporary_dir}"' EXIT
package_path="${temporary_dir}/libvlc.nupkg"
extracted_dir="${temporary_dir}/extracted"

echo "Downloading VideoLAN.LibVLC.Android ${version}..."
curl -fL --retry 3 --connect-timeout 15 "${package_url}" -o "${package_path}"

if command -v shasum >/dev/null 2>&1; then
    actual_sha256="$(shasum -a 256 "${package_path}" | awk '{print $1}')"
elif command -v sha256sum >/dev/null 2>&1; then
    actual_sha256="$(sha256sum "${package_path}" | awk '{print $1}')"
else
    echo "Missing shasum or sha256sum for package verification." >&2
    exit 1
fi

if [[ "${actual_sha256}" != "${expected_sha256}" ]]; then
    echo "LibVLC package checksum mismatch." >&2
    echo "Expected: ${expected_sha256}" >&2
    echo "Actual:   ${actual_sha256}" >&2
    exit 1
fi

mkdir -p "${extracted_dir}" "${target_dir}"
unzip -q "${package_path}" \
    'build/android-armv8/libvlc.so' \
    'build/android-armv8/libc++_shared.so' \
    -d "${extracted_dir}"
install -m 0644 "${extracted_dir}/build/android-armv8/libvlc.so" "${target_dir}/libvlc.so"
install -m 0644 "${extracted_dir}/build/android-armv8/libc++_shared.so" "${target_dir}/libc++_shared.so"
printf '%s\n' "${version}" > "${version_marker}"

echo "Installed LibVLC software decoder to ${target_dir}."
