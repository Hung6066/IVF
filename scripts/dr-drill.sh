#!/bin/bash
# =============================================
# IVF DR Drill Script
# Automated disaster recovery drill that validates:
#   1. Backup restoration capability
#   2. Replication health & lag
#   3. Failover procedure (read-only test)
#   4. Service health checks
#   5. Data integrity verification
# Usage: ./scripts/dr-drill.sh [--full] [--report-only]
# =============================================

set -euo pipefail

FULL_DRILL=false
REPORT_ONLY=false
LOG_PREFIX="[dr-drill]"
REPORT_FILE="/tmp/ivf-dr-drill-$(date +%Y%m%d_%H%M%S).txt"
PASS_COUNT=0
FAIL_COUNT=0
WARN_COUNT=0
STANDBY_CONTAINER="${DB_STANDBY_CONTAINER:-ivf-db-standby}"
PRIMARY_CONTAINER="${DB_PRIMARY_CONTAINER:-ivf-db}"

for arg in "$@"; do
    case $arg in
        --full)        FULL_DRILL=true ;;
        --report-only) REPORT_ONLY=true ;;
    esac
done

log() { echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') $LOG_PREFIX $1" | tee -a "$REPORT_FILE"; }
pass() { log "✓ PASS: $1"; ((PASS_COUNT++)); }
fail() { log "✗ FAIL: $1"; ((FAIL_COUNT++)); }
warn() { log "⚠ WARN: $1"; ((WARN_COUNT++)); }

log "═══════════════════════════════════════════════════"
log "  IVF Disaster Recovery Drill — $(date -u '+%Y-%m-%d %H:%M UTC')"
log "  Mode: $([ "$FULL_DRILL" = true ] && echo 'FULL' || echo 'BASIC')"
log "═══════════════════════════════════════════════════"
echo ""

# ── Check 1: Primary database health ────────────────

log "── Check 1: Primary Database Health ──"
if docker exec "$PRIMARY_CONTAINER" pg_isready -U postgres &>/dev/null 2>&1; then
    pass "Primary database ($PRIMARY_CONTAINER) is responding"
else
    fail "Primary database ($PRIMARY_CONTAINER) is not responding"
fi

# Verify WAL archiving is enabled
wal_status=$(docker exec "$PRIMARY_CONTAINER" psql -U postgres -tAc \
    "SELECT setting FROM pg_settings WHERE name='archive_mode';" 2>/dev/null || echo "unknown")
if [[ "$wal_status" == "on" ]]; then
    pass "WAL archiving is enabled"
else
    warn "WAL archiving is $wal_status"
fi

# ── Check 2: Standby / replication health ────────────

log ""
log "── Check 2: Replication Health ──"
if docker exec "$STANDBY_CONTAINER" pg_isready -U postgres &>/dev/null 2>&1; then
    pass "Standby database ($STANDBY_CONTAINER) is responding"

    # Check replication state
    in_recovery=$(docker exec "$STANDBY_CONTAINER" psql -U postgres -tAc \
        "SELECT pg_is_in_recovery();" 2>/dev/null || echo "unknown")
    if [[ "$in_recovery" == "t" ]]; then
        pass "Standby is in recovery mode (receiving WAL)"
    else
        fail "Standby is NOT in recovery mode"
    fi

    # Check replication lag
    lag=$(docker exec "$STANDBY_CONTAINER" psql -U postgres -tAc \
        "SELECT CASE WHEN pg_last_wal_receive_lsn() = pg_last_wal_replay_lsn() THEN 0
         ELSE EXTRACT(EPOCH FROM now() - pg_last_xact_replay_timestamp())::int END;" 2>/dev/null || echo "-1")
    if [[ "$lag" == "-1" ]]; then
        warn "Could not determine replication lag"
    elif (( lag <= 5 )); then
        pass "Replication lag: ${lag}s (excellent)"
    elif (( lag <= 30 )); then
        warn "Replication lag: ${lag}s (acceptable)"
    else
        fail "Replication lag: ${lag}s (too high)"
    fi

    # Check streaming replication from primary
    rep_count=$(docker exec "$PRIMARY_CONTAINER" psql -U postgres -tAc \
        "SELECT count(*) FROM pg_stat_replication;" 2>/dev/null || echo "0")
    if (( rep_count > 0 )); then
        pass "Primary has $rep_count streaming replication connections"
    else
        warn "No streaming replication connections found on primary"
    fi
else
    fail "Standby database ($STANDBY_CONTAINER) is not responding"
    warn "Skipping replication checks"
fi

# ── Check 3: Backup verification ────────────────────

log ""
log "── Check 3: Backup Verification ──"

# Check for recent backups
backup_count=$(find /opt/ivf/backups/ -name "*.sql.gz" -mtime -1 2>/dev/null | wc -l || echo "0")
if (( backup_count > 0 )); then
    pass "Found $backup_count backup(s) from last 24 hours"
else
    backup_count=$(ls -1 backups/*.sha256 2>/dev/null | wc -l || echo "0")
    if (( backup_count > 0 )); then
        pass "Found $backup_count backup checksums"
    else
        warn "No recent backups found"
    fi
fi

# ── Check 4: API service health ──────────────────────

log ""
log "── Check 4: API Service Health ──"

api_url="${API_URL:-http://localhost:5000}"

if curl -sf "$api_url/health" &>/dev/null; then
    pass "API health endpoint is responding"
else
    fail "API health endpoint is not responding"
fi

# Check API readiness
if curl -sf "$api_url/health/ready" &>/dev/null; then
    pass "API readiness check passed"
else
    warn "API readiness check not available"
fi

# ── Check 5: Redis health ───────────────────────────

log ""
log "── Check 5: Redis Health ──"

if docker exec ivf-redis redis-cli ping 2>/dev/null | grep -q "PONG"; then
    pass "Redis is responding"
else
    warn "Redis is not responding (caching degraded)"
fi

# ── Check 6: MinIO health ───────────────────────────

log ""
log "── Check 6: MinIO Object Storage Health ──"

if curl -sf "http://localhost:9000/minio/health/live" &>/dev/null; then
    pass "MinIO is healthy"
else
    fail "MinIO is not responding"
fi

# Verify buckets exist
for bucket in ivf-documents ivf-signed-pdfs ivf-medical-images ivf-audit-archive; do
    if docker exec ivf-minio mc ls local/"$bucket" &>/dev/null 2>&1; then
        pass "Bucket $bucket exists"
    else
        warn "Bucket $bucket not found"
    fi
done

# ── Check 7: Full drill (optional) ──────────────────

if [[ "$FULL_DRILL" == true && "$REPORT_ONLY" != true ]]; then
    log ""
    log "── Check 7: Full Drill — Backup Restore Test ──"

    # Create a test database from latest backup
    latest_backup=$(ls -t backups/*.sql.gz.sha256 2>/dev/null | head -1 | sed 's/.sha256$//')
    if [[ -n "$latest_backup" && -f "$latest_backup" ]]; then
        log "Testing restore from $latest_backup..."
        if docker exec "$PRIMARY_CONTAINER" createdb -U postgres ivf_dr_test 2>/dev/null; then
            if gunzip -c "$latest_backup" | docker exec -i "$PRIMARY_CONTAINER" psql -U postgres ivf_dr_test &>/dev/null; then
                # Verify table count
                table_count=$(docker exec "$PRIMARY_CONTAINER" psql -U postgres -tAc \
                    "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';" ivf_dr_test 2>/dev/null || echo "0")
                if (( table_count > 0 )); then
                    pass "Backup restore test: $table_count tables restored"
                else
                    fail "Backup restore test: no tables found"
                fi
            else
                fail "Backup restore test: restore command failed"
            fi
            # Clean up test database
            docker exec "$PRIMARY_CONTAINER" dropdb -U postgres ivf_dr_test 2>/dev/null || true
        else
            warn "Could not create test database for restore verification"
        fi
    else
        warn "No backup file found for restore test"
    fi

    log ""
    log "── Check 8: Standby Read-Only Query Test ──"
    if docker exec "$STANDBY_CONTAINER" pg_isready -U postgres &>/dev/null 2>&1; then
        patient_count=$(docker exec "$STANDBY_CONTAINER" psql -U postgres ivf_db -tAc \
            "SELECT count(*) FROM \"Patients\";" 2>/dev/null || echo "-1")
        if [[ "$patient_count" != "-1" ]]; then
            pass "Standby read-only query succeeded: $patient_count patients"
        else
            warn "Standby read-only query failed (table may not exist)"
        fi
    fi
fi

# ── Report Summary ──────────────────────────────────

log ""
log "═══════════════════════════════════════════════════"
log "  DR Drill Summary"
log "  PASS: $PASS_COUNT  |  WARN: $WARN_COUNT  |  FAIL: $FAIL_COUNT"

if (( FAIL_COUNT == 0 )); then
    log "  Status: ✓ ALL CRITICAL CHECKS PASSED"
else
    log "  Status: ✗ FAILURES DETECTED — action required"
fi

log "  Report: $REPORT_FILE"
log "═══════════════════════════════════════════════════"

exit $((FAIL_COUNT > 0 ? 1 : 0))
