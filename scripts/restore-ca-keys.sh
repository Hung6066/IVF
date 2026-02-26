#!/bin/bash
# =====================================================
# EJBCA CA Keys Restore Script
# =====================================================
# Restores a backup created by backup-ca-keys.sh.
# Reverses the backup process:
#   1. Restores local certificate & secret files
#   2. Restores EJBCA persistent data into container
#   3. Restores SignServer persistent data into container
#   4. Restores EJBCA database from SQL dump
#   5. Reconciles keystore aliases with worker config
#   5b. Regenerates TSA cert if missing critical EKU
#   6. Reactivates SignServer workers
#   7. Verifies everything
#
# Usage:
#   bash scripts/restore-ca-keys.sh <backup-archive>
#   bash scripts/restore-ca-keys.sh backups/ivf-ca-backup_20260226_091837.tar.gz
#   bash scripts/restore-ca-keys.sh --keys-only <backup-archive>
#   bash scripts/restore-ca-keys.sh --dry-run <backup-archive>
#
# The script will prompt before overwriting existing files.
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
EJBCA_DB_CONTAINER="ivf-ejbca-db"
SIGNSERVER_CLI="/opt/signserver/bin/signserver"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

# ── Parse args ──
KEYS_ONLY=false
DRY_RUN=false
SKIP_CONFIRM=false
ARCHIVE=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --keys-only) KEYS_ONLY=true; shift ;;
        --dry-run) DRY_RUN=true; shift ;;
        --yes|-y) SKIP_CONFIRM=true; shift ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS] <backup-archive.tar.gz>"
            echo ""
            echo "Options:"
            echo "  --keys-only   Only restore certificate and secret files"
            echo "  --dry-run     Show what would be restored without making changes"
            echo "  --yes, -y     Skip confirmation prompts"
            echo ""
            echo "Examples:"
            echo "  $0 backups/ivf-ca-backup_20260226_091837.tar.gz"
            echo "  $0 --keys-only backups/ivf-ca-backup_20260226_091837.tar.gz"
            echo "  $0 --dry-run backups/ivf-ca-backup_20260226_091837.tar.gz"
            exit 0
            ;;
        -*) log_error "Unknown option: $1"; exit 1 ;;
        *)
            if [ -z "$ARCHIVE" ]; then
                ARCHIVE="$1"
            else
                log_error "Unexpected argument: $1"
                exit 1
            fi
            shift
            ;;
    esac
done

if [ -z "$ARCHIVE" ]; then
    log_error "No backup archive specified"
    echo "Usage: $0 [OPTIONS] <backup-archive.tar.gz>"
    echo ""
    # List available backups
    if ls "${PROJECT_DIR}/backups/"ivf-ca-backup_*.tar.gz &>/dev/null; then
        echo "Available backups:"
        ls -lh "${PROJECT_DIR}/backups/"ivf-ca-backup_*.tar.gz 2>/dev/null | \
            awk '{print "  " $NF " (" $5 ", " $6 " " $7 ")"}'
    fi
    exit 1
fi

# ── Decrypt if encrypted ──
if [[ "$ARCHIVE" == *.enc ]]; then
    log_info "Archive is encrypted — decrypting..."
    DECRYPTED="${ARCHIVE%.enc}"
    if [ "$DRY_RUN" = true ]; then
        log_warn "[DRY RUN] Would decrypt $ARCHIVE → $DECRYPTED"
    else
        openssl enc -aes-256-cbc -d -pbkdf2 -in "$ARCHIVE" -out "$DECRYPTED"
        ARCHIVE="$DECRYPTED"
        log_ok "Decrypted to $ARCHIVE"
    fi
fi

# ── Validate archive ──
if [ ! -f "$ARCHIVE" ]; then
    log_error "Archive not found: $ARCHIVE"
    exit 1
fi

log_info "═══ EJBCA CA Keys Restore ═══"
log_info "Archive: $ARCHIVE"
log_info "Archive size: $(du -h "$ARCHIVE" | cut -f1)"

if [ "$DRY_RUN" = true ]; then
    log_warn "DRY RUN — no changes will be made"
fi

# List archive contents
log_info "Archive contents:"
tar tzf "$ARCHIVE" | head -30
FILE_COUNT=$(tar tzf "$ARCHIVE" | wc -l)
log_info "Total files: $FILE_COUNT"
echo ""

