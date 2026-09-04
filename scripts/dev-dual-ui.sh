#!/usr/bin/env bash
set -euo pipefail

readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly COMPANION_URL="http://127.0.0.1:4176/"
readonly GLASSES_URL="http://127.0.0.1:4175/"
readonly HARNESS_URL="http://127.0.0.1:4177/"

declare -a child_pids=()

cleanup()
{
    trap - EXIT INT TERM
    for child_pid in "${child_pids[@]}"; do
        if kill -0 "${child_pid}" 2>/dev/null; then
            kill "${child_pid}" 2>/dev/null || true
        fi
    done
    for child_pid in "${child_pids[@]}"; do
        wait "${child_pid}" 2>/dev/null || true
    done
}

trap cleanup EXIT INT TERM

wait_for_service()
{
    local url="$1"
    local process_id="$2"
    local label="$3"
    local attempt
    for attempt in {1..120}; do
        if ! kill -0 "${process_id}" 2>/dev/null; then
            wait "${process_id}" || true
            echo "${label} 启动失败。" >&2
            exit 1
        fi
        if curl --fail --silent --show-error --max-time 1 "${url}" >/dev/null 2>&1; then
            return
        fi
        sleep 0.25
    done
    echo "等待 ${label} 超时。" >&2
    exit 1
}

cd "${PROJECT_DIR}"

if [[ ! -x "GlassesUI/node_modules/.bin/vite" \
        || ! -x "CompanionUI/node_modules/.bin/vite" ]]; then
    echo "前端依赖尚未安装，请先运行：" >&2
    echo "npm --prefix GlassesUI ci && npm --prefix CompanionUI ci" >&2
    exit 1
fi

(
    cd GlassesUI
    exec ./node_modules/.bin/vite --host 127.0.0.1 --port 4175 --strictPort
) &
child_pids+=("$!")
readonly GLASSES_PID="${child_pids[0]}"

(
    cd CompanionUI
    exec ./node_modules/.bin/vite --host 127.0.0.1 --port 4176 --strictPort
) &
child_pids+=("$!")
readonly COMPANION_PID="${child_pids[1]}"

node DevHarness/server.mjs &
child_pids+=("$!")
readonly HARNESS_PID="${child_pids[2]}"

wait_for_service "${GLASSES_URL}" "${GLASSES_PID}" "眼镜端 Vite"
wait_for_service "${COMPANION_URL}" "${COMPANION_PID}" "手机端 Vite"
wait_for_service "${HARNESS_URL}health" "${HARNESS_PID}" "双端联调页"

echo
echo "双端联调已就绪：${HARNESS_URL}"
echo "左侧为 CompanionUI，右侧为 GlassesUI；按 Ctrl-C 同时停止三个服务。"

if [[ "${RAYNEO_DUAL_UI_NO_OPEN:-0}" != "1" ]]; then
    if command -v open >/dev/null 2>&1; then
        open "${HARNESS_URL}"
    elif command -v xdg-open >/dev/null 2>&1; then
        xdg-open "${HARNESS_URL}" >/dev/null 2>&1 || true
    fi
fi

while true; do
    for child_pid in "${child_pids[@]}"; do
        if ! kill -0 "${child_pid}" 2>/dev/null; then
            wait "${child_pid}" || true
            echo "联调子进程意外退出，正在停止其余服务。" >&2
            exit 1
        fi
    done
    sleep 1
done
