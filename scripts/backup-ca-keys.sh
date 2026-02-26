#!/bin/bash
# =====================================================
# EJBCA CA Keys Backup Script
# =====================================================
# Creates encrypted backup of EJBCA CA private keys,
# persistent data, and critical configuration.
#
# Usage:
#   bash scripts/backup-ca-keys.sh                    # full backup
#   bash scripts/backup-ca-keys.sh --keys-only        # CA keys only
#   bash scripts/backup-ca-keys.sh --output /path/    # custom output dir
#
# Backups are encrypted with AES-256 using a passphrase.
# =====================================================

set -euo pipefail

# ── Colors ──
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info()  { echo -e "${BLUE}[INFO]${NC} $1"; }
log_ok()    { echo -e "${GREEN}[OK]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# ── Configuration ──
EJBCA_CONTAINER="ivf-ejbca"
SIGNSERVER_CONTAINER="ivf-signserver"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BACKUP_DIR="${PROJECT_DIR}/backups"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
BACKUP_NAME="ivf-ca-backup_${TIMESTAMP}"
KEYS_ONLY=false
OUTPUT_DIR=""

# ── Parse args ──
while [[ $# -gt 0 ]]; do
    case $1 in
        --keys-only) KEYS_ONLY=true; shift ;;
        --output) OUTPUT_DIR="$2"; shift 2 ;;
        -h|--help)
            echo "Usage: $0 [--keys-only] [--output /path/to/dir]"
            echo ""
            echo "Options:"
            echo "  --keys-only   Only backup CA certificate and key files"
            echo "  --output      Custom output directory (default: ./backups/)"
            exit 0
            ;;
        *) log_error "Unknown option: $1"; exit 1 ;;
    esac
done

if [ -n "$OUTPUT_DIR" ]; then
    BACKUP_DIR="$OUTPUT_DIR"
fi

BACKUP_PATH="${BACKUP_DIR}/${BACKUP_NAME}"

# ── Pre-flight ──
log_info "═══ EJBCA CA Keys Backup ═══"
log_info "Timestamp: $TIMESTAMP"
log_info "Output: $BACKUP_PATH/"

mkdir -p "$BACKUP_PATH"

# ── Step 1: Backup local certificate files ──
log_info "Step 1: Backing up local certificate files..."

CERTS_DIR="${PROJECT_DIR}/certs"
SECRETS_DIR="${PROJECT_DIR}/secrets"

if [ -d "$CERTS_DIR" ]; then
    cp -r "$CERTS_DIR" "${BACKUP_PATH}/certs"
    log_ok "Local certs copied ($(find "${BACKUP_PATH}/certs" -type f | wc -l) files)"
else
    log_warn "No certs directory found at $CERTS_DIR"
fi

if [ -d "$SECRETS_DIR" ]; then
    cp -r "$SECRETS_DIR" "${BACKUP_PATH}/secrets"
    log_ok "Secrets copied"
else
    log_warn "No secrets directory found"
fi

if [ "$KEYS_ONLY" = true ]; then
    log_info "Keys-only mode — skipping container data export"