# ── Extract to temp directory ──
RESTORE_TMP=$(mktemp -d)
trap "rm -rf '$RESTORE_TMP'" EXIT

tar xzf "$ARCHIVE" -C "$RESTORE_TMP"

# Find the backup directory inside the extracted archive
BACKUP_DIR_NAME=$(ls "$RESTORE_TMP" | head -1)
RESTORE_PATH="${RESTORE_TMP}/${BACKUP_DIR_NAME}"

if [ ! -d "$RESTORE_PATH" ]; then
    log_error "Invalid backup structure — expected directory inside archive"
    exit 1
fi

# Show backup metadata if available
if [ -f "${RESTORE_PATH}/backup-info.txt" ]; then
    log_info "Backup metadata:"
    cat "${RESTORE_PATH}/backup-info.txt" | sed 's/^/  /'
    echo ""
fi

# ── Confirmation ──
if [ "$DRY_RUN" = false ] && [ "$SKIP_CONFIRM" = false ]; then
    echo -e "${YELLOW}WARNING: This will overwrite existing certificates, keys, and data.${NC}"
    echo -e "${YELLOW}Containers may need to be restarted after restore.${NC}"
    echo ""
    read -p "Continue with restore? [y/N] " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log_info "Restore cancelled"
        exit 0
    fi
fi

RESTORED=0
SKIPPED=0

# ═══════════════════════════════════════
# Step 1: Restore local certificate files
# ═══════════════════════════════════════
log_info "Step 1: Restoring local certificate files..."

if [ -d "${RESTORE_PATH}/certs" ]; then
    if [ "$DRY_RUN" = true ]; then
        CERT_COUNT=$(find "${RESTORE_PATH}/certs" -type f | wc -l)
        log_warn "[DRY RUN] Would restore $CERT_COUNT cert files to ${PROJECT_DIR}/certs/"
    else
        mkdir -p "${PROJECT_DIR}/certs"
        cp -r "${RESTORE_PATH}/certs/." "${PROJECT_DIR}/certs/"
        CERT_COUNT=$(find "${RESTORE_PATH}/certs" -type f | wc -l)
        log_ok "Restored $CERT_COUNT certificate files"
        RESTORED=$((RESTORED + 1))
    fi
else
    log_warn "No certs directory in backup — skipping"
    SKIPPED=$((SKIPPED + 1))
fi

if [ -d "${RESTORE_PATH}/secrets" ]; then
    if [ "$DRY_RUN" = true ]; then
        log_warn "[DRY RUN] Would restore secrets to ${PROJECT_DIR}/secrets/"
    else
        mkdir -p "${PROJECT_DIR}/secrets"
        cp -r "${RESTORE_PATH}/secrets/." "${PROJECT_DIR}/secrets/"
        log_ok "Restored secrets"
        RESTORED=$((RESTORED + 1))
    fi
else
    log_warn "No secrets directory in backup — skipping"
    SKIPPED=$((SKIPPED + 1))
fi

if [ "$KEYS_ONLY" = true ]; then
    log_info "Keys-only mode — skipping container data restore"
