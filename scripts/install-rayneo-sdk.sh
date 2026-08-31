#!/usr/bin/env bash
set -euo pipefail

readonly RAYNEO_SDK_URL="https://file-down.test.leiniao.com/03/008765713965800010022373251.zip"
readonly RAYNEO_SDK_MD5="0ae0fb9de5dffae6cb0344535e20c454"
readonly CARDBOARD_URL="https://file-down.api.leiniao.com/54/006292446263300010043424250.0.3.zip"
readonly CARDBOARD_MD5="fddf7e51544a4e43201f90c499fef428"

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
readonly PACKAGES_DIR="$PROJECT_DIR/Packages"
readonly RAYNEO_PACKAGE_DIR="$PACKAGES_DIR/com.ffalcon.plugin.xr"
readonly CARDBOARD_PACKAGE_DIR="$PACKAGES_DIR/com.google.xr.cardboard"

force_install=false
if [[ "${1:-}" == "--force" ]]; then
  force_install=true
elif [[ $# -gt 0 ]]; then
  echo "Usage: $0 [--force]" >&2
  exit 2
fi

if [[ "$force_install" != true
      && -f "$RAYNEO_PACKAGE_DIR/package.json"
      && -f "$CARDBOARD_PACKAGE_DIR/package.json" ]]; then
  echo "RayNeo Air SDK and Cardboard XR Plugin are already installed."
  exit 0
fi

for command_name in curl unzip zipinfo; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Missing required command: $command_name" >&2
    exit 1
  fi
done

task_temp_dir="$(mktemp -d "${TMPDIR:-/tmp}/rayneo-sdk.XXXXXX")"
trap 'rm -rf -- "$task_temp_dir"' EXIT

checksum_md5() {
  local file_path="$1"
  if command -v md5 >/dev/null 2>&1; then
    md5 -q "$file_path"
  elif command -v md5sum >/dev/null 2>&1; then
    md5sum "$file_path" | awk '{print $1}'
  else
    echo "No MD5 utility is available." >&2
    return 1
  fi
}

download_verified() {
  local source_url="$1"
  local expected_md5="$2"
  local destination="$3"

  curl --fail --location --retry 3 --silent --show-error \
    --output "$destination" "$source_url"

  local actual_md5
  actual_md5="$(checksum_md5 "$destination" | tr '[:upper:]' '[:lower:]')"
  if [[ "$actual_md5" != "$expected_md5" ]]; then
    echo "Checksum mismatch for $source_url" >&2
    echo "Expected: $expected_md5" >&2
    echo "Actual:   $actual_md5" >&2
    exit 1
  fi
}

# RayNeo's ZIPs contain a mixture of Windows and POSIX separators. Extract each
# entry while normalizing separators and rejecting path traversal.
extract_normalized() {
  local archive_path="$1"
  local destination_dir="$2"
  local archive_entry
  local normalized_entry
  local escaped_entry

  mkdir -p "$destination_dir"
  while IFS= read -r archive_entry; do
    normalized_entry="${archive_entry//\\//}"
    normalized_entry="${normalized_entry#./}"

    case "$normalized_entry" in
      ""|/*|..|../*|*/../*)
        echo "Unsafe archive entry: $archive_entry" >&2
        exit 1
        ;;
    esac

    if [[ "$normalized_entry" == */ ]]; then
      mkdir -p "$destination_dir/$normalized_entry"
      continue
    fi

    mkdir -p "$(dirname "$destination_dir/$normalized_entry")"
    escaped_entry="${archive_entry//\\/\\\\}"
    unzip -p "$archive_path" "$escaped_entry" > "$destination_dir/$normalized_entry"
  done < <(zipinfo -1 "$archive_path")
}

install_package() {
  local staged_package="$1"
  local package_name="$2"
  local destination="$PACKAGES_DIR/$package_name"

  if [[ -e "$destination" ]]; then
    if [[ "$force_install" != true ]]; then
      echo "$package_name is already installed. Use --force to replace it."
      return
    fi
    rm -rf -- "$destination"
  fi

  mv "$staged_package" "$destination"
  echo "Installed $package_name"
}

rayneo_archive="$task_temp_dir/rayneo-sdk.zip"
cardboard_archive="$task_temp_dir/cardboard.zip"

echo "Downloading verified RayNeo Air SDK v1.0.3..."
download_verified "$RAYNEO_SDK_URL" "$RAYNEO_SDK_MD5" "$rayneo_archive"
download_verified "$CARDBOARD_URL" "$CARDBOARD_MD5" "$cardboard_archive"

extract_normalized "$rayneo_archive" "$task_temp_dir/com.ffalcon.plugin.xr"
extract_normalized "$cardboard_archive" "$task_temp_dir/cardboard-staging"

if [[ ! -f "$task_temp_dir/com.ffalcon.plugin.xr/package.json" ]]; then
  echo "RayNeo package.json was not found after extraction." >&2
  exit 1
fi
if [[ ! -f "$task_temp_dir/cardboard-staging/cardboard-xr-plugin/package.json" ]]; then
  echo "Cardboard package.json was not found after extraction." >&2
  exit 1
fi

install_package "$task_temp_dir/com.ffalcon.plugin.xr" "com.ffalcon.plugin.xr"
install_package "$task_temp_dir/cardboard-staging/cardboard-xr-plugin" "com.google.xr.cardboard"

echo "RayNeo dependencies are ready. Reopen Unity or let Package Manager refresh."