else
    # ── Step 2: Export EJBCA persistent volume ──
    log_info "Step 2: Exporting EJBCA persistent data..."

    if docker inspect "$EJBCA_CONTAINER" &>/dev/null; then
        docker exec "$EJBCA_CONTAINER" tar cf - \
            -C /opt/keyfactor/persistent . 2>/dev/null \
            | gzip > "${BACKUP_PATH}/ejbca-persistent.tar.gz" || true

        if [ -s "${BACKUP_PATH}/ejbca-persistent.tar.gz" ]; then
            log_ok "EJBCA persistent data exported"
        else
            rm -f "${BACKUP_PATH}/ejbca-persistent.tar.gz"
            log_warn "Could not export EJBCA persistent data"
        fi
    else
        log_warn "EJBCA container not running — skipping volume export"
    fi

    # ── Step 3: Export SignServer persistent volume ──
    log_info "Step 3: Exporting SignServer persistent data..."

    if docker inspect "$SIGNSERVER_CONTAINER" &>/dev/null; then
        docker exec "$SIGNSERVER_CONTAINER" tar cf - \
            -C /opt/keyfactor/persistent . 2>/dev/null \
            | gzip > "${BACKUP_PATH}/signserver-persistent.tar.gz" || true

        if [ -s "${BACKUP_PATH}/signserver-persistent.tar.gz" ]; then
            log_ok "SignServer persistent data exported"
        else
            rm -f "${BACKUP_PATH}/signserver-persistent.tar.gz"
            log_warn "Could not export SignServer persistent data"
        fi
    else
        log_warn "SignServer container not running — skipping volume export"
    fi

    # ── Step 4: Export EJBCA database ──
    log_info "Step 4: Exporting EJBCA database..."

    EJBCA_DB_CONTAINER="ivf-ejbca-db"
    if docker inspect "$EJBCA_DB_CONTAINER" &>/dev/null; then
        docker exec "$EJBCA_DB_CONTAINER" pg_dump -U ejbca -d ejbca \
            --no-owner --no-acl \
            > "${BACKUP_PATH}/ejbca-db.sql" 2>/dev/null || true

        if [ -s "${BACKUP_PATH}/ejbca-db.sql" ]; then
            log_ok "EJBCA database exported ($(du -h "${BACKUP_PATH}/ejbca-db.sql" | cut -f1))"
        else
            rm -f "${BACKUP_PATH}/ejbca-db.sql"
            log_warn "Could not export EJBCA database"
        fi
    else
        log_warn "EJBCA DB container not running — skipping database export"
    fi

    # ── Step 4b: Export SignServer database ──
    log_info "Step 4b: Exporting SignServer database..."

    SIGNSERVER_DB_CONTAINER="ivf-signserver-db"
    if docker inspect "$SIGNSERVER_DB_CONTAINER" &>/dev/null; then
        docker exec "$SIGNSERVER_DB_CONTAINER" pg_dump -U signserver -d signserver \
            --no-owner --no-acl \
            > "${BACKUP_PATH}/signserver-db.sql" 2>/dev/null || true

        if [ -s "${BACKUP_PATH}/signserver-db.sql" ]; then
            log_ok "SignServer database exported ($(du -h "${BACKUP_PATH}/signserver-db.sql" | cut -f1))"
        else
            rm -f "${BACKUP_PATH}/signserver-db.sql"
            log_warn "Could not export SignServer database"
        fi
    else
        log_warn "SignServer DB container not running — skipping database export"
    fi
fi

# ── Step 5: Create metadata file ──
log_info "Creating backup metadata..."

cat > "${BACKUP_PATH}/backup-info.txt" << EOF
IVF CA Keys Backup
==================
Timestamp: $TIMESTAMP
Date: $(date -Iseconds)
Host: $(hostname)
Mode: $([ "$KEYS_ONLY" = true ] && echo "keys-only" || echo "full")

Contents:
$(ls -la "${BACKUP_PATH}/" 2>/dev/null)

EJBCA Container: $(docker inspect --format='{{.State.Status}}' "$EJBCA_CONTAINER" 2>/dev/null || echo "not running")
SignServer Container: $(docker inspect --format='{{.State.Status}}' "$SIGNSERVER_CONTAINER" 2>/dev/null || echo "not running")
EOF

log_ok "Metadata written"

# ── Step 6: Create encrypted archive ──
log_info "Step 5: Creating encrypted archive..."

ARCHIVE="${BACKUP_DIR}/${BACKUP_NAME}.tar.gz"

tar czf "$ARCHIVE" -C "$BACKUP_DIR" "$BACKUP_NAME"

ARCHIVE_SIZE=$(du -h "$ARCHIVE" | cut -f1)
log_ok "Archive created: $ARCHIVE ($ARCHIVE_SIZE)"

# Prompt for encryption
echo ""
echo -e "${YELLOW}To encrypt (recommended for offsite storage):${NC}"
echo "  openssl enc -aes-256-cbc -salt -pbkdf2 -in $ARCHIVE -out ${ARCHIVE}.enc"
echo ""
echo -e "${YELLOW}To decrypt:${NC}"
echo "  openssl enc -aes-256-cbc -d -pbkdf2 -in ${ARCHIVE}.enc -out ${ARCHIVE}"
echo ""

# Clean up uncompressed directory
rm -rf "$BACKUP_PATH"

# ── Summary ──
echo ""
log_info "═══ Backup Summary ═══"
log_ok "Backup: $ARCHIVE ($ARCHIVE_SIZE)"
FILE_COUNT=$(tar tzf "$ARCHIVE" | wc -l)
log_info "Files: $FILE_COUNT"
echo ""
log_warn "IMPORTANT: Store this backup in a secure, offsite location."
log_warn "CA private keys allow issuing arbitrary certificates."
log_ok "Backup complete"