else
    # ═══════════════════════════════════════
    # Step 2: Restore EJBCA persistent data
    # ═══════════════════════════════════════
    log_info "Step 2: Restoring EJBCA persistent data..."

    if [ -f "${RESTORE_PATH}/ejbca-persistent.tar.gz" ]; then
        if ! docker inspect "$EJBCA_CONTAINER" &>/dev/null; then
            log_warn "EJBCA container not found — skipping persistent data restore"
            SKIPPED=$((SKIPPED + 1))
        elif [ "$DRY_RUN" = true ]; then
            log_warn "[DRY RUN] Would restore EJBCA persistent data"
        else
            gunzip -c "${RESTORE_PATH}/ejbca-persistent.tar.gz" | \
                docker exec -i "$EJBCA_CONTAINER" tar xf - -C /opt/keyfactor/persistent 2>/dev/null || true

            if [ $? -eq 0 ]; then
                log_ok "EJBCA persistent data restored"
                RESTORED=$((RESTORED + 1))
            else
                log_warn "Could not restore EJBCA persistent data"
                SKIPPED=$((SKIPPED + 1))
            fi
        fi
    else
        log_warn "No EJBCA persistent data in backup"
        SKIPPED=$((SKIPPED + 1))
    fi

    # ═══════════════════════════════════════
    # Step 3: Restore SignServer persistent data
    # ═══════════════════════════════════════
    log_info "Step 3: Restoring SignServer persistent data..."

    if [ -f "${RESTORE_PATH}/signserver-persistent.tar.gz" ]; then
        if ! docker inspect "$SIGNSERVER_CONTAINER" &>/dev/null; then
            log_warn "SignServer container not found — skipping persistent data restore"
            SKIPPED=$((SKIPPED + 1))
        elif [ "$DRY_RUN" = true ]; then
            log_warn "[DRY RUN] Would restore SignServer persistent data"
        else
            gunzip -c "${RESTORE_PATH}/signserver-persistent.tar.gz" | \
                docker exec -i "$SIGNSERVER_CONTAINER" tar xf - -C /opt/keyfactor/persistent 2>/dev/null || true

            if [ $? -eq 0 ]; then
                log_ok "SignServer persistent data restored"
                RESTORED=$((RESTORED + 1))
            else
                log_warn "Could not restore SignServer persistent data"
                SKIPPED=$((SKIPPED + 1))
            fi
        fi
    else
        log_warn "No SignServer persistent data in backup"
        SKIPPED=$((SKIPPED + 1))
    fi

    # ═══════════════════════════════════════
    # Step 4: Restore EJBCA database
    # ═══════════════════════════════════════
    log_info "Step 4: Restoring EJBCA database..."

    if [ -f "${RESTORE_PATH}/ejbca-db.sql" ]; then
        if ! docker inspect "$EJBCA_DB_CONTAINER" &>/dev/null; then
            log_warn "EJBCA DB container not found — skipping database restore"
            SKIPPED=$((SKIPPED + 1))
        elif [ "$DRY_RUN" = true ]; then
            DB_SIZE=$(du -h "${RESTORE_PATH}/ejbca-db.sql" | cut -f1)
            log_warn "[DRY RUN] Would restore EJBCA database ($DB_SIZE)"
        else
            DB_SIZE=$(du -h "${RESTORE_PATH}/ejbca-db.sql" | cut -f1)
            log_info "  Dropping and recreating EJBCA database ($DB_SIZE)..."

            # Drop existing connections and recreate
            docker exec "$EJBCA_DB_CONTAINER" psql -U ejbca -d postgres -c \
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='ejbca' AND pid <> pg_backend_pid();" \
                2>/dev/null || true

            docker exec "$EJBCA_DB_CONTAINER" psql -U ejbca -d postgres -c \
                "DROP DATABASE IF EXISTS ejbca;" 2>/dev/null || true

            docker exec "$EJBCA_DB_CONTAINER" psql -U ejbca -d postgres -c \
                "CREATE DATABASE ejbca OWNER ejbca;" 2>/dev/null || true

            # Restore the dump
            cat "${RESTORE_PATH}/ejbca-db.sql" | \
                docker exec -i "$EJBCA_DB_CONTAINER" psql -U ejbca -d ejbca \
                    --quiet --single-transaction 2>/dev/null

            if [ $? -eq 0 ]; then
                log_ok "EJBCA database restored ($DB_SIZE)"
                RESTORED=$((RESTORED + 1))
            else
                log_error "EJBCA database restore failed"
                SKIPPED=$((SKIPPED + 1))
            fi
        fi
    else
        log_warn "No EJBCA database dump in backup"
        SKIPPED=$((SKIPPED + 1))
    fi

    # ═══════════════════════════════════════
    # Step 4b: Restore SignServer database
    # ═══════════════════════════════════════
    log_info "Step 4b: Restoring SignServer database..."

    SIGNSERVER_DB_CONTAINER="ivf-signserver-db"

    if [ -f "${RESTORE_PATH}/signserver-db.sql" ]; then
        if ! docker inspect "$SIGNSERVER_DB_CONTAINER" &>/dev/null; then
            log_warn "SignServer DB container not found — skipping database restore"
            SKIPPED=$((SKIPPED + 1))
        elif [ "$DRY_RUN" = true ]; then
            DB_SIZE=$(du -h "${RESTORE_PATH}/signserver-db.sql" | cut -f1)
            log_warn "[DRY RUN] Would restore SignServer database ($DB_SIZE)"
        else
            DB_SIZE=$(du -h "${RESTORE_PATH}/signserver-db.sql" | cut -f1)
            log_info "  Dropping and recreating SignServer database ($DB_SIZE)..."

            docker exec "$SIGNSERVER_DB_CONTAINER" psql -U signserver -d postgres -c \
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='signserver' AND pid <> pg_backend_pid();" \
                2>/dev/null || true

            docker exec "$SIGNSERVER_DB_CONTAINER" psql -U signserver -d postgres -c \
                "DROP DATABASE IF EXISTS signserver;" 2>/dev/null || true

            docker exec "$SIGNSERVER_DB_CONTAINER" psql -U signserver -d postgres -c \
                "CREATE DATABASE signserver OWNER signserver;" 2>/dev/null || true

            cat "${RESTORE_PATH}/signserver-db.sql" | \
                docker exec -i "$SIGNSERVER_DB_CONTAINER" psql -U signserver -d signserver \
                    --quiet --single-transaction 2>/dev/null

            if [ $? -eq 0 ]; then
                log_ok "SignServer database restored ($DB_SIZE)"
                RESTORED=$((RESTORED + 1))
            else
                log_error "SignServer database restore failed"
                SKIPPED=$((SKIPPED + 1))
            fi
        fi
    else
        log_warn "No SignServer database dump in backup — skipping"
    fi

    # ═══════════════════════════════════════
    # Step 5: Reconcile keystore aliases
    # ═══════════════════════════════════════
    log_info "Step 5: Reconciling keystore aliases with worker config..."

    if ! docker inspect "$SIGNSERVER_CONTAINER" &>/dev/null; then
        log_warn "SignServer container not found — skipping alias reconciliation"
    elif [ "$DRY_RUN" = true ]; then
        log_warn "[DRY RUN] Would reconcile keystore aliases"
    else
        WORKER_IDS=$(docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
            getstatus brief all 2>/dev/null \
            | grep "Status of Signer with ID" \
            | sed 's/.*ID \([0-9]*\).*/\1/' || echo "")

        RECONCILED=0
        for wid in $WORKER_IDS; do
            # Get the expected DEFAULTKEY and KEYSTOREPATH
            local expected_alias
            expected_alias=$(docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
                getconfig "$wid" 2>/dev/null | grep "DEFAULTKEY=" | sed 's/.*DEFAULTKEY=//' | tr -d '[:space:]')
            local keystore_path
            keystore_path=$(docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
                getconfig "$wid" 2>/dev/null | grep "KEYSTOREPATH=" | sed 's/.*KEYSTOREPATH=//' | tr -d '[:space:]')

            if [ -z "$expected_alias" ] || [ -z "$keystore_path" ]; then
                continue
            fi

            # Check actual alias in keystore
            local actual_alias
            actual_alias=$(docker exec "$SIGNSERVER_CONTAINER" keytool -list \
                -keystore "$keystore_path" -storepass changeit -storetype PKCS12 2>/dev/null \
                | grep "PrivateKeyEntry" | head -1 | cut -d',' -f1 | tr -d '[:space:]')

            if [ -n "$actual_alias" ] && [ "$actual_alias" != "$expected_alias" ]; then
                log_info "  Worker $wid: alias '$actual_alias' → setting DEFAULTKEY='$actual_alias'"
                docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
                    setproperty "$wid" DEFAULTKEY "$actual_alias" 2>/dev/null
                docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
                    reload "$wid" 2>/dev/null
                RECONCILED=$((RECONCILED + 1))
            fi
        done

        if [ "$RECONCILED" -gt 0 ]; then
            log_ok "Reconciled $RECONCILED worker aliases"
        else
            log_ok "All keystore aliases match worker config"
        fi
    fi

    # ═══════════════════════════════════════
    # Step 5b: Regenerate TSA cert if needed
    # ═══════════════════════════════════════
    log_info "Step 5b: Checking TSA worker certificate..."

    if ! docker inspect "$SIGNSERVER_CONTAINER" &>/dev/null; then
        log_warn "SignServer container not found — skipping TSA check"
    elif [ "$DRY_RUN" = true ]; then
        log_warn "[DRY RUN] Would check and fix TSA certificate"
    else
        # Check if TimeStampSigner (ID 100) has the critical EKU error
        TSA_STATUS=$(docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
            getstatus brief 100 2>/dev/null || echo "")

        if echo "$TSA_STATUS" | grep -qi "extended key usage.*critical"; then
            log_info "  TSA cert missing critical EKU — regenerating..."
            TSA_KEYSTORE="/opt/keyfactor/persistent/keys/tsa-signer.p12"

            docker exec "$SIGNSERVER_CONTAINER" rm -f "$TSA_KEYSTORE"
            docker exec "$SIGNSERVER_CONTAINER" keytool -genkeypair \
                -alias tsa -keyalg RSA -keysize 2048 -sigalg SHA256withRSA \
                -validity 3650 \
                -dname "CN=IVF Timestamp Authority,O=IVF Clinic,C=VN" \
                -ext "ExtendedKeyUsage:critical=timeStamping" \
                -ext "KeyUsage=digitalSignature" \
                -storetype PKCS12 \
                -keystore "$TSA_KEYSTORE" \
                -storepass changeit -keypass changeit 2>/dev/null

            docker exec "$SIGNSERVER_CONTAINER" chmod 400 "$TSA_KEYSTORE"
            docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" reload 100 2>/dev/null
            log_ok "TSA certificate regenerated with critical EKU"
        else
            log_ok "TSA certificate OK"
        fi
    fi

    # ═══════════════════════════════════════
    # Step 6: Reactivate SignServer workers
    # ═══════════════════════════════════════
    log_info "Step 6: Reactivating SignServer workers..."

    if ! docker inspect "$SIGNSERVER_CONTAINER" &>/dev/null; then
        log_warn "SignServer container not found — skipping worker reactivation"
    elif [ "$DRY_RUN" = true ]; then
        log_warn "[DRY RUN] Would reactivate SignServer workers"
    else
        # Get list of worker IDs
        WORKER_IDS=$(docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
            getstatus brief all 2>/dev/null \
            | grep "Status of Signer with ID" \
            | sed 's/.*ID \([0-9]*\).*/\1/' || echo "")

        if [ -n "$WORKER_IDS" ]; then
            ACTIVATED=0
            FAILED=0
            for wid in $WORKER_IDS; do
                RESULT=$(docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
                    activatecryptotoken "$wid" changeit 2>&1 || true)
                if echo "$RESULT" | grep -q "successful"; then
                    ACTIVATED=$((ACTIVATED + 1))
                else
                    FAILED=$((FAILED + 1))
                    log_warn "Worker $wid activation failed"
                fi
            done
            log_ok "Workers activated: $ACTIVATED, failed: $FAILED"
            RESTORED=$((RESTORED + 1))
        else
            log_warn "No workers found to reactivate"
        fi
    fi
