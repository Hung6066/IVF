#!/bin/bash
# ═══════════════════════════════════════════════════════════
#  IVF Platform — Production Monitoring Script
#  Chạy: */5 * * * * /opt/ivf/scripts/monitor.sh
#
#  Kiểm tra: services, disk, replication, backup age
#  Alert qua Discord webhook
# ═══════════════════════════════════════════════════════════

set -uo pipefail

LOG_DIR="/var/log/ivf"
LOG_FILE="${LOG_DIR}/monitor.log"
ALERT_WEBHOOK="${IVF_ALERT_WEBHOOK:-}"
BASE_URL="${IVF_BASE_URL:-https://natra.site}"

mkdir -p "$LOG_DIR"

ALERTS=""

log() {
  echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" >> "$LOG_FILE"
}

alert() {
  local msg="$1"
  ALERTS="${ALERTS}\n${msg}"
  log "ALERT: $msg"
}

# ── 1. Docker Swarm Services ──
SERVICES=("ivf_api" "ivf_db" "ivf_redis" "ivf_minio" "ivf_caddy" "ivf_frontend" "ivf_db-standby" "ivf_redis-replica")

for svc in "${SERVICES[@]}"; do
  REPLICAS=$(docker service ls --filter "name=${svc}" --format "{{.Replicas}}" 2>/dev/null | head -1 | tr -d '[:space:]')
  if [ -z "$REPLICAS" ]; then
    continue
  fi
  RUNNING=$(echo "$REPLICAS" | cut -d'/' -f1)
  DESIRED=$(echo "$REPLICAS" | cut -d'/' -f2)
  if [ "$RUNNING" != "$DESIRED" ]; then
    alert "⚠️ Service ${svc}: ${RUNNING}/${DESIRED} replicas"
  fi
done

# ── 2. Disk Usage ──
DISK_USAGE=$(df / | tail -1 | awk '{print $5}' | tr -d '%')
if [ "$DISK_USAGE" -gt 85 ]; then
  alert "💾 Disk usage: ${DISK_USAGE}% (threshold: 85%)"
fi

# ── 3. Memory Usage ──
MEM_USAGE=$(free | grep Mem | awk '{printf "%.0f", $3/$2 * 100}')
if [ "$MEM_USAGE" -gt 90 ]; then
  alert "🧠 Memory usage: ${MEM_USAGE}% (threshold: 90%)"
fi

# ── 4. PostgreSQL Replication Lag ──
DB_CONTAINER=$(docker ps -q -f name=ivf_db.1 -f status=running)
if [ -n "$DB_CONTAINER" ]; then
  REP_STATE=$(docker exec "$DB_CONTAINER" psql -U postgres -tAc \
    "SELECT state FROM pg_stat_replication LIMIT 1" 2>/dev/null || echo "")
  if [ -n "$REP_STATE" ] && [ "$REP_STATE" != "streaming" ]; then
    alert "🔄 PG replication state: ${REP_STATE} (expected: streaming)"
  fi

  LAG_BYTES=$(docker exec "$DB_CONTAINER" psql -U postgres -tAc \
    "SELECT COALESCE(pg_wal_lsn_diff(sent_lsn, replay_lsn), 0) FROM pg_stat_replication LIMIT 1" 2>/dev/null || echo "0")
  if [ "${LAG_BYTES:-0}" -gt 104857600 ]; then  # >100MB
    LAG_MB=$((LAG_BYTES / 1048576))
    alert "🔄 PG replication lag: ${LAG_MB}MB (threshold: 100MB)"
  fi
fi

# ── 5. Redis Replication ──
REDIS_CONTAINER=$(docker ps -q -f name=ivf_redis.1 -f status=running)
if [ -n "$REDIS_CONTAINER" ]; then
  CONNECTED_SLAVES=$(docker exec "$REDIS_CONTAINER" redis-cli info replication 2>/dev/null | grep "connected_slaves:" | cut -d: -f2 | tr -d '\r')
  if [ "${CONNECTED_SLAVES:-0}" -lt 1 ]; then
    alert "🔴 Redis: no connected replicas (expected: 1)"
  fi
fi

# ── 6. S3 Backup Age ──
if command -v aws &>/dev/null; then
  LATEST_BACKUP=$(aws s3 ls s3://ivf-backups-production/daily/ 2>/dev/null | sort | tail -1 | awk '{print $1" "$2}')
  if [ -n "$LATEST_BACKUP" ]; then
    BACKUP_EPOCH=$(date -d "$LATEST_BACKUP" +%s 2>/dev/null || echo 0)
    CURRENT_EPOCH=$(date +%s)
    if [ "$BACKUP_EPOCH" -gt 0 ]; then
      AGE_HOURS=$(( (CURRENT_EPOCH - BACKUP_EPOCH) / 3600 ))
      if [ "$AGE_HOURS" -gt 26 ]; then
        alert "📦 S3 backup age: ${AGE_HOURS}h (threshold: 26h)"
      fi
    fi
  fi
fi

# ── 7. API Health ──
HTTP_CODE=$(curl -so /dev/null -w "%{http_code}" --max-time 10 "${BASE_URL}/health" 2>/dev/null || echo "000")
if [ "$HTTP_CODE" != "200" ]; then
  alert "🌐 API health check failed: HTTP ${HTTP_CODE}"
fi

# ── 8. Error log spike (exclude known worker-node warnings) ──
ERROR_COUNT=$(docker service logs ivf_api --since 5m 2>&1 | grep -ci "exception\|fatal\|unhandled" || true)
if [ "$ERROR_COUNT" -gt 10 ]; then
  alert "📊 API error spike: ${ERROR_COUNT} errors in last 5 minutes"
fi

# ── Send Alerts ──
if [ -n "$ALERTS" ] && [ -n "$ALERT_WEBHOOK" ]; then
  HOSTNAME=$(hostname)
  TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')

  # Escape for JSON
  ESCAPED_ALERTS=$(echo -e "$ALERTS" | sed 's/"/\\"/g' | sed ':a;N;$!ba;s/\n/\\n/g')

  curl -s -X POST "$ALERT_WEBHOOK" \
    -H "Content-Type: application/json" \
    -d "{\"content\":\"🚨 **IVF Monitor Alert** — ${HOSTNAME} (${TIMESTAMP})\\n${ESCAPED_ALERTS}\"}" \
    > /dev/null 2>&1 || true
fi

# ── Log summary ──
if [ -z "$ALERTS" ]; then
  log "OK: All checks passed"
fi

# ── Log rotation ──
if [ -f "$LOG_FILE" ]; then
  LOG_SIZE=$(stat -f%z "$LOG_FILE" 2>/dev/null || stat -c%s "$LOG_FILE" 2>/dev/null || echo 0)
  if [ "$LOG_SIZE" -gt 10485760 ]; then  # >10MB
    mv "$LOG_FILE" "${LOG_FILE}.old"
  fi
fi
