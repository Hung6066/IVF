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

DB_CONTAINER=$(docker ps -q -f name=ivf_db.1 -f status=running)
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

MINIO_CONTAINER=$(docker ps -q -f name=ivf_minio.1 -f status=running)
if [ -n "$MINIO_CONTAINER" ]; then
  MINIO_KEY=$(docker exec "$MINIO_CONTAINER" cat /run/secrets/minio_access_key 2>/dev/null || echo "minioadmin")
  MINIO_SECRET=$(docker exec "$MINIO_CONTAINER" cat /run/secrets/minio_secret_key 2>/dev/null || echo "minioadmin123")

  for BUCKET_NAME in ivf-documents ivf-signed-pdfs ivf-medical-images; do
    log "  Syncing bucket: ${BUCKET_NAME}..."
    TEMP_MINIO="/tmp/ivf-minio-${BUCKET_NAME}"
    mkdir -p "$TEMP_MINIO"

    docker run --rm --network ivf_ivf-data \
      -v "${TEMP_MINIO}:/data" \
      minio/mc:latest \
      bash -c "
        mc alias set local http://minio:9000 '${MINIO_KEY}' '${MINIO_SECRET}' 2>/dev/null
        mc mirror --overwrite local/${BUCKET_NAME} /data/ 2>/dev/null
      " 2>/dev/null || log "  Warning: failed to sync ${BUCKET_NAME}"

    if [ -d "$TEMP_MINIO" ] && [ "$(ls -A "$TEMP_MINIO" 2>/dev/null)" ]; then
      aws s3 sync "$TEMP_MINIO/" "${BUCKET}/minio/${BUCKET_NAME}/" \
        --storage-class STANDARD --sse AES256 --quiet
    fi
    rm -rf "$TEMP_MINIO"
  done
  log "  MinIO sync completed"
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

EJBCA_CONTAINER=$(docker ps -q -f name=ivf_ejbca.1 -f status=running)
if [ -n "$EJBCA_CONTAINER" ]; then
  docker cp "${EJBCA_CONTAINER}:/opt/keyfactor/ejbca-ce" "${BACKUP_DIR}/ejbca-persistent/" 2>/dev/null || true
  if [ -d "${BACKUP_DIR}/ejbca-persistent" ]; then
    tar czf "${BACKUP_DIR}/pki_ejbca_${DATE}.tar.gz" -C "${BACKUP_DIR}" ejbca-persistent/
    aws s3 cp "${BACKUP_DIR}/pki_ejbca_${DATE}.tar.gz" \
      "${BUCKET}/pki/pki_ejbca_${DATE}.tar.gz" --sse AES256 --quiet
    log "  EJBCA volume backed up"
  fi
fi

SIGNSRV_CONTAINER=$(docker ps -q -f name=ivf_signserver.1 -f status=running)
if [ -n "$SIGNSRV_CONTAINER" ]; then
  docker cp "${SIGNSRV_CONTAINER}:/opt/keyfactor/signserver-ce" "${BACKUP_DIR}/signserver-persistent/" 2>/dev/null || true
  if [ -d "${BACKUP_DIR}/signserver-persistent" ]; then
    tar czf "${BACKUP_DIR}/pki_signserver_${DATE}.tar.gz" -C "${BACKUP_DIR}" signserver-persistent/
    aws s3 cp "${BACKUP_DIR}/pki_signserver_${DATE}.tar.gz" \
      "${BUCKET}/pki/pki_signserver_${DATE}.tar.gz" --sse AES256 --quiet
    log "  SignServer volume backed up"
  fi
fi

# ── Retention: xóa backup local cũ hơn 7 ngày ──
find /var/log/ivf/ -name "backup-*.log" -mtime +30 -delete 2>/dev/null || true

# ── Summary ──
log "═══ Backup completed successfully ═══"

if [ -n "$HEALTHCHECK_URL" ]; then
  curl -fsS --retry 3 "$HEALTHCHECK_URL" > /dev/null 2>&1 || true
fi
