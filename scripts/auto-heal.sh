#!/bin/bash
# =============================================
# IVF Auto-Heal Script
# Monitors Docker Swarm services and automatically
# restarts unhealthy containers. Run via cron every 2 min.
# Usage: ./scripts/auto-heal.sh [--dry-run] [--verbose]
# Cron:  */2 * * * * /opt/ivf/scripts/auto-heal.sh >> /var/log/ivf-autoheal.log 2>&1
# =============================================

set -euo pipefail

DRY_RUN=false
VERBOSE=false
LOG_PREFIX="[auto-heal]"
SERVICES=("ivf_api" "ivf_db" "ivf_redis" "ivf_minio" "ivf_caddy")
MAX_RESTARTS=5           # Max restarts before alerting
RESTART_WINDOW=3600      # 1 hour window for restart counting
RESTART_COUNT_FILE="/tmp/ivf-autoheal-counts"
WEBHOOK_URL="${AUTOHEAL_WEBHOOK_URL:-}"

for arg in "$@"; do
    case $arg in
        --dry-run) DRY_RUN=true ;;
        --verbose) VERBOSE=true ;;
    esac
done

log() { echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') $LOG_PREFIX $1"; }
log_verbose() { [[ "$VERBOSE" == true ]] && log "$1" || true; }

send_alert() {
    local message="$1"
    local severity="${2:-warning}"
    log "ALERT [$severity]: $message"

    if [[ -n "$WEBHOOK_URL" ]]; then
        curl -sf -X POST "$WEBHOOK_URL" \
            -H "Content-Type: application/json" \
            -d "{\"text\":\"$LOG_PREFIX $message\",\"severity\":\"$severity\"}" \
            --max-time 5 2>/dev/null || true
    fi
}

get_restart_count() {
    local service="$1"
    if [[ -f "$RESTART_COUNT_FILE" ]]; then
        local entry
        entry=$(grep "^$service:" "$RESTART_COUNT_FILE" 2>/dev/null || echo "")
        if [[ -n "$entry" ]]; then
            local timestamp count
            timestamp=$(echo "$entry" | cut -d: -f2)
            count=$(echo "$entry" | cut -d: -f3)
            local now
            now=$(date +%s)
            if (( now - timestamp < RESTART_WINDOW )); then
                echo "$count"
                return
            fi
        fi
    fi
    echo "0"
}

increment_restart_count() {
    local service="$1"
    local now
    now=$(date +%s)
    local current_count
    current_count=$(get_restart_count "$service")
    local new_count=$((current_count + 1))

    if [[ -f "$RESTART_COUNT_FILE" ]]; then
        grep -v "^$service:" "$RESTART_COUNT_FILE" > "${RESTART_COUNT_FILE}.tmp" 2>/dev/null || true
        mv "${RESTART_COUNT_FILE}.tmp" "$RESTART_COUNT_FILE"
    fi
    echo "$service:$now:$new_count" >> "$RESTART_COUNT_FILE"
}

# ── Check Docker Swarm services ──────────────────────

healed=0
failed=0

log_verbose "Starting auto-heal check..."

for service in "${SERVICES[@]}"; do
    # Check if service exists
    if ! docker service inspect "$service" &>/dev/null; then
        log_verbose "Service $service not found — skipping"
        continue
    fi

    # Get replicas status
    replicas=$(docker service ls --filter "name=$service" --format "{{.Replicas}}" 2>/dev/null || echo "")
    if [[ -z "$replicas" ]]; then
        continue
    fi

    current=$(echo "$replicas" | cut -d/ -f1)
    desired=$(echo "$replicas" | cut -d/ -f2)

    if [[ "$current" == "$desired" ]]; then
        log_verbose "✓ $service: $current/$desired replicas healthy"
        continue
    fi

    log "⚠ $service: $current/$desired replicas — unhealthy"

    # Check restart count
    restart_count=$(get_restart_count "$service")
    if (( restart_count >= MAX_RESTARTS )); then
        send_alert "$service exceeded max restarts ($MAX_RESTARTS in ${RESTART_WINDOW}s) — manual intervention needed" "critical"
        ((failed++))
        continue
    fi

    # Restart the service
    if [[ "$DRY_RUN" == true ]]; then
        log "DRY-RUN: Would force-update $service"
    else
        log "Restarting $service (attempt $((restart_count + 1))/$MAX_RESTARTS)..."
        if docker service update --force "$service" &>/dev/null; then
            increment_restart_count "$service"
            send_alert "$service was auto-healed (restart $((restart_count + 1)))" "info"
            ((healed++))
        else
            send_alert "Failed to restart $service" "critical"
            ((failed++))
        fi
    fi
done

# ── Check for unhealthy containers (non-Swarm) ──────

unhealthy_containers=$(docker ps --filter "health=unhealthy" --format "{{.Names}}" 2>/dev/null || echo "")
if [[ -n "$unhealthy_containers" ]]; then
    while IFS= read -r container; do
        log "⚠ Container $container is unhealthy"

        restart_count=$(get_restart_count "$container")
        if (( restart_count >= MAX_RESTARTS )); then
            send_alert "Container $container exceeded max restarts" "critical"
            ((failed++))
            continue
        fi

        if [[ "$DRY_RUN" == true ]]; then
            log "DRY-RUN: Would restart container $container"
        else
            log "Restarting container $container..."
            if docker restart "$container" &>/dev/null; then
                increment_restart_count "$container"
                send_alert "Container $container was auto-healed" "info"
                ((healed++))
            else
                send_alert "Failed to restart container $container" "critical"
                ((failed++))
            fi
        fi
    done <<< "$unhealthy_containers"
fi

# ── Summary ──────────────────────────────────────────

if (( healed > 0 || failed > 0 )); then
    log "Summary: healed=$healed, failed=$failed"
fi

exit $((failed > 0 ? 1 : 0))
