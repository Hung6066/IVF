#!/bin/bash
# ═══════════════════════════════════════════════════════════
#  IVF — WAL Archive Sync to S3 (every 15 minutes)
#  Chạy: */15 * * * * /opt/ivf/scripts/sync-wal-s3.sh
# ═══════════════════════════════════════════════════════════

set -euo pipefail

BUCKET="s3://ivf-backups-production"
TEMP_DIR="/tmp/ivf-wal-sync"
LOG_FILE="/var/log/ivf/wal-s3.log"

mkdir -p "$TEMP_DIR" "$(dirname "$LOG_FILE")"

DB_CONTAINER=$(docker ps -q --filter 'name=ivf_db' --filter status=running | head -1)
if [ -z "$DB_CONTAINER" ]; then
  exit 0
fi

docker cp "${DB_CONTAINER}:/var/lib/postgresql/archive/" "${TEMP_DIR}/" 2>/dev/null || exit 0

if [ "$(ls -A "${TEMP_DIR}" 2>/dev/null)" ]; then
  SYNCED=$(aws s3 sync "${TEMP_DIR}/" "${BUCKET}/wal/" \
    --storage-class STANDARD --sse AES256 \
    --exclude "*.partial" --quiet 2>&1 | wc -l)

  if [ "$SYNCED" -gt 0 ]; then
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Synced WAL segments" >> "$LOG_FILE"
  fi
fi

rm -rf "$TEMP_DIR"
