#!/usr/bin/env bash
set -euo pipefail

readonly RAYNEO_SDK_VERSION="1.0.3"
readonly RAYNEO_SDK_URL="https://file-down.test.leiniao.com/03/008765713965800010022373251.zip"
readonly RAYNEO_ARCHIVE_MD5="0ae0fb9de5dffae6cb0344535e20c454"
readonly RAYNEO_AAR_SHA256="505551d383db80d7852612e67f9158d4c67382304d22619c796abdc0365f15b6"

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly TARGET_DIR="${PROJECT_DIR}/AndroidApp/app/libs"
readonly TARGET_AAR="${TARGET_DIR}/ffalcon-sdk-client-${RAYNEO_SDK_VERSION}.aar"

force_install=false
if [[ "${1:-}" == "--force" ]]; then
    force_install=true
elif [[ $# -gt 0 ]]; then
    echo "Usage: $0 [--force]" >&2
    exit 2
fi

sha256_file() {
    local file_path="$1"
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "${file_path}" | awk '{print $1}'
    elif command -v sha256sum >/dev/null 2>&1; then
        sha256sum "${file_path}" | awk '{print $1}'
    else
        echo "Missing shasum or sha256sum." >&2
        return 1
    fi
}

md5_file() {
    local file_path="$1"
    if command -v md5 >/dev/null 2>&1; then
        md5 -q "${file_path}"
    elif command -v md5sum >/dev/null 2>&1; then
        md5sum "${file_path}" | awk '{print $1}'
    else
        echo "Missing md5 or md5sum." >&2
        return 1
    fi
}

verify_aar() {
    local file_path="$1"
    [[ -f "${file_path}" ]] \
        && [[ "$(sha256_file "${file_path}")" == "${RAYNEO_AAR_SHA256}" ]]
}

if [[ "${force_install}" == false ]] && verify_aar "${TARGET_AAR}"; then
    echo "RayNeo Android SDK ${RAYNEO_SDK_VERSION} is already installed."
    exit 0
fi

for command_name in curl unzip zipinfo; do
    if ! command -v "${command_name}" >/dev/null 2>&1; then
        echo "Missing required command: ${command_name}" >&2
        exit 1
    fi
done

task_temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/rayneo-android-sdk.XXXXXX")"
trap 'rm -rf -- "${task_temp_dir}"' EXIT
staged_aar="${task_temp_dir}/ffalcon-sdk-client-${RAYNEO_SDK_VERSION}.aar"
archive_path="${task_temp_dir}/rayneo-sdk.zip"
extracted_dir="${task_temp_dir}/extracted"
echo "Downloading verified RayNeo Air SDK ${RAYNEO_SDK_VERSION}..."
curl --fail --location --retry 3 --silent --show-error \
    --output "${archive_path}" "${RAYNEO_SDK_URL}"
if [[ "$(md5_file "${archive_path}")" != "${RAYNEO_ARCHIVE_MD5}" ]]; then
    echo "RayNeo SDK archive checksum mismatch." >&2
    exit 1
fi

mkdir -p "${extracted_dir}"
while IFS= read -r archive_entry; do
    normalized_entry="${archive_entry//\\//}"
    normalized_entry="${normalized_entry#./}"
    case "${normalized_entry}" in
        ""|/*|..|../*|*/../*)
            echo "Unsafe archive entry: ${archive_entry}" >&2
            exit 1
            ;;
    esac
    if [[ "${normalized_entry}" == */ ]]; then
        mkdir -p "${extracted_dir}/${normalized_entry}"
        continue
    fi
    mkdir -p "$(dirname "${extracted_dir}/${normalized_entry}")"
    escaped_entry="${archive_entry//\\/\\\\}"
    unzip -p "${archive_path}" "${escaped_entry}" \
        > "${extracted_dir}/${normalized_entry}"
done < <(zipinfo -1 "${archive_path}")

discovered_aar="$(find "${extracted_dir}" -type f \
    -name "ffalcon-sdk-client-${RAYNEO_SDK_VERSION}.aar" -print -quit)"
if [[ -z "${discovered_aar}" ]]; then
    echo "RayNeo Android client AAR was not found in the verified archive." >&2
    exit 1
fi
cp "${discovered_aar}" "${staged_aar}"

if ! verify_aar "${staged_aar}"; then
    echo "RayNeo Android client AAR checksum mismatch." >&2
    exit 1
fi

mkdir -p "${TARGET_DIR}"
install -m 0644 "${staged_aar}" "${TARGET_AAR}"
echo "Installed RayNeo Android SDK ${RAYNEO_SDK_VERSION} to AndroidApp/app/libs."
