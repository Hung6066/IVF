#!/bin/bash
# =============================================
# IVF DR Failover Script
# Promotes PostgreSQL standby to primary and updates
# API connection strings in Docker Swarm.
# Usage: ./scripts/dr-failover.sh [--dry-run] [--force]
# =============================================

set -euo pipefail

DRY_RUN=false
FORCE=false
LOG_PREFIX="[dr-failover]"
PRIMARY_HOST="${DB_PRIMARY_HOST:-db}"
STANDBY_HOST="${DB_STANDBY_HOST:-db-standby}"
STANDBY_CONTAINER="${DB_STANDBY_CONTAINER:-ivf-db-standby}"
API_SERVICE="${API_SERVICE_NAME:-ivf_api}"
WEBHOOK_URL="${DR_WEBHOOK_URL:-}"

for arg in "$@"; do
    case $arg in
        --dry-run) DRY_RUN=true ;;
        --force)   FORCE=true ;;
    esac
done

log() { echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') $LOG_PREFIX $1"; }

send_alert() {
    local message="$1"
    log "ALERT: $message"
    if [[ -n "$WEBHOOK_URL" ]]; then
        curl -sf -X POST "$WEBHOOK_URL" \
            -H "Content-Type: application/json" \
            -d "{\"text\":\"$LOG_PREFIX $message\",\"severity\":\"critical\"}" \
            --max-time 5 2>/dev/null || true
    fi
}

# ── Pre-flight checks ───────────────────────────────

log "Starting DR failover process..."

# 1. Verify primary is actually down
if [[ "$FORCE" != true ]]; then
    if docker exec "$PRIMARY_HOST" pg_isready -U postgres &>/dev/null 2>&1; then
        log "ERROR: Primary ($PRIMARY_HOST) is still responding. Use --force to override."
        exit 1
    fi
    log "✓ Primary ($PRIMARY_HOST) confirmed unreachable"
fi

# 2. Verify standby is healthy
if ! docker exec "$STANDBY_CONTAINER" pg_isready -U postgres &>/dev/null 2>&1; then
    log "ERROR: Standby ($STANDBY_CONTAINER) is not responding — cannot failover"
    send_alert "DR FAILOVER ABORTED: Standby is not responding"
    exit 1
fi
log "✓ Standby ($STANDBY_CONTAINER) is healthy"

# 3. Check replication lag
lag=$(docker exec "$STANDBY_CONTAINER" psql -U postgres -tAc \
    "SELECT CASE WHEN pg_last_wal_receive_lsn() = pg_last_wal_replay_lsn() THEN 0
     ELSE EXTRACT(EPOCH FROM now() - pg_last_xact_replay_timestamp())::int END;" 2>/dev/null || echo "-1")

if [[ "$lag" == "-1" ]]; then
    log "WARNING: Could not determine replication lag"
elif (( lag > 60 )); then
    log "WARNING: Replication lag is ${lag}s — potential data loss"
    if [[ "$FORCE" != true ]]; then
        log "ERROR: Lag too high. Use --force to accept data loss."
        exit 1
    fi
else
    log "✓ Replication lag: ${lag}s"
fi

# ── Execute failover ────────────────────────────────

if [[ "$DRY_RUN" == true ]]; then
    log "DRY-RUN: Would promote $STANDBY_CONTAINER to primary"
    log "DRY-RUN: Would update $API_SERVICE connection string to $STANDBY_HOST"
    exit 0
fi

send_alert "DR FAILOVER INITIATED — promoting $STANDBY_CONTAINER to primary"

# 4. Promote standby to primary
log "Promoting standby to primary..."
docker exec "$STANDBY_CONTAINER" pg_ctl promote -D /var/lib/postgresql/data 2>/dev/null || \
    docker exec "$STANDBY_CONTAINER" psql -U postgres -c "SELECT pg_promote();" 2>/dev/null

# 5. Verify promotion
sleep 3
is_recovery=$(docker exec "$STANDBY_CONTAINER" psql -U postgres -tAc "SELECT pg_is_in_recovery();" 2>/dev/null)
if [[ "$is_recovery" == "f" ]]; then
    log "✓ Standby promoted to primary successfully"
else
    log "ERROR: Standby promotion failed — still in recovery mode"
    send_alert "DR FAILOVER FAILED: Standby did not promote"
    exit 1
fi

# 6. Update API service connection string (Docker Swarm)
NEW_CONN="Host=$STANDBY_HOST;Database=ivf_db;Username=postgres;Password=postgres"
log "Updating API service connection string..."
docker service update \
    --env-add "ConnectionStrings__DefaultConnection=$NEW_CONN" \
    "$API_SERVICE" 2>/dev/null

# 7. Wait for API to restart with new connection
log "Waiting for API to restart..."
sleep 10

# 8. Verify API health
api_healthy=false
for i in {1..12}; do
    if curl -sf "http://localhost:5000/health" &>/dev/null; then
        api_healthy=true
        break
    fi
    sleep 5
done

if [[ "$api_healthy" == true ]]; then
    log "✓ API is healthy with new database connection"
    send_alert "DR FAILOVER COMPLETE — API now pointing to $STANDBY_HOST"
else
    log "WARNING: API health check failed after failover"
    send_alert "DR FAILOVER: API may not be healthy — check immediately"
fi

log "DR failover complete."
log "NEXT STEPS:"
log "  1. Investigate why primary ($PRIMARY_HOST) failed"
log "  2. Rebuild primary as a new standby"
log "  3. Consider failback when primary is restored"
