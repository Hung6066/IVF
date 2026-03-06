#!/usr/bin/env bash
#
# IVF Platform — Post-Deploy Health Check Script
# Sử dụng: ./scripts/health-check.sh <BASE_URL> [OPTIONS]
#
# Options:
#   --timeout <seconds>   Max wait time (default: 120)
#   --retries <count>     Max retry attempts (default: 12)
#   --interval <seconds>  Retry interval (default: 10)
#   --swarm               Also check Docker Swarm service status
#   --verbose             Show detailed output
#   --help                Show this help message
#
# Examples:
#   ./scripts/health-check.sh https://ivf.clinic
#   ./scripts/health-check.sh https://staging.ivf.clinic --timeout 60
#   ./scripts/health-check.sh http://localhost:8080 --swarm --verbose
#
# Exit codes:
#   0 — All checks passed
#   1 — Critical check failed
#   2 — Warning (non-critical check failed)
#

set -euo pipefail

# ─────────────────────── Defaults ───────────────────────
BASE_URL=""
MAX_RETRIES=12
RETRY_INTERVAL=10
TIMEOUT=120
CHECK_SWARM=false
VERBOSE=false

# ─────────────────────── Colors ───────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# ─────────────────────── Counters ───────────────────────
CHECKS_PASSED=0
CHECKS_WARNED=0
CHECKS_FAILED=0

# ─────────────────────── Functions ───────────────────────
log_info()  { echo -e "${BLUE}[INFO]${NC}  $*"; }
log_ok()    { echo -e "${GREEN}[PASS]${NC}  $*"; CHECKS_PASSED=$((CHECKS_PASSED + 1)); }
log_warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; CHECKS_WARNED=$((CHECKS_WARNED + 1)); }
log_fail()  { echo -e "${RED}[FAIL]${NC}  $*"; CHECKS_FAILED=$((CHECKS_FAILED + 1)); }
log_debug() { [ "$VERBOSE" = "true" ] && echo -e "       $*" || true; }

usage() {
    head -22 "$0" | grep '^#' | sed 's/^# \?//'
    exit 0
}

check_url() {
    local URL="$1"
    local DESCRIPTION="$2"
    local EXPECTED_CODE="${3:-200}"

    log_info "Checking: ${DESCRIPTION}"
    log_debug "URL: ${URL}"

    local HTTP_CODE RESPONSE_TIME
    HTTP_CODE=$(curl -so /dev/null -w "%{http_code}" \
        --max-time 15 "$URL" 2>/dev/null || echo "000")

    RESPONSE_TIME=$(curl -so /dev/null -w "%{time_total}" \
        --max-time 15 "$URL" 2>/dev/null || echo "0")

    if [ "$HTTP_CODE" = "$EXPECTED_CODE" ]; then
        log_ok "${DESCRIPTION}: HTTP ${HTTP_CODE} (${RESPONSE_TIME}s)"
        return 0
    elif [ "$HTTP_CODE" = "000" ]; then
        log_fail "${DESCRIPTION}: Connection refused / timeout"
        return 1
    else
        log_fail "${DESCRIPTION}: HTTP ${HTTP_CODE} (expected ${EXPECTED_CODE})"
        return 1
    fi
}

check_url_with_retry() {
    local URL="$1"
    local DESCRIPTION="$2"

    log_info "Checking (with retry): ${DESCRIPTION}"

    for i in $(seq 1 "$MAX_RETRIES"); do
        local HTTP_CODE
        HTTP_CODE=$(curl -so /dev/null -w "%{http_code}" \
            --max-time 10 "$URL" 2>/dev/null || echo "000")

        if [ "$HTTP_CODE" = "200" ]; then
            log_ok "${DESCRIPTION}: HTTP 200 (attempt $i)"
            return 0
        fi

        log_debug "Attempt $i/$MAX_RETRIES: HTTP ${HTTP_CODE}"
        [ "$i" -lt "$MAX_RETRIES" ] && sleep "$RETRY_INTERVAL"
    done

    log_fail "${DESCRIPTION}: Failed after ${MAX_RETRIES} attempts"
    return 1
}

check_response_contains() {
    local URL="$1"
    local NEEDLE="$2"
    local DESCRIPTION="$3"

    log_info "Checking: ${DESCRIPTION}"

    local BODY
    BODY=$(curl -s --max-time 15 "$URL" 2>/dev/null || echo "")

    if echo "$BODY" | grep -q "$NEEDLE"; then
        log_ok "${DESCRIPTION}: Contains '${NEEDLE}'"
        return 0
    else
        log_fail "${DESCRIPTION}: Missing '${NEEDLE}'"
        log_debug "Body (first 200 chars): $(echo "$BODY" | head -c 200)"
        return 1
    fi
}

check_response_time() {
    local URL="$1"
    local MAX_MS="$2"
    local DESCRIPTION="$3"

    log_info "Checking: ${DESCRIPTION}"

    local RESPONSE_TIME
    RESPONSE_TIME=$(curl -so /dev/null -w "%{time_total}" \
        --max-time 15 "$URL" 2>/dev/null || echo "999")

    local RESPONSE_MS
    RESPONSE_MS=$(echo "$RESPONSE_TIME * 1000" | bc 2>/dev/null | cut -d. -f1 || echo "999")

    if [ "${RESPONSE_MS:-999}" -le "$MAX_MS" ]; then
        log_ok "${DESCRIPTION}: ${RESPONSE_MS}ms (threshold: ${MAX_MS}ms)"
        return 0
    else
        log_warn "${DESCRIPTION}: ${RESPONSE_MS}ms exceeds ${MAX_MS}ms"
        return 0  # Warning only, not failure
    fi
}

