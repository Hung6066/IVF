#!/usr/bin/env bash
#
# IVF Platform — Docker Swarm Deploy Script
# Sử dụng: ./scripts/deploy.sh <IMAGE_TAG> [OPTIONS]
#
# Options:
#   --service <name>     Service name (default: ivf_api)
#   --registry <url>     Registry URL (default: ghcr.io/hung6066/ivf)
#   --skip-backup        Skip pre-deploy database backup
#   --skip-health        Skip post-deploy health check
#   --dry-run            Show what would be done without executing
#   --rollback           Rollback service to previous version
#   --help               Show this help message
#
# Examples:
#   ./scripts/deploy.sh sha-abc1234
#   ./scripts/deploy.sh v1.2.0
#   ./scripts/deploy.sh v1.2.0 --dry-run
#   ./scripts/deploy.sh --rollback
#

set -euo pipefail

# ─────────────────────── Defaults ───────────────────────
SERVICE_NAME="ivf_api"
REGISTRY="ghcr.io/hung6066/ivf"
HEALTH_URL="http://localhost:8080/health/live"
HEALTH_RETRIES=12
HEALTH_INTERVAL=10
BACKUP_DIR="/opt/ivf/backups"
IMAGE_TAG=""
SKIP_BACKUP=false
SKIP_HEALTH=false
DRY_RUN=false
DO_ROLLBACK=false

# ─────────────────────── Colors ───────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# ─────────────────────── Functions ───────────────────────
log_info()  { echo -e "${BLUE}[INFO]${NC}  $*"; }
log_ok()    { echo -e "${GREEN}[OK]${NC}    $*"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }

usage() {
    head -20 "$0" | grep '^#' | sed 's/^# \?//'
    exit 0
}

check_prerequisites() {
    if ! command -v docker &>/dev/null; then
        log_error "Docker not installed"
        exit 1
    fi

    if ! docker info --format '{{.Swarm.LocalNodeState}}' 2>/dev/null | grep -q "active"; then
        log_error "Docker Swarm not active on this node"
        exit 1
    fi

    local NODE_ROLE
    NODE_ROLE=$(docker info --format '{{.Swarm.ControlAvailable}}' 2>/dev/null || echo "false")
    if [ "$NODE_ROLE" != "true" ]; then
        log_error "This node is not a Swarm manager. Run deploy on manager node."
        exit 1
    fi
}

backup_database() {
    if [ "$SKIP_BACKUP" = "true" ]; then
        log_warn "Skipping pre-deploy backup (--skip-backup)"
        return 0
    fi

    log_info "Creating pre-deploy database backup..."
    mkdir -p "$BACKUP_DIR"

    local TIMESTAMP
    TIMESTAMP=$(date +%Y%m%d_%H%M%S)
    local BACKUP_FILE="${BACKUP_DIR}/pre_deploy_${TIMESTAMP}.sql.gz"

    local DB_CONTAINER
    DB_CONTAINER=$(docker ps -q -f name=ivf_db --format "{{.ID}}" | head -1)

    if [ -z "$DB_CONTAINER" ]; then
        log_warn "Database container not found, skipping backup"
        return 0
    fi

    docker exec "$DB_CONTAINER" pg_dump -U postgres ivf_db 2>/dev/null | gzip > "$BACKUP_FILE"
    local BACKUP_SIZE
    BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
    log_ok "Backup created: $BACKUP_FILE ($BACKUP_SIZE)"
}

pull_image() {
    local FULL_IMAGE="${REGISTRY}:${IMAGE_TAG}"
    log_info "Pulling image: ${FULL_IMAGE}"

    if [ "$DRY_RUN" = "true" ]; then
        log_warn "[DRY RUN] Would pull: ${FULL_IMAGE}"
        return 0
    fi

    if ! docker pull "$FULL_IMAGE"; then
        log_error "Failed to pull image: ${FULL_IMAGE}"
        log_info "Ensure GHCR login: echo \$TOKEN | docker login ghcr.io -u hung6066 --password-stdin"
        exit 1
    fi

    log_ok "Image pulled: ${FULL_IMAGE}"
}

get_current_image() {
    docker service inspect "$SERVICE_NAME" \
        --format '{{.Spec.TaskTemplate.ContainerSpec.Image}}' 2>/dev/null || echo "unknown"
}

deploy_rolling_update() {
    local FULL_IMAGE="${REGISTRY}:${IMAGE_TAG}"
    local CURRENT_IMAGE
    CURRENT_IMAGE=$(get_current_image)

    echo ""
    log_info "═══════════════════════════════════════════"
    log_info "  IVF Platform — Rolling Update"
    log_info "═══════════════════════════════════════════"
    log_info "  Service:  ${SERVICE_NAME}"
    log_info "  Current:  ${CURRENT_IMAGE}"
    log_info "  New:      ${FULL_IMAGE}"
    log_info "  Strategy: start-first, parallelism=1"
    log_info "═══════════════════════════════════════════"
    echo ""

    if [ "$DRY_RUN" = "true" ]; then
        log_warn "[DRY RUN] Would update ${SERVICE_NAME} to ${FULL_IMAGE}"
        return 0
    fi

    docker service update \
        --image "$FULL_IMAGE" \
        --update-parallelism 1 \
        --update-delay 30s \
        --update-order start-first \
        --update-failure-action rollback \
        --update-monitor 60s \
        "$SERVICE_NAME"

    # Wait for convergence
    log_info "Waiting for update to converge..."
    local TIMEOUT=300
    local ELAPSED=0

    while [ $ELAPSED -lt $TIMEOUT ]; do
        local STATE
        STATE=$(docker service inspect "$SERVICE_NAME" \
            --format '{{.UpdateStatus.State}}' 2>/dev/null || echo "unknown")

        case "$STATE" in
            "completed")
                log_ok "Rolling update completed"
                return 0
                ;;
            "rollback_completed")
                log_error "Update failed — Swarm rolled back to previous version"
                return 1
                ;;
            "paused")
                log_error "Update paused due to failure — initiating rollback"
                docker service update --rollback "$SERVICE_NAME"
                return 1
                ;;
            *)
                log_info "Update state: $STATE (${ELAPSED}s/${TIMEOUT}s)"
                sleep 10
                ELAPSED=$((ELAPSED + 10))
                ;;
        esac
    done

    log_error "Update timed out after ${TIMEOUT}s — initiating rollback"
    docker service update --rollback "$SERVICE_NAME"
    return 1
}

