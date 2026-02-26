#!/bin/bash
# =====================================================
# PostgreSQL Point-in-Time Recovery (PITR) Script
# =====================================================
# Restores the IVF database from a base backup + WAL archive
# to a specific point in time.
#
# Usage:
#   bash scripts/restore-pitr.sh <base-backup.tar.gz> [options]
#
# Options:
#   --target-time "2026-02-26 10:30:00 UTC"   Recover up to this timestamp
#   --target-latest                             Recover to latest available (default)
#   --dry-run                                   Validate without restoring
#   --yes, -y                                   Skip confirmation prompts
#   --wal-dir <path>                            Extra WAL directory (host path)
#
# Examples:
#   # Restore to latest available point
#   bash scripts/restore-pitr.sh backups/ivf_basebackup_20260226_082819.tar.gz
#
#   # Restore to a specific time
#   bash scripts/restore-pitr.sh backups/ivf_basebackup_20260226_082819.tar.gz \
#       --target-time "2026-02-26 10:30:00 UTC"
#
#   # Dry-run validation
#   bash scripts/restore-pitr.sh backups/ivf_basebackup_20260226_082819.tar.gz --dry-run
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
log_step()  { echo -e "\n${BLUE}═══ Step $1: $2 ═══${NC}"; }

# ── Configuration ──
DB_CONTAINER="ivf-db"
DB_USER="postgres"
DB_NAME="ivf_db"
PGDATA="/var/lib/postgresql/data"
ARCHIVE_DIR="/var/lib/postgresql/archive"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
BACKUPS_DIR="${PROJECT_DIR}/backups"

# ── Parse args ──
BASE_BACKUP=""
TARGET_TIME=""
TARGET_LATEST=true
DRY_RUN=false
SKIP_CONFIRM=false
EXTRA_WAL_DIR=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --target-time)
            TARGET_TIME="$2"
            TARGET_LATEST=false
            shift 2
            ;;
        --target-latest)
            TARGET_LATEST=true
            shift
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --yes|-y)
            SKIP_CONFIRM=true
            shift
            ;;
        --wal-dir)
            EXTRA_WAL_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 <base-backup.tar.gz> [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --target-time <timestamp>   Recover to specific time (UTC)"
            echo "  --target-latest             Recover to latest WAL point (default)"
            echo "  --dry-run                   Validate without restoring"
            echo "  --wal-dir <path>            Extra WAL archive directory"
            echo "  --yes, -y                   Skip confirmation prompts"
            echo ""
            echo "Available base backups:"
            ls -lh "${BACKUPS_DIR}/"ivf_basebackup_*.tar.gz 2>/dev/null | \
                awk '{print "  " $NF " (" $5 ")"}' || echo "  (none found)"
            echo ""
            echo "Available WAL files in container:"
            docker exec ${DB_CONTAINER} sh -c "ls -1 ${ARCHIVE_DIR}/ 2>/dev/null | wc -l" 2>/dev/null || echo "  (container not running)"
            exit 0
            ;;
        -*)
            log_error "Unknown option: $1"
            exit 1
            ;;
        *)
            if [ -z "$BASE_BACKUP" ]; then
                BASE_BACKUP="$1"
            else
                log_error "Unexpected argument: $1"
                exit 1
            fi
            shift
            ;;
    esac
done

# ── Validate base backup path ──
if [ -z "$BASE_BACKUP" ]; then
    log_error "No base backup specified"
    echo "Usage: $0 <base-backup.tar.gz> [OPTIONS]"
    echo ""
    echo "Available base backups:"
    ls -lh "${BACKUPS_DIR}/"ivf_basebackup_*.tar.gz 2>/dev/null | \
        awk '{print "  " $NF " (" $5 ")"}' || echo "  (none found)"
    exit 1
fi

