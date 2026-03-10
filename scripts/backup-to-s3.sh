#!/bin/bash
# ═══════════════════════════════════════════════════════════
#  IVF Platform — Daily Backup to AWS S3
#  Chạy: 0 3 * * * /opt/ivf/scripts/backup-to-s3.sh
# ═══════════════════════════════════════════════════════════

set -euo pipefail

# ── Configuration ──
BUCKET="s3://ivf-backups-production"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/tmp/ivf-backup-${DATE}"
LOG_DIR="/var/log/ivf"
LOG_FILE="${LOG_DIR}/backup-s3.log"
HEALTHCHECK_URL="${IVF_HEALTHCHECK_URL:-}"
GPG_PASSPHRASE_FILE="/opt/ivf/secrets/gpg_passphrase.txt"

# ── Helpers ──
mkdir -p "$BACKUP_DIR" "$LOG_DIR"

log() {
  echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

cleanup() {
  local EXIT_CODE=$?
  rm -rf "$BACKUP_DIR"
  if [ $EXIT_CODE -ne 0 ] && [ -n "$HEALTHCHECK_URL" ]; then
    curl -fsS --retry 3 "${HEALTHCHECK_URL}/fail" > /dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

log "═══ Starting IVF backup to S3: ${DATE} ═══"

# ── 1. PostgreSQL Full Backup ──
log "[1/5] PostgreSQL full backup..."

DB_CONTAINER=$(docker ps -q --filter 'name=ivf_db' --filter status=running | head -1)
if [ -z "$DB_CONTAINER" ]; then
  log "ERROR: PostgreSQL container not found!"
  exit 1
fi

docker exec "$DB_CONTAINER" pg_dump -U postgres ivf_db -Fc 2>/dev/null | \
  gzip > "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz"

DUMP_SIZE=$(du -sh "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" | cut -f1)
log "  Database dump: ${DUMP_SIZE}"

sha256sum "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" > "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz.sha256"

aws s3 cp "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz" \
  "${BUCKET}/daily/ivf_db_${DATE}.dump.gz" \
  --storage-class STANDARD --sse AES256 --quiet
aws s3 cp "${BACKUP_DIR}/ivf_db_${DATE}.dump.gz.sha256" \
  "${BUCKET}/daily/ivf_db_${DATE}.dump.gz.sha256" --quiet

log "  Uploaded to S3: daily/ivf_db_${DATE}.dump.gz"

# ── 2. WAL Archives ──
log "[2/5] WAL archives sync..."

docker cp "${DB_CONTAINER}:/var/lib/postgresql/archive/" "${BACKUP_DIR}/wal/" 2>/dev/null || true

if [ -d "${BACKUP_DIR}/wal" ] && [ "$(ls -A "${BACKUP_DIR}/wal" 2>/dev/null)" ]; then
  WAL_COUNT=$(ls "${BACKUP_DIR}/wal" | wc -l)
  aws s3 sync "${BACKUP_DIR}/wal/" "${BUCKET}/wal/" \
    --storage-class STANDARD --sse AES256 --exclude "*.partial" --quiet
  log "  Synced ${WAL_COUNT} WAL segments"
else
  log "  No WAL archives to sync"
fi

# ── 3. MinIO Objects ──
log "[3/5] MinIO objects sync..."

MINIO_CONTAINER=$(docker ps -q --filter name=minio --filter status=running | head -1)
if [ -n "$MINIO_CONTAINER" ]; then
  # Backup MinIO data directory directly
  docker cp "${MINIO_CONTAINER}:/data/" "${BACKUP_DIR}/minio-data/" 2>/dev/null || true
  
  if [ -d "${BACKUP_DIR}/minio-data" ] && [ "$(ls -A "${BACKUP_DIR}/minio-data" 2>/dev/null)" ]; then
    tar czf "${BACKUP_DIR}/minio_objects_${DATE}.tar.gz" -C "${BACKUP_DIR}" minio-data/
    aws s3 cp "${BACKUP_DIR}/minio_objects_${DATE}.tar.gz" \
      "${BUCKET}/minio/minio_objects_${DATE}.tar.gz" \
      --storage-class STANDARD --sse AES256 --quiet
    MINIO_SIZE=$(du -sh "${BACKUP_DIR}/minio_objects_${DATE}.tar.gz" | cut -f1)
    log "  MinIO data backed up (${MINIO_SIZE})"
  fi
  
  rm -rf "${BACKUP_DIR}/minio-data"
else
  log "  Warning: MinIO container not running, skipping"
fi

# ── 4. Config & Secrets (encrypted) ──
log "[4/5] Config backup (GPG encrypted)..."

if [ -d /opt/ivf ]; then
  cd /opt/ivf

  tar czf "${BACKUP_DIR}/config_${DATE}.tar.gz" \
    stack.yml \
    docker/caddy/Caddyfile \
    docker/postgres/*.sh \
    2>/dev/null || true

  aws s3 cp "${BACKUP_DIR}/config_${DATE}.tar.gz" \
    "${BUCKET}/config/config_${DATE}.tar.gz" --quiet 2>/dev/null || true

  if [ -f "$GPG_PASSPHRASE_FILE" ]; then
    tar czf - secrets/ 2>/dev/null | \
      gpg --symmetric --cipher-algo AES256 \
        --batch --passphrase-file "$GPG_PASSPHRASE_FILE" \
        --output "${BACKUP_DIR}/secrets_${DATE}.tar.gz.gpg"

    aws s3 cp "${BACKUP_DIR}/secrets_${DATE}.tar.gz.gpg" \
      "${BUCKET}/config/secrets_${DATE}.tar.gz.gpg" --quiet
    log "  Secrets backed up (GPG encrypted)"
  else
    log "  Warning: GPG passphrase not found, skipping secrets backup"
  fi
fi

# ── 5. PKI Backup (EJBCA + SignServer volumes) ──
log "[5/5] PKI volumes backup..."

EJBCA_CONTAINER=$(docker ps -q --filter name=ejbca --filter status=running | grep -v db | head -1)
if [ -n "$EJBCA_CONTAINER" ]; then
  docker cp "${EJBCA_CONTAINER}:/opt/keyfactor/" "${BACKUP_DIR}/ejbca-persistent/" 2>/dev/null || true
  if [ -d "${BACKUP_DIR}/ejbca-persistent" ] && [ "$(ls -A "${BACKUP_DIR}/ejbca-persistent" 2>/dev/null)" ]; then
    tar czf "${BACKUP_DIR}/pki_ejbca_${DATE}.tar.gz" -C "${BACKUP_DIR}" ejbca-persistent/
    aws s3 cp "${BACKUP_DIR}/pki_ejbca_${DATE}.tar.gz" \
      "${BUCKET}/pki/pki_ejbca_${DATE}.tar.gz" --sse AES256 --quiet
    EJBCA_SIZE=$(du -sh "${BACKUP_DIR}/pki_ejbca_${DATE}.tar.gz" | cut -f1)
    log "  EJBCA volume backed up (${EJBCA_SIZE})"
  else
    log "  EJBCA data not found"
  fi
else
  log "  Warning: EJBCA container not found"
fi

SIGNSRV_CONTAINER=$(docker ps -q --filter name=signserver --filter status=running | grep -v db | head -1)
if [ -n "$SIGNSRV_CONTAINER" ]; then
  docker cp "${SIGNSRV_CONTAINER}:/opt/keyfactor/" "${BACKUP_DIR}/signserver-persistent/" 2>/dev/null || true
  if [ -d "${BACKUP_DIR}/signserver-persistent" ] && [ "$(ls -A "${BACKUP_DIR}/signserver-persistent" 2>/dev/null)" ]; then
    tar czf "${BACKUP_DIR}/pki_signserver_${DATE}.tar.gz" -C "${BACKUP_DIR}" signserver-persistent/
    aws s3 cp "${BACKUP_DIR}/pki_signserver_${DATE}.tar.gz" \
      "${BUCKET}/pki/pki_signserver_${DATE}.tar.gz" --sse AES256 --quiet
    SS_SIZE=$(du -sh "${BACKUP_DIR}/pki_signserver_${DATE}.tar.gz" | cut -f1)
    log "  SignServer volume backed up (${SS_SIZE})"
  else
    log "  SignServer data not found"
  fi
else
  log "  Warning: SignServer container not found"
fi

# ── Retention: xóa backup local cũ hơn 7 ngày ──
find /var/log/ivf/ -name "backup-*.log" -mtime +30 -delete 2>/dev/null || true

# ── Summary ──
log "═══ Backup completed successfully ═══"

if [ -n "$HEALTHCHECK_URL" ]; then
  curl -fsS --retry 3 "$HEALTHCHECK_URL" > /dev/null 2>&1 || true
fi