fi

# ═══════════════════════════════════════
# Step 7: Verify
# ═══════════════════════════════════════
if [ "$DRY_RUN" = false ] && [ "$KEYS_ONLY" = false ]; then
    log_info "Step 7: Verification..."

    # Check SignServer workers
    if docker inspect "$SIGNSERVER_CONTAINER" &>/dev/null; then
        echo ""
        docker exec "$SIGNSERVER_CONTAINER" "$SIGNSERVER_CLI" \
            getstatus brief all 2>/dev/null || true
        echo ""
    fi

    # Check EJBCA CAs
    if docker inspect "$EJBCA_CONTAINER" &>/dev/null; then
        log_info "EJBCA CAs:"
        docker exec "$EJBCA_CONTAINER" /opt/keyfactor/bin/ejbca.sh ca listcas 2>/dev/null \
            | grep "CA Name:" | sed 's/^.*CA Name:/  CA:/' || true
    fi
fi

# ═══════════════════════════════════════
# Summary
# ═══════════════════════════════════════
echo ""
log_info "═══ Restore Summary ═══"
if [ "$DRY_RUN" = true ]; then
    log_warn "DRY RUN — no changes were made"
else
    log_ok "Restored: $RESTORED components"
    [ "$SKIPPED" -gt 0 ] && log_warn "Skipped: $SKIPPED components"
fi

echo ""
if [ "$DRY_RUN" = false ] && [ "$KEYS_ONLY" = false ]; then
    log_warn "Recommended post-restore steps:"
    echo "  1. Restart EJBCA:      docker restart $EJBCA_CONTAINER"
    echo "  2. Restart SignServer:  docker restart $SIGNSERVER_CONTAINER"
    echo "  3. Re-apply mTLS:      bash scripts/init-mtls.sh"
    echo "  4. Re-apply TSA:       bash scripts/init-tsa.sh"
    echo "  5. Verify signing:     curl -sk https://localhost:9443/signserver/healthcheck/signserverhealth"
fi

log_ok "Restore complete"