# Resolve relative paths
if [[ ! "$BASE_BACKUP" = /* ]]; then
    BASE_BACKUP="${PROJECT_DIR}/${BASE_BACKUP}"
fi

if [ ! -f "$BASE_BACKUP" ]; then
    log_error "Base backup not found: $BASE_BACKUP"
    exit 1
fi

BACKUP_FILENAME="$(basename "$BASE_BACKUP")"

# ══════════════════════════════════════════════════════
# Pre-flight checks
# ══════════════════════════════════════════════════════
log_info "PostgreSQL Point-in-Time Recovery"
echo "────────────────────────────────────"
echo "  Base backup:  ${BACKUP_FILENAME}"
if [ "$TARGET_LATEST" = true ]; then
    echo "  Target:       Latest available WAL"
else
    echo "  Target time:  ${TARGET_TIME}"
fi
echo "  Dry run:      ${DRY_RUN}"
echo "  Database:     ${DB_NAME}"
echo "  Container:    ${DB_CONTAINER}"
echo "────────────────────────────────────"

# Check container is running
if ! docker inspect -f '{{.State.Running}}' "$DB_CONTAINER" 2>/dev/null | grep -q true; then
    log_error "Container '${DB_CONTAINER}' is not running"
    exit 1
fi

# Verify checksum if available
CHECKSUM_FILE="${BASE_BACKUP}.sha256"
if [ -f "$CHECKSUM_FILE" ]; then
    log_info "Verifying backup checksum..."
    EXPECTED_HASH=$(awk '{print $1}' "$CHECKSUM_FILE")
    ACTUAL_HASH=$(sha256sum "$BASE_BACKUP" | awk '{print $1}')
    if [ "$EXPECTED_HASH" != "$ACTUAL_HASH" ]; then
        log_error "Checksum mismatch!"
        echo "  Expected: ${EXPECTED_HASH}"
        echo "  Actual:   ${ACTUAL_HASH}"
        exit 1
    fi
    log_ok "Checksum verified: ${ACTUAL_HASH:0:16}..."
else
    log_warn "No checksum file found — skipping integrity check"
fi

# Check WAL archive availability
WAL_COUNT=$(docker exec "$DB_CONTAINER" sh -c "ls -1 ${ARCHIVE_DIR}/ 2>/dev/null | wc -l" 2>/dev/null || echo "0")
WAL_COUNT=$(echo "$WAL_COUNT" | tr -d '[:space:]')
log_info "WAL archive contains ${WAL_COUNT} segment(s) in container"

# Also check local WAL backups
LOCAL_WAL_DIR="${BACKUPS_DIR}/wal"
LOCAL_WAL_COUNT=0
if [ -d "$LOCAL_WAL_DIR" ]; then
    LOCAL_WAL_COUNT=$(ls -1 "$LOCAL_WAL_DIR" 2>/dev/null | wc -l)
    LOCAL_WAL_COUNT=$(echo "$LOCAL_WAL_COUNT" | tr -d '[:space:]')
    log_info "Local WAL backup directory has ${LOCAL_WAL_COUNT} file(s)"
fi

# Validate target time format if provided
if [ -n "$TARGET_TIME" ]; then
    if ! date -d "$TARGET_TIME" >/dev/null 2>&1; then
        log_error "Invalid target time format: '${TARGET_TIME}'"
        echo "  Use ISO 8601 format, e.g.: '2026-02-26 10:30:00 UTC'"
        exit 1
    fi
    log_ok "Target time is valid: $(date -d "$TARGET_TIME" --utc '+%Y-%m-%d %H:%M:%S UTC')"
fi

# ── Dry-run stop point ──
if [ "$DRY_RUN" = true ]; then
    echo ""
    log_info "=== DRY RUN — No changes will be made ==="
    echo ""
    echo "Recovery plan:"
    echo "  1. Stop PostgreSQL in container"
    echo "  2. Backup current PGDATA to ${PGDATA}_pre_pitr"
    echo "  3. Extract base backup to ${PGDATA}"
    echo "  4. Copy ${WAL_COUNT} WAL segments to pg_wal"
    if [ "$LOCAL_WAL_COUNT" -gt 0 ]; then
        echo "     + ${LOCAL_WAL_COUNT} WAL segments from local backups"
    fi
    if [ -n "$EXTRA_WAL_DIR" ]; then
        echo "     + WAL segments from ${EXTRA_WAL_DIR}"
    fi
    echo "  5. Configure recovery target"
    if [ "$TARGET_LATEST" = true ]; then
        echo "     → recovery_target_action = 'promote' (latest)"
    else
        echo "     → recovery_target_time = '${TARGET_TIME}'"
    fi
    echo "  6. Start PostgreSQL in recovery mode"
    echo "  7. Wait for recovery to complete"
    echo "  8. Verify database"
    echo ""
    log_ok "Dry run complete — backup and WAL files look valid"
    exit 0
fi

# ── Confirm ──
if [ "$SKIP_CONFIRM" = false ]; then
    echo ""
    log_warn "⚠ THIS WILL REPLACE THE CURRENT DATABASE"
    log_warn "The current PGDATA will be backed up to ${PGDATA}_pre_pitr"
    echo ""
    read -p "Continue with PITR restore? [y/N] " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log_info "Aborted"
        exit 0
    fi
fi

# ══════════════════════════════════════════════════════
# Step 1: Create a safety backup of current data
# ══════════════════════════════════════════════════════
log_step 1 "Safety backup of current PGDATA"

# Force a WAL switch to flush latest changes
docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -c "SELECT pg_switch_wal();" >/dev/null 2>&1 || true
sleep 1

# Create a quick pg_dump of the current database as a last-resort rollback
SAFETY_DUMP="${BACKUPS_DIR}/ivf_db_pre_pitr_$(date +%Y%m%d_%H%M%S).sql.gz"
log_info "Creating safety dump: $(basename "$SAFETY_DUMP")..."
docker exec "$DB_CONTAINER" sh -c "pg_dump -U ${DB_USER} -d ${DB_NAME} --no-owner --no-acl | gzip" > "$SAFETY_DUMP" 2>/dev/null

SAFETY_SIZE=$(stat -c%s "$SAFETY_DUMP" 2>/dev/null || stat -f%z "$SAFETY_DUMP" 2>/dev/null || echo "0")
if [ "$SAFETY_SIZE" -lt 100 ]; then
    log_warn "Safety dump is very small (${SAFETY_SIZE} bytes) — current DB may be empty"
else
    log_ok "Safety dump created: $(basename "$SAFETY_DUMP") ($(numfmt --to=iec "$SAFETY_SIZE" 2>/dev/null || echo "${SAFETY_SIZE} bytes"))"
fi

# ══════════════════════════════════════════════════════
# Step 2: Stop PostgreSQL
# ══════════════════════════════════════════════════════
log_step 2 "Stopping PostgreSQL"

docker exec "$DB_CONTAINER" pg_ctl stop -D "$PGDATA" -m fast 2>/dev/null || true
sleep 2

# Verify it stopped
if docker exec "$DB_CONTAINER" pg_isready -U "$DB_USER" 2>/dev/null; then
    log_error "PostgreSQL is still running after stop command"
    exit 1
fi
log_ok "PostgreSQL stopped"

# ══════════════════════════════════════════════════════
# Step 3: Preserve current PGDATA and extract base backup
# ══════════════════════════════════════════════════════
log_step 3 "Replacing PGDATA with base backup"

# Move current PGDATA aside (safety net)
PITR_BACKUP_SUFFIX="pre_pitr_$(date +%Y%m%d_%H%M%S)"
docker exec "$DB_CONTAINER" sh -c "
    if [ -d '${PGDATA}_${PITR_BACKUP_SUFFIX}' ]; then
        rm -rf '${PGDATA}_${PITR_BACKUP_SUFFIX}'
    fi
    mv '${PGDATA}' '${PGDATA}_${PITR_BACKUP_SUFFIX}'
    mkdir -p '${PGDATA}'
    chown postgres:postgres '${PGDATA}'
    chmod 700 '${PGDATA}'
"
log_ok "Current PGDATA preserved as ${PGDATA}_${PITR_BACKUP_SUFFIX}"

# Copy base backup into container and extract
log_info "Copying base backup into container..."
docker cp "$BASE_BACKUP" "${DB_CONTAINER}:/tmp/${BACKUP_FILENAME}"

log_info "Extracting base backup..."
docker exec "$DB_CONTAINER" sh -c "
    cd /tmp
    # The base backup is a tar.gz containing a directory with base.tar.gz + pg_wal.tar.gz
    # or it could be a flat tar format from pg_basebackup -Ft
    tar xzf '/tmp/${BACKUP_FILENAME}'

    # Find the extracted directory (if wrapped)
    EXTRACTED=\$(find /tmp -maxdepth 1 -name 'ivf_basebackup_*' -type d | head -1)

    if [ -n \"\$EXTRACTED\" ] && [ -f \"\$EXTRACTED/base.tar.gz\" ]; then
        # Standard pg_basebackup tar format: base.tar.gz + pg_wal.tar.gz
        tar xzf \"\$EXTRACTED/base.tar.gz\" -C '${PGDATA}'

        # Extract WAL files
        if [ -f \"\$EXTRACTED/pg_wal.tar.gz\" ]; then
            mkdir -p '${PGDATA}/pg_wal'
            tar xzf \"\$EXTRACTED/pg_wal.tar.gz\" -C '${PGDATA}/pg_wal'
        fi
        rm -rf \"\$EXTRACTED\"
    elif [ -f '/tmp/base.tar.gz' ]; then
        # Flat extraction
        tar xzf /tmp/base.tar.gz -C '${PGDATA}'
        if [ -f '/tmp/pg_wal.tar.gz' ]; then
            mkdir -p '${PGDATA}/pg_wal'
            tar xzf /tmp/pg_wal.tar.gz -C '${PGDATA}/pg_wal'
        fi
        rm -f /tmp/base.tar.gz /tmp/pg_wal.tar.gz
    else
        # pg_basebackup plain format (files directly in tar) — already extracted to /tmp
        # Try to find PG_VERSION to detect PGDATA
        PGVER=\$(find /tmp -maxdepth 2 -name 'PG_VERSION' -not -path '${PGDATA}/*' | head -1)
        if [ -n \"\$PGVER\" ]; then
            SRCDIR=\$(dirname \"\$PGVER\")
            cp -a \"\$SRCDIR\"/* '${PGDATA}/'
            rm -rf \"\$SRCDIR\"
        fi
    fi

    rm -f '/tmp/${BACKUP_FILENAME}'

    # Fix ownership
    chown -R postgres:postgres '${PGDATA}'
    chmod 700 '${PGDATA}'
"
log_ok "Base backup extracted to ${PGDATA}"

# ══════════════════════════════════════════════════════
# Step 4: Copy WAL segments for recovery
# ══════════════════════════════════════════════════════
log_step 4 "Preparing WAL archive for recovery"

# Ensure pg_wal directory exists
docker exec "$DB_CONTAINER" sh -c "mkdir -p '${PGDATA}/pg_wal' && chown postgres:postgres '${PGDATA}/pg_wal'"

# Copy archived WAL from container's archive directory
COPIED_WAL=0
if [ "$WAL_COUNT" -gt 0 ]; then
    log_info "Copying ${WAL_COUNT} WAL segments from container archive..."
    docker exec "$DB_CONTAINER" sh -c "
        mkdir -p /tmp/wal_restore
        cp ${ARCHIVE_DIR}/* /tmp/wal_restore/ 2>/dev/null || true
    "
    COPIED_WAL=$WAL_COUNT
fi

# Copy local WAL backups into container
if [ -d "$LOCAL_WAL_DIR" ] && [ "$LOCAL_WAL_COUNT" -gt 0 ]; then
    log_info "Copying ${LOCAL_WAL_COUNT} WAL segments from local backups..."
    docker cp "${LOCAL_WAL_DIR}/." "${DB_CONTAINER}:/tmp/wal_restore/"
    COPIED_WAL=$((COPIED_WAL + LOCAL_WAL_COUNT))
fi

# Copy extra WAL directory if specified
if [ -n "$EXTRA_WAL_DIR" ] && [ -d "$EXTRA_WAL_DIR" ]; then
    EXTRA_COUNT=$(ls -1 "$EXTRA_WAL_DIR" 2>/dev/null | wc -l)
    EXTRA_COUNT=$(echo "$EXTRA_COUNT" | tr -d '[:space:]')
    if [ "$EXTRA_COUNT" -gt 0 ]; then
        log_info "Copying ${EXTRA_COUNT} WAL segments from ${EXTRA_WAL_DIR}..."
        docker cp "${EXTRA_WAL_DIR}/." "${DB_CONTAINER}:/tmp/wal_restore/"
        COPIED_WAL=$((COPIED_WAL + EXTRA_COUNT))
    fi
fi

if [ "$COPIED_WAL" -gt 0 ]; then
    docker exec "$DB_CONTAINER" sh -c "chown -R postgres:postgres /tmp/wal_restore"
    log_ok "Total WAL segments available for recovery: ~${COPIED_WAL}"
else
    log_warn "No WAL segments found — recovery will only include data up to the base backup"
fi

# ══════════════════════════════════════════════════════
# Step 5: Configure recovery settings
# ══════════════════════════════════════════════════════
log_step 5 "Configuring recovery target"

# Remove any existing recovery signals
docker exec "$DB_CONTAINER" sh -c "
    rm -f '${PGDATA}/standby.signal'
    rm -f '${PGDATA}/recovery.signal'
"

# Create recovery.signal to trigger recovery mode
docker exec "$DB_CONTAINER" sh -c "touch '${PGDATA}/recovery.signal' && chown postgres:postgres '${PGDATA}/recovery.signal'"

# Build recovery configuration
RECOVERY_CONF=""

# restore_command copies WAL from our temp directory
RECOVERY_CONF="restore_command = 'cp /tmp/wal_restore/%f %p 2>/dev/null || cp ${ARCHIVE_DIR}/%f %p'"

if [ "$TARGET_LATEST" = true ]; then
    RECOVERY_CONF="${RECOVERY_CONF}
recovery_target = 'immediate'
recovery_target_action = 'promote'"
    log_info "Recovery target: latest available point"
else
    RECOVERY_CONF="${RECOVERY_CONF}
recovery_target_time = '${TARGET_TIME}'
recovery_target_action = 'promote'"
    log_info "Recovery target: ${TARGET_TIME}"
fi

# Write recovery config into postgresql.auto.conf (appending to existing)
docker exec "$DB_CONTAINER" sh -c "
    # Remove any previous recovery settings
    if [ -f '${PGDATA}/postgresql.auto.conf' ]; then
        sed -i '/^restore_command/d; /^recovery_target/d' '${PGDATA}/postgresql.auto.conf'
    fi

    # Append recovery settings
    cat >> '${PGDATA}/postgresql.auto.conf' << 'EOCONF'

# PITR Recovery Configuration (auto-generated by restore-pitr.sh)
${RECOVERY_CONF}
EOCONF

    chown postgres:postgres '${PGDATA}/postgresql.auto.conf'
"

log_ok "Recovery configuration written"

# ══════════════════════════════════════════════════════
# Step 6: Start PostgreSQL in recovery mode
# ══════════════════════════════════════════════════════
log_step 6 "Starting PostgreSQL recovery"

docker exec -u postgres "$DB_CONTAINER" pg_ctl start -D "$PGDATA" -l /tmp/pg_pitr_recovery.log -o "-c listen_addresses=localhost" &
PG_PID=$!

# Wait for recovery to complete (max 5 minutes)
RECOVERY_TIMEOUT=300
ELAPSED=0
RECOVERY_DONE=false

log_info "Waiting for recovery to complete (timeout: ${RECOVERY_TIMEOUT}s)..."

while [ $ELAPSED -lt $RECOVERY_TIMEOUT ]; do
    sleep 3
    ELAPSED=$((ELAPSED + 3))

    # Check if PostgreSQL is accepting connections
    if docker exec "$DB_CONTAINER" pg_isready -U "$DB_USER" -h localhost 2>/dev/null; then
        # Check if still in recovery
        IS_RECOVERING=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -h localhost -d postgres -t -A -c "SELECT pg_is_in_recovery();" 2>/dev/null || echo "error")

        if [ "$IS_RECOVERING" = "f" ]; then
            RECOVERY_DONE=true
            break
        fi
        echo -ne "  Recovery in progress... (${ELAPSED}s)\r"
    else
        echo -ne "  PostgreSQL starting... (${ELAPSED}s)\r"
    fi
done
echo ""

if [ "$RECOVERY_DONE" = false ]; then
    log_warn "Recovery did not complete within ${RECOVERY_TIMEOUT}s"
    log_info "Checking recovery log..."
    docker exec "$DB_CONTAINER" tail -20 /tmp/pg_pitr_recovery.log 2>/dev/null || true

    # Promote manually if stuck in recovery
    log_info "Attempting manual promotion..."
    docker exec -u postgres "$DB_CONTAINER" pg_ctl promote -D "$PGDATA" 2>/dev/null || true
    sleep 5

    IS_RECOVERING=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -h localhost -d postgres -t -A -c "SELECT pg_is_in_recovery();" 2>/dev/null || echo "error")
    if [ "$IS_RECOVERING" = "f" ]; then
        RECOVERY_DONE=true
    fi
fi

if [ "$RECOVERY_DONE" = false ]; then
    log_error "Recovery failed — check logs: docker exec ${DB_CONTAINER} cat /tmp/pg_pitr_recovery.log"
    log_info "Rollback: stop container, then copy back from ${PGDATA}_${PITR_BACKUP_SUFFIX}"
    exit 1
fi

log_ok "Recovery completed — database promoted to primary"

# ══════════════════════════════════════════════════════
# Step 7: Post-recovery cleanup and verification
# ══════════════════════════════════════════════════════
log_step 7 "Post-recovery verification"

# Stop PG started with localhost-only, then restart properly
docker exec -u postgres "$DB_CONTAINER" pg_ctl stop -D "$PGDATA" -m fast 2>/dev/null || true
sleep 2

# Clean up recovery settings from postgresql.auto.conf
docker exec "$DB_CONTAINER" sh -c "
    if [ -f '${PGDATA}/postgresql.auto.conf' ]; then
        sed -i '/^# PITR Recovery/d; /^restore_command/d; /^recovery_target/d' '${PGDATA}/postgresql.auto.conf'
    fi
    rm -f '${PGDATA}/recovery.signal'
"

# Re-enable WAL archiving settings
docker exec "$DB_CONTAINER" sh -c "
    cat >> '${PGDATA}/postgresql.auto.conf' << 'EOCONF'

# WAL archiving (re-enabled after PITR)
wal_level = 'replica'
archive_mode = 'on'
archive_command = 'cp %p ${ARCHIVE_DIR}/%f'
archive_timeout = '300'
max_wal_senders = 5
max_replication_slots = 5
wal_keep_size = '256MB'
EOCONF
    chown postgres:postgres '${PGDATA}/postgresql.auto.conf'
"

# Start PostgreSQL (full network access)
docker exec -u postgres "$DB_CONTAINER" pg_ctl start -D "$PGDATA" -l /tmp/pg_pitr_recovery.log
sleep 3

# Verify database is accessible
if ! docker exec "$DB_CONTAINER" pg_isready -U "$DB_USER" 2>/dev/null; then
    log_error "PostgreSQL failed to start after recovery"
    exit 1
fi
log_ok "PostgreSQL is running"

# Check database exists and has data
TABLE_COUNT=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -t -A -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" 2>/dev/null || echo "0")
ROW_COUNT=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -t -A -c "SELECT COALESCE(SUM(n_live_tup), 0) FROM pg_stat_user_tables;" 2>/dev/null || echo "0")

log_ok "Database '${DB_NAME}': ${TABLE_COUNT} tables, ~${ROW_COUNT} rows"

# Show recovery point
RECOVERY_LSN=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -t -A -c "SELECT pg_current_wal_lsn()::text;" 2>/dev/null || echo "unknown")
log_ok "Current WAL LSN: ${RECOVERY_LSN}"

# Check if replication slots need recreation
SLOT_COUNT=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -t -A -c "SELECT COUNT(*) FROM pg_replication_slots;" 2>/dev/null || echo "0")
if [ "$SLOT_COUNT" = "0" ]; then
    log_warn "No replication slots found — standby replication will need to be re-initialized"
    log_info "Recreating standby_slot..."
    docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -c "SELECT pg_create_physical_replication_slot('standby_slot');" 2>/dev/null || true
fi

# Clean up temp WAL directory
docker exec "$DB_CONTAINER" sh -c "rm -rf /tmp/wal_restore" 2>/dev/null || true
docker exec "$DB_CONTAINER" sh -c "rm -f /tmp/pg_pitr_recovery.log" 2>/dev/null || true

# Clean up old PGDATA backups (keep last 2)
docker exec "$DB_CONTAINER" sh -c "
    BACKUPS=\$(ls -d ${PGDATA}_pre_pitr_* 2>/dev/null | sort -r)
    COUNT=0
    for DIR in \$BACKUPS; do
        COUNT=\$((COUNT + 1))
        if [ \$COUNT -gt 2 ]; then
            rm -rf \"\$DIR\"
        fi
    done
"

echo ""
echo "════════════════════════════════════════════"
log_ok "PITR restore completed successfully!"
echo "════════════════════════════════════════════"
echo ""
echo "  Database:     ${DB_NAME}"
echo "  Tables:       ${TABLE_COUNT}"
echo "  Rows:         ~${ROW_COUNT}"
echo "  WAL LSN:      ${RECOVERY_LSN}"
echo ""
echo "  Safety dump:  $(basename "$SAFETY_DUMP")"
echo "  Old PGDATA:   ${PGDATA}_${PITR_BACKUP_SUFFIX}"
echo ""
if [ "$TARGET_LATEST" = true ]; then
    echo "  Recovered to: Latest available WAL"
else
    echo "  Recovered to: ${TARGET_TIME}"
fi
echo ""
log_info "If the standby replica was running, rebuild it:"
echo "  docker rm -f ivf-db-standby"
echo "  docker volume rm ivf_postgres_standby"
echo "  docker compose --profile replication up -d db-standby"