health_check() {
    if [ "$SKIP_HEALTH" = "true" ]; then
        log_warn "Skipping health check (--skip-health)"
        return 0
    fi

    if [ "$DRY_RUN" = "true" ]; then
        log_warn "[DRY RUN] Would run health check on ${HEALTH_URL}"
        return 0
    fi

    echo ""
    log_info "Running post-deploy health checks..."
    sleep 5

    # 1. Liveness
    log_info "--- Liveness Check ---"
    local LIVE_OK=false
    for i in $(seq 1 "$HEALTH_RETRIES"); do
        local HTTP_CODE
        HTTP_CODE=$(curl -so /dev/null -w "%{http_code}" \
            --max-time 10 "$HEALTH_URL" 2>/dev/null || echo "000")

        if [ "$HTTP_CODE" = "200" ]; then
            log_ok "Liveness: OK (attempt $i)"
            LIVE_OK=true
            break
        fi
        log_info "Liveness: HTTP $HTTP_CODE (attempt $i/$HEALTH_RETRIES)"
        sleep "$HEALTH_INTERVAL"
    done

    if [ "$LIVE_OK" = "false" ]; then
        log_error "Liveness check failed after $HEALTH_RETRIES attempts"
        return 1
    fi

    # 2. Replicas
    log_info "--- Replica Check ---"
    local REPLICAS
    REPLICAS=$(docker service ls --format "{{.Replicas}}" --filter "name=$SERVICE_NAME")
    local RUNNING DESIRED
    RUNNING=$(echo "$REPLICAS" | cut -d'/' -f1)
    DESIRED=$(echo "$REPLICAS" | cut -d'/' -f2)

    if [ "$RUNNING" = "$DESIRED" ]; then
        log_ok "All $DESIRED replicas running"
    else
        log_warn "Only $RUNNING of $DESIRED replicas running"
    fi

    # 3. Error logs
    log_info "--- Error Log Check ---"
    local ERROR_COUNT
    ERROR_COUNT=$(docker service logs "$SERVICE_NAME" --since 2m 2>&1 \
        | grep -ci "error\|exception\|fatal" || true)

    if [ "$ERROR_COUNT" -le 2 ]; then
        log_ok "Error count: $ERROR_COUNT (acceptable)"
    else
        log_warn "Error count: $ERROR_COUNT in last 2 minutes"
    fi

    echo ""
    log_ok "Health check passed"
    return 0
}

do_rollback() {
    log_info "Rolling back ${SERVICE_NAME} to previous version..."

    if [ "$DRY_RUN" = "true" ]; then
        log_warn "[DRY RUN] Would rollback ${SERVICE_NAME}"
        return 0
    fi

    local CURRENT_IMAGE
    CURRENT_IMAGE=$(get_current_image)
    log_info "Current image: ${CURRENT_IMAGE}"

    docker service rollback "$SERVICE_NAME"

    sleep 10

    local NEW_IMAGE
    NEW_IMAGE=$(get_current_image)
    log_ok "Rolled back to: ${NEW_IMAGE}"

    health_check
}

# ─────────────────────── Parse Args ───────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --service)
            SERVICE_NAME="$2"
            shift 2
            ;;
        --registry)
            REGISTRY="$2"
            shift 2
            ;;
        --skip-backup)
            SKIP_BACKUP=true
            shift
            ;;
        --skip-health)
            SKIP_HEALTH=true
            shift
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --rollback)
            DO_ROLLBACK=true
            shift
            ;;
        --help|-h)
            usage
            ;;
        -*)
            log_error "Unknown option: $1"
            usage
            ;;
        *)
            IMAGE_TAG="$1"
            shift
            ;;
    esac
done

# ─────────────────────── Main ───────────────────────
echo ""
echo "╔══════════════════════════════════════════╗"
echo "║     IVF Platform — Deploy Script         ║"
echo "╚══════════════════════════════════════════╝"
echo ""

check_prerequisites

if [ "$DO_ROLLBACK" = "true" ]; then
    do_rollback
    exit $?
fi

if [ -z "$IMAGE_TAG" ]; then
    log_error "Image tag required. Usage: $0 <IMAGE_TAG>"
    echo ""
    usage
fi

if [ "$DRY_RUN" = "true" ]; then
    log_warn "=== DRY RUN MODE — no changes will be made ==="
    echo ""
fi

START_TIME=$(date +%s)

backup_database
pull_image

if deploy_rolling_update; then
    if health_check; then
        END_TIME=$(date +%s)
        DURATION=$((END_TIME - START_TIME))

        echo ""
        log_ok "═══════════════════════════════════════════"
        log_ok "  Deploy thành công!"
        log_ok "  Version: ${IMAGE_TAG}"
        log_ok "  Duration: ${DURATION}s"
        log_ok "═══════════════════════════════════════════"
        exit 0
    else
        log_error "Health check failed — initiating rollback"
        docker service update --rollback "$SERVICE_NAME"
        exit 1
    fi
else
    log_error "Deploy failed — service was rolled back"
    exit 1
fi
