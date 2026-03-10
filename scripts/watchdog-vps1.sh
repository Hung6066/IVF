#!/bin/bash
# =============================================
# IVF Health Watchdog — Chạy trên VPS2 qua cron
#
# Theo dõi VPS1. Nếu VPS1 down liên tục → tự động kích hoạt failover.
#
# Cài trên VPS2:
#   scp scripts/watchdog-vps1.sh root@194.163.181.19:/opt/ivf/scripts/
#   ssh root@194.163.181.19 "chmod +x /opt/ivf/scripts/watchdog-vps1.sh"
#
# Thêm vào crontab trên VPS2:
#   crontab -e
#   */2 * * * * /opt/ivf/scripts/watchdog-vps1.sh >> /var/log/ivf-watchdog.log 2>&1
# =============================================

set -euo pipefail

VPS1_HEALTH_URL="https://natra.site/api/health/live"
FAILOVER_SCRIPT="/opt/ivf/scripts/failover-manager.sh"
LOCK_FILE="/tmp/ivf-failover.lock"
FAIL_COUNT_FILE="/tmp/ivf-vps1-fail-count"
FAIL_THRESHOLD=3       # Số lần fail liên tiếp trước khi trigger failover
RECOVERY_URL="https://natra.site/api/health/live"
DISCORD_WEBHOOK="${DISCORD_WEBHOOK_URL:-}"
LOG="/var/log/ivf-watchdog.log"

log() { echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') [WATCHDOG] $1" | tee -a "$LOG"; }
alert() {
    if [[ -n "$DISCORD_WEBHOOK" ]]; then
        curl -s -X POST "$DISCORD_WEBHOOK" \
            -H "Content-Type: application/json" \
            -d "{\"content\":\"$1\"}" >/dev/null 2>&1 || true
    fi
}

# Đọc/ghi fail count từ file (persist qua các lần chạy cron)
get_fail_count() { cat "$FAIL_COUNT_FILE" 2>/dev/null || echo "0"; }
set_fail_count() { echo "$1" > "$FAIL_COUNT_FILE"; }
reset_fail_count() { echo "0" > "$FAIL_COUNT_FILE"; }

# Kiểm tra health endpoint
check_health() {
    local http_code
    http_code=$(curl -sk --connect-timeout 8 --max-time 12 \
        -o /dev/null -w "%{http_code}" "$VPS1_HEALTH_URL" 2>/dev/null || echo "000")
    echo "$http_code"
}

# ─── Main ───
main() {
    # Nếu failover đang chạy, bỏ qua
    if [[ -f "$LOCK_FILE" ]]; then
        local lock_age=$(( $(date +%s) - $(stat -c %Y "$LOCK_FILE" 2>/dev/null || date +%s) ))
        if (( lock_age < 600 )); then  # 10 phút
            log "Failover đang chạy (lock file tồn tại ${lock_age}s). Bỏ qua."
            exit 0
        else
            log "Lock file cũ (${lock_age}s). Xóa và tiếp tục..."
            rm -f "$LOCK_FILE"
        fi
    fi

    local http_code
    http_code=$(check_health)

    if [[ "$http_code" == "200" ]]; then
        # VPS1 healthy
        local prev_count
        prev_count=$(get_fail_count)
        if (( prev_count > 0 )); then
            log "VPS1 đã phục hồi (code $http_code). Reset fail count từ $prev_count → 0."
            alert "✅ VPS1 đã phục hồi. Hệ thống IVF hoạt động bình thường."
        fi
        reset_fail_count
        exit 0
    fi

    # VPS1 không healthy
    local fail_count
    fail_count=$(get_fail_count)
    fail_count=$(( fail_count + 1 ))
    set_fail_count "$fail_count"

    log "VPS1 không healthy (HTTP $http_code). Fail count: $fail_count/$FAIL_THRESHOLD"

    if (( fail_count == 1 )); then
        alert "⚠️ IVF: VPS1 không phản hồi (lần 1/$FAIL_THRESHOLD). Theo dõi..."
    fi

    if (( fail_count >= FAIL_THRESHOLD )); then
        log "VPS1 đã fail $fail_count lần liên tiếp. Kích hoạt failover!"
        alert "🚨 IVF FAILOVER: VPS1 down $fail_count lần liên tiếp. Chuyển sang VPS2..."

        if [[ -f "$FAILOVER_SCRIPT" ]]; then
            touch "$LOCK_FILE"
            bash "$FAILOVER_SCRIPT" >> "$LOG" 2>&1 || {
                log "ERROR: Failover script thất bại!"
                alert "❌ Failover script thất bại! Cần can thiệp thủ công."
                rm -f "$LOCK_FILE"
            }
            rm -f "$LOCK_FILE"
            reset_fail_count
        else
            log "ERROR: Không tìm thấy failover script tại $FAILOVER_SCRIPT"
            alert "❌ Failover script không tồn tại tại $FAILOVER_SCRIPT! Can thiệp thủ công!"
        fi
    fi
}

main "$@"