check_swarm_services() {
    if [ "$CHECK_SWARM" != "true" ]; then
        return 0
    fi

    if ! command -v docker &>/dev/null; then
        log_warn "Docker not available — skipping Swarm checks"
        return 0
    fi

    echo ""
    log_info "═══ Docker Swarm Checks ═══"

    # Check ivf_api
    local API_REPLICAS
    API_REPLICAS=$(docker service ls --format "{{.Replicas}}" --filter "name=ivf_api" 2>/dev/null || echo "?/?")
    local RUNNING DESIRED
    RUNNING=$(echo "$API_REPLICAS" | cut -d'/' -f1)
    DESIRED=$(echo "$API_REPLICAS" | cut -d'/' -f2)

    if [ "$RUNNING" = "$DESIRED" ] && [ "$RUNNING" != "?" ]; then
        log_ok "Service ivf_api: ${RUNNING}/${DESIRED} replicas"
    else
        log_fail "Service ivf_api: ${RUNNING}/${DESIRED} replicas"
    fi

    # Check other services
    local SERVICES=("ivf_caddy" "ivf_db" "ivf_redis")
    for SVC in "${SERVICES[@]}"; do
        local REPS
        REPS=$(docker service ls --format "{{.Replicas}}" --filter "name=${SVC}" 2>/dev/null || echo "")
        if [ -n "$REPS" ]; then
            local R D
            R=$(echo "$REPS" | cut -d'/' -f1)
            D=$(echo "$REPS" | cut -d'/' -f2)
            if [ "$R" = "$D" ]; then
                log_ok "Service ${SVC}: ${R}/${D} replicas"
            else
                log_warn "Service ${SVC}: ${R}/${D} replicas"
            fi
        fi
    done

    # Check update status
    local UPDATE_STATE
    UPDATE_STATE=$(docker service inspect ivf_api \
        --format '{{.UpdateStatus.State}}' 2>/dev/null || echo "unknown")

    if [ "$UPDATE_STATE" = "completed" ] || [ "$UPDATE_STATE" = "" ]; then
        log_ok "Update status: ${UPDATE_STATE:-none}"
    elif [ "$UPDATE_STATE" = "rollback_completed" ]; then
        log_fail "Update status: rollback_completed (update failed)"
    else
        log_warn "Update status: ${UPDATE_STATE}"
    fi

    # Check error logs
    local ERROR_COUNT
    ERROR_COUNT=$(docker service logs ivf_api --since 5m 2>&1 \
        | grep -ci "error\|exception\|fatal" || true)

    if [ "$ERROR_COUNT" -le 3 ]; then
        log_ok "Error logs (5min): ${ERROR_COUNT}"
    else
        log_warn "Error logs (5min): ${ERROR_COUNT} — investigate"
    fi

    # Check node status
    log_info "--- Node Status ---"
    docker node ls --format "table {{.Hostname}}\t{{.Status}}\t{{.Availability}}\t{{.ManagerStatus}}" 2>/dev/null || true
}

# ─────────────────────── Parse Args ───────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --timeout)
            TIMEOUT="$2"
            shift 2
            ;;
        --retries)
            MAX_RETRIES="$2"
            shift 2
            ;;
        --interval)
            RETRY_INTERVAL="$2"
            shift 2
            ;;
        --swarm)
            CHECK_SWARM=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        --help|-h)
            usage
            ;;
        -*)
            echo "Unknown option: $1"
            usage
            ;;
        *)
            BASE_URL="$1"
            shift
            ;;
    esac
done

if [ -z "$BASE_URL" ]; then
    echo "Error: BASE_URL required"
    echo ""
    usage
fi

# Remove trailing slash
BASE_URL="${BASE_URL%/}"

# ─────────────────────── Main ───────────────────────
echo ""
echo "╔══════════════════════════════════════════╗"
echo "║   IVF Platform — Health Check            ║"
echo "╚══════════════════════════════════════════╝"
echo ""
log_info "Target: ${BASE_URL}"
log_info "Timeout: ${TIMEOUT}s | Retries: ${MAX_RETRIES} | Interval: ${RETRY_INTERVAL}s"
echo ""

START_TIME=$(date +%s)

# ─── 1. API Liveness (with retry) ───
log_info "═══ API Health Checks ═══"
check_url_with_retry "${BASE_URL}/health/live" "API Liveness" || true

# ─── 2. API Readiness ───
check_url "${BASE_URL}/health/ready" "API Readiness (DB + Redis)" || true

# ─── 3. Frontend SPA ───
echo ""
log_info "═══ Frontend Checks ═══"
check_url "${BASE_URL}/" "Frontend SPA" || true
check_response_contains "${BASE_URL}/" "app-root" "Angular App Bootstrap" || true

# ─── 4. Response Time ───
echo ""
log_info "═══ Performance Checks ═══"
check_response_time "${BASE_URL}/health/live" 2000 "API Response Time (<2s)" || true

# ─── 5. Swarm Services ───
check_swarm_services

# ─── Summary ───
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

echo ""
echo "═══════════════════════════════════════════"
echo -e "  ${GREEN}Passed: ${CHECKS_PASSED}${NC}  |  ${YELLOW}Warnings: ${CHECKS_WARNED}${NC}  |  ${RED}Failed: ${CHECKS_FAILED}${NC}"
echo "  Duration: ${DURATION}s"
echo "═══════════════════════════════════════════"
echo ""

if [ "$CHECKS_FAILED" -gt 0 ]; then
    log_fail "Health check FAILED (${CHECKS_FAILED} critical failures)"
    exit 1
elif [ "$CHECKS_WARNED" -gt 0 ]; then
    log_warn "Health check PASSED with warnings"
    exit 2
else
    log_ok "All health checks PASSED"
    exit 0
fi
