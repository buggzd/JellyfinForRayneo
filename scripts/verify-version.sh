#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly VERSION_FILE="${PROJECT_DIR}/version.properties"
readonly RELEASE_TAG="${1:-}"

fail()
{
    echo "Version verification failed: $1" >&2
    exit 1
}

property()
{
    local key="$1"
    awk -F= -v requested_key="${key}" \
        '$1 == requested_key { sub(/^[^=]*=/, ""); print }' "${VERSION_FILE}"
}

for command_name in awk git node; do
    command -v "${command_name}" >/dev/null 2>&1 \
        || fail "missing required command: ${command_name}"
done

[[ -f "${VERSION_FILE}" ]] || fail "version.properties is missing"

version_name="$(property versionName)"
version_code="$(property versionCode)"
[[ "$(awk -F= '$1 == "versionName" { count++ } END { print count + 0 }' "${VERSION_FILE}")" == 1 ]] \
    || fail "versionName must appear exactly once"
[[ "$(awk -F= '$1 == "versionCode" { count++ } END { print count + 0 }' "${VERSION_FILE}")" == 1 ]] \
    || fail "versionCode must appear exactly once"
[[ "${version_name}" =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?$ ]] \
    || fail "versionName must follow SemVer without build metadata"
[[ "${version_code}" =~ ^[1-9][0-9]*$ ]] \
    || fail "versionCode must be a positive integer"
(( version_code <= 2147483647 )) || fail "versionCode exceeds the Android integer limit"

for manifest in \
        "CompanionUI/package.json" \
        "CompanionUI/package-lock.json" \
        "GlassesUI/package.json" \
        "GlassesUI/package-lock.json"; do
    manifest_version="$(node -e \
        'const value=require(process.argv[1]); process.stdout.write(String(value.version || ""))' \
        "${PROJECT_DIR}/${manifest}")"
    [[ "${manifest_version}" == "${version_name}" ]] \
        || fail "${manifest} has ${manifest_version:-no version}, expected ${version_name}"
done

if [[ -n "${RELEASE_TAG}" ]]; then
    expected_tag="v${version_name}"
    [[ "${RELEASE_TAG}" == "${expected_tag}" ]] \
        || fail "tag ${RELEASE_TAG} does not match ${expected_tag}"
    git -C "${PROJECT_DIR}" rev-parse --verify --quiet "refs/tags/${RELEASE_TAG}" >/dev/null \
        || fail "tag ${RELEASE_TAG} is not present in the checkout"
    [[ "$(git -C "${PROJECT_DIR}" cat-file -t "refs/tags/${RELEASE_TAG}")" == tag ]] \
        || fail "release tags must be annotated"
    [[ "$(git -C "${PROJECT_DIR}" rev-list -n 1 "${RELEASE_TAG}")" \
        == "$(git -C "${PROJECT_DIR}" rev-parse HEAD)" ]] \
        || fail "release tag must point at the checked-out commit"
    if git -C "${PROJECT_DIR}" show-ref --verify --quiet refs/remotes/origin/main; then
        git -C "${PROJECT_DIR}" merge-base --is-ancestor HEAD refs/remotes/origin/main \
            || fail "release tag must point to a commit reachable from origin/main"
    fi

    previous_maximum=0
    while IFS= read -r prior_tag; do
        [[ -z "${prior_tag}" || "${prior_tag}" == "${RELEASE_TAG}" ]] && continue
        prior_source="$(git -C "${PROJECT_DIR}" show "${prior_tag}:version.properties" 2>/dev/null || true)"
        prior_code="$(printf '%s\n' "${prior_source}" \
            | awk -F= '$1 == "versionCode" { print $2; exit }')"
        if [[ "${prior_code}" =~ ^[1-9][0-9]*$ ]] \
                && (( prior_code > previous_maximum )); then
            previous_maximum="${prior_code}"
        fi
    done < <(git -C "${PROJECT_DIR}" tag --list 'v*' --sort=version:refname)
    (( version_code > previous_maximum )) \
        || fail "versionCode ${version_code} must exceed previous release code ${previous_maximum}"
fi

echo "Version verification passed: ${version_name} (${version_code})${RELEASE_TAG:+ for ${RELEASE_TAG}}"
