#!/bin/bash
# =====================================================
# EJBCA — Remove Public Access Role (Security Hardening)
# =====================================================
# Removes the "Public Access Group" role from EJBCA so that
# only clients presenting a valid client certificate can
# access the admin web interface.
#
# PREREQUISITES:
#   1. Client cert (admin.p12) imported in your browser
#   2. Verified you CAN access EJBCA admin web WITH the cert
#   3. SSH tunnel active (port 18443 → 8443)
#
# Run:
#   bash scripts/secure-ejbca-access.sh
#
# To UNDO (re-add public access if locked out):
#   bash scripts/secure-ejbca-access.sh --restore
#
# The script is idempotent — safe to re-run.
# =====================================================

set -euo pipefail

CONTAINER_NAME="${EJBCA_CONTAINER:-ivf-ejbca}"
EJBCA_CLI="/opt/keyfactor/bin/ejbca.sh"
ROLE_NAME="Public Access Group"
MODE="${1:-}"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Detect host vs container
if [ -f "$EJBCA_CLI" ]; then
    EXEC_PREFIX=""
else
    EXEC_PREFIX="docker exec ${CONTAINER_NAME}"
fi

run_ejbca() { $EXEC_PREFIX "$EJBCA_CLI" "$@" 2>&1; }

# ─── Pre-flight ───
log_info "Checking prerequisites..."
if [ -n "$EXEC_PREFIX" ]; then
    if ! docker inspect "$CONTAINER_NAME" --format='{{.State.Status}}' 2>/dev/null | grep -q running; then
        log_error "Container '$CONTAINER_NAME' is not running"
        exit 1
    fi
fi

# Verify Super Administrator Role has at least one member
SA_MEMBERS=$(run_ejbca roles listadmins --role "Super Administrator Role" || true)
if echo "$SA_MEMBERS" | grep -q "Match with"; then
    log_info "  ✓ Super Administrator Role has members"
else
    log_error "  Super Administrator Role has NO members!"
    log_error "  Refusing to remove Public Access — would lock you out."
    exit 1
fi

if [ "$MODE" = "--restore" ]; then
    # ─── Restore: Re-add Public Access Group ───
    log_warn "RESTORE MODE: Re-adding Public Access Group..."

    # Check if role exists
    ROLE_CHECK=$(run_ejbca roles listroles || true)
    if echo "$ROLE_CHECK" | grep -q "$ROLE_NAME"; then
        log_info "  Role '$ROLE_NAME' already exists"
    else
        log_info "  Creating role '$ROLE_NAME'..."
        run_ejbca roles addrole --role "$ROLE_NAME"
    fi

    # Add TRANSPORT_ANY member (allows any HTTPS connection)
    log_info "  Adding TRANSPORT_ANY member..."
    run_ejbca roles addrolemember \
        --role "$ROLE_NAME" \
        --caname "" \
        --with "SpecialAccessAuthenticationToken:TRANSPORT_ANY" \
        --value "" 2>&1 || true

    # Add access rules for admin web
    log_info "  Adding access rules..."
    run_ejbca roles changerule \
        --role "$ROLE_NAME" \
        --rule "/administrator" \
        --state ACCEPT 2>&1 || true
    run_ejbca roles changerule \
        --role "$ROLE_NAME" \
        --rule "/" \
        --state ACCEPT 2>&1 || true

    log_info "═══════════════════════════════════════"
    log_info "  Public Access Group RESTORED"
    log_info "  Anyone with HTTPS access can admin"
    log_info "═══════════════════════════════════════"
    exit 0
fi

# ─── Remove: Delete Public Access Group ───
log_info "Listing current roles..."
ROLES=$(run_ejbca roles listroles || true)
echo "$ROLES"

if echo "$ROLES" | grep -q "$ROLE_NAME"; then
    log_warn "Found '$ROLE_NAME' — removing..."

    # List members first (for audit log)
    log_info "Current members of '$ROLE_NAME':"
    run_ejbca roles listadmins --role "$ROLE_NAME" || true

    # Remove the role entirely
    run_ejbca roles removerole --role "$ROLE_NAME"
    log_info "  ✓ Role '$ROLE_NAME' removed"
else
    log_info "  ✓ Role '$ROLE_NAME' does not exist (already removed)"
fi

# ─── Verify ───
log_info "Verification:"
ROLES_AFTER=$(run_ejbca roles listroles || true)
if echo "$ROLES_AFTER" | grep -q "$ROLE_NAME"; then
    log_error "  ✗ Role still exists!"
    exit 1
else
    log_info "  ✓ '$ROLE_NAME' confirmed removed"
fi

echo ""
log_info "═══════════════════════════════════════"
log_info "  EJBCA Public Access Role REMOVED"
log_info "  Only client cert holders can admin"
log_info "  To undo: bash $0 --restore"
log_info "═══════════════════════════════════════"
