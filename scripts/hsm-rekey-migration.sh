#!/bin/bash
# ============================================================
# IVF PKI – HSM Re-Key Migration
# ============================================================
# Migrates ALL SignServer worker private keys to a new HSM/token.
# Required when:
#   - Upgrading from SoftHSM2 (FIPS Level 1) to hardware HSM (FIPS Level 2+)
#   - Replacing a compromised/expired SoftHSM2 token
#   - Moving to a new PKCS#11 provider (Luna, Utimaco, nCipher, AWS CloudHSM)
#
# WHY NEW KEYS ARE GENERATED (not exported/moved):
#   SoftHSM2 keys are created with CKA_EXTRACTABLE=false, meaning they
#   cannot be exported from the token even with the SO PIN. Migration
#   requires generating fresh RSA key pairs on the new HSM, then re-issuing
#   new certificates from EJBCA against those new public keys.
#
# WHAT THIS SCRIPT DOES:
#   Phase 1 – Pre-flight: verify containers, EJBCA, SignServer, new HSM reachable
#   Phase 2 – Backup current worker configs + certificate details
#   Phase 3 – Configure new HSM token in SignServer
#   Phase 4 – For each worker (6 total):
#               a. Set PKCS#11 properties pointing to new HSM
#               b. Activate new token
#               c. Generate new RSA-4096 key on new HSM
#               d. Generate CSR from new key
#               e. Reset EJBCA end entity + sign new cert
#               f. Upload cert + full chain to SignServer
#               g. Reload & verify worker is ACTIVE
#   Phase 5 – Final verification + cleanup temp files
#
# MAINTENANCE WINDOW:
#   Workers are briefly OFFLINE during their individual key rollover (~30s each).
#   Total downtime: ~3-5 minutes for all 6 workers if run sequentially.
#   Use --worker ID to migrate one worker at a time for zero-downtime rolling.
#
# USAGE:
#   # Run on VPS:
#   scp scripts/hsm-rekey-migration.sh root@10.200.0.1:/tmp/
#   ssh root@10.200.0.1 "sed -i 's/\r//' /tmp/hsm-rekey-migration.sh && \
#       bash /tmp/hsm-rekey-migration.sh 2>&1 | tee /tmp/rekey-$(date +%Y%m%d-%H%M%S).log"
#
#   # Dry run (no changes, shows what would happen):
#   bash /tmp/hsm-rekey-migration.sh --dry-run
#
#   # Migrate single worker (zero-downtime rolling):
#   bash /tmp/hsm-rekey-migration.sh --worker 1
#   bash /tmp/hsm-rekey-migration.sh --worker 100
#
#   # Hardware HSM (Luna/Utimaco):
#   bash /tmp/hsm-rekey-migration.sh \
#       --hsm-lib /usr/safenet/lunaclient/lib/libCryptoki2_64.so \
#       --hsm-name LunaHSM \
#       --hsm-slot 0 \
#       --hsm-pin-file /run/secrets/hsm_pin
#
#   # New SoftHSM2 token (fresh token with new label):
#   bash /tmp/hsm-rekey-migration.sh \
#       --new-token-label SignServerToken-v2 \
#       --hsm-lib /usr/lib64/pkcs11/libsofthsm2.so \
#       --hsm-name SoftHSM
#
# PREREQUISITES:
#   - EJBCA running with IVF-Signing-SubCA active
#   - SignServer running with existing workers
#   - New HSM physically attached or PKCS#11 library available in container
#   - Docker secrets: softhsm_pin (existing), hsm_pin (new — if different)
#   - If hardware HSM: PKCS#11 library must be bind-mounted into SignServer container
#
# ============================================================
set -euo pipefail

# ── Colors & logging ─────────────────────────────────────────
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'
BLUE='\033[0;34m'; CYAN='\033[0;36m'; MAGENTA='\033[0;35m'; NC='\033[0m'
BOLD='\033[1m'

ok()      { echo -e "${GREEN}[OK]${NC} $*"; }
info()    { echo -e "${BLUE}[INFO]${NC} $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
err()     { echo -e "${RED}[ERROR]${NC} $*" >&2; }
step()    { echo -e "\n${MAGENTA}════════════════════════════════════════════════${NC}"
            echo -e "${MAGENTA}  ${BOLD}$*${NC}"
            echo -e "${MAGENTA}════════════════════════════════════════════════${NC}"; }
substep() { echo -e "${CYAN}  ── $*${NC}"; }
banner()  { echo -e "\n${BOLD}${BLUE}$*${NC}"; }

# ── Defaults ─────────────────────────────────────────────────
DRY_RUN=false
TARGET_WORKER=""
SKIP_BACKUP=false
FORCE=false
ROLLBACK_ON_FAILURE=true

# New HSM settings (override via flags)
NEW_HSM_LIB="/usr/lib64/pkcs11/libsofthsm2.so"
NEW_HSM_NAME="SoftHSM"
NEW_TOKEN_LABEL="SignServerToken"          # Same label = in-place re-init
NEW_HSM_SLOT=""                            # Empty = use slot label
NEW_HSM_PIN_FILE=""                        # Empty = use existing softhsm_pin secret
NEW_SO_PIN_FILE=""

# EJBCA issuing CA
ISSUING_CA="IVF-Signing-SubCA"
KEYSTORE_PASSWORD="changeit"

# Certificate profiles (matching setup-pkcs11-workers.sh)
PDF_CERT_PROFILE="IVF-PDFSigner-Profile"
PDF_EE_PROFILE="IVF-PDFSigner-EEProfile"
TSA_CERT_PROFILE="IVF-TSA-Profile"
TSA_EE_PROFILE="IVF-TSA-EEProfile"

# Workers: "ID|Name|KeyAlias|CertProfile|EEProfile|CN"
ALL_WORKERS=(
    "1|PDFSigner|signer|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|IVF PDF Signer"
    "100|TimeStampSigner|tsa|${TSA_CERT_PROFILE}|${TSA_EE_PROFILE}|IVF Timestamp Authority"
    "272|PDFSigner_technical|pdfsigner_technical|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|Ky Thuat Vien IVF"
    "444|PDFSigner_head_department|pdfsigner_head_department|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|Truong Khoa IVF"
    "597|PDFSigner_doctor1|pdfsigner_doctor1|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|Bac Si IVF"
    "907|PDFSigner_admin|pdfsigner_admin|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|Quan Tri IVF"
)

# Internal state tracking
MIGRATED_WORKERS=()
FAILED_WORKERS=()
BACKUP_DIR="/tmp/rekey-backup-$(date +%Y%m%d-%H%M%S)"

# ── Argument parser ───────────────────────────────────────────
usage() {
    cat <<EOF
Usage: $0 [OPTIONS]

Options:
  --dry-run                  Preview all steps without making changes
  --worker ID                Migrate only this worker ID (e.g. 1, 100, 272)
  --hsm-lib PATH             PKCS#11 library path in container (default: SoftHSM2 system path)
  --hsm-name NAME            SHAREDLIBRARYNAME in SignServer (must match deploy.properties)
  --new-token-label LABEL    SoftHSM2 token label to use (default: SignServerToken)
  --hsm-slot SLOT            Slot number (for hardware HSMs; leave empty for label-based)
  --hsm-pin-file PATH        Path to new HSM PIN file inside container (default: /run/secrets/softhsm_pin)
  --so-pin-file PATH         Path to new HSM SO PIN file inside container (for SoftHSM2 re-init)
  --issuing-ca NAME          EJBCA issuing CA name (default: IVF-Signing-SubCA)
  --skip-backup              Skip backing up current worker configs (not recommended)
  --no-rollback              Don't rollback failed workers (leave in partial state)
  --force                    Skip confirmation prompts

Examples:
  # Migrate all workers to fresh SoftHSM2 token:
  $0 --new-token-label SignServerToken-v2

  # Migrate to Luna Network HSM:
  $0 --hsm-lib /usr/safenet/lunaclient/lib/libCryptoki2_64.so \\
     --hsm-name LunaHSM --hsm-slot 0 \\
     --hsm-pin-file /run/secrets/luna_pin

  # Single worker only (rolling migration):
  $0 --worker 1 --dry-run
  $0 --worker 1

EOF
    exit 0
}

parse_args() {
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --dry-run)           DRY_RUN=true; shift ;;
            --worker)            TARGET_WORKER="$2"; shift 2 ;;
            --hsm-lib)           NEW_HSM_LIB="$2"; shift 2 ;;
            --hsm-name)          NEW_HSM_NAME="$2"; shift 2 ;;
            --new-token-label)   NEW_TOKEN_LABEL="$2"; shift 2 ;;
            --hsm-slot)          NEW_HSM_SLOT="$2"; shift 2 ;;
            --hsm-pin-file)      NEW_HSM_PIN_FILE="$2"; shift 2 ;;
            --so-pin-file)       NEW_SO_PIN_FILE="$2"; shift 2 ;;
            --issuing-ca)        ISSUING_CA="$2"; shift 2 ;;
            --skip-backup)       SKIP_BACKUP=true; shift ;;
            --no-rollback)       ROLLBACK_ON_FAILURE=false; shift ;;
            --force)             FORCE=true; shift ;;
            --help|-h)           usage ;;
            *) err "Unknown option: $1"; usage ;;
        esac
    done
}

# ── Dry-run wrapper ────────────────────────────────────────────
# Prints command instead of running it in dry-run mode
run() {
    if $DRY_RUN; then
        echo -e "  ${YELLOW}[DRY-RUN]${NC} $*"
    else
        "$@"
    fi
}

# ── Container discovery ────────────────────────────────────────
discover_containers() {
    EJBCA_CONT=$(docker ps --filter name=ivf_ejbca --format "{{.Names}}" | grep -v '\-db' | head -1 || true)
    SS_CONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1 || true)

    if [[ -z "$EJBCA_CONT" || -z "$SS_CONT" ]]; then
        err "Could not locate required containers. Running containers:"
        docker ps --format "  {{.Names}}" | grep ivf || true
        exit 1
    fi

    ok "EJBCA container:      $EJBCA_CONT"
    ok "SignServer container: $SS_CONT"
}

# ── CLI aliases ────────────────────────────────────────────────
ejbca()   { docker exec            "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh "$@"; }
ss()      { docker exec            "$SS_CONT"    /opt/signserver/bin/signserver "$@"; }
ss_root() { docker exec --user root "$SS_CONT"  bash -c "$1"; }
ss_exec() { docker exec            "$SS_CONT"    bash -c "$1"; }

# ── Resolve PIN for new HSM ───────────────────────────────────
resolve_hsm_pin() {
    if [[ -n "$NEW_HSM_PIN_FILE" ]]; then
        HSM_PIN=$(ss_exec "cat '${NEW_HSM_PIN_FILE}' 2>/dev/null" || true)
        if [[ -z "$HSM_PIN" ]]; then
            err "Could not read PIN from ${NEW_HSM_PIN_FILE} inside container"
            exit 1
        fi
    else
        # Default: same Docker secret as current softhsm_pin
        HSM_PIN=$(ss_exec "cat /run/secrets/softhsm_pin 2>/dev/null" || true)
        if [[ -z "$HSM_PIN" ]]; then
            warn "Could not read /run/secrets/softhsm_pin — using fallback PIN"
            HSM_PIN="3ac33a807af6b22fe9f22e4ba2c56a3b"
        fi
    fi

    if [[ -n "$NEW_SO_PIN_FILE" ]]; then
        HSM_SO_PIN=$(ss_exec "cat '${NEW_SO_PIN_FILE}' 2>/dev/null" || echo "$HSM_PIN")
    else
        HSM_SO_PIN=$(ss_exec "cat /run/secrets/softhsm_so_pin 2>/dev/null" || echo "$HSM_PIN")
    fi
}

# ════════════════════════════════════════════════════════════
# PHASE 1: Pre-flight Checks
# ════════════════════════════════════════════════════════════
phase_preflight() {
    step "Phase 1: Pre-flight Checks"

    # ── Health: EJBCA ──
    substep "Checking EJBCA health..."
    if docker exec "$EJBCA_CONT" curl -sk \
        "http://localhost:8080/ejbca/publicweb/healthcheck/ejbcahealth" \
        | grep -q "ALLOK"; then
        ok "EJBCA healthy"
    else
        err "EJBCA health check failed. Cannot proceed."
        exit 1
    fi

    # ── Health: SignServer ──
    substep "Checking SignServer health..."
    if docker exec "$SS_CONT" curl -sk \
        "http://localhost:8080/signserver/healthcheck/signserverhealth" \
        | grep -q "ALLOK"; then
        ok "SignServer healthy"
    else
        err "SignServer health check failed. Cannot proceed."
        exit 1
    fi

    # ── Check EJBCA issuing CA ──
    substep "Verifying issuing CA: ${ISSUING_CA}..."
    if ejbca ca listcas 2>&1 | grep -q "CA Name: ${ISSUING_CA}"; then
        ok "CA '${ISSUING_CA}' found in EJBCA"
    else
        err "CA '${ISSUING_CA}' not found. Available CAs:"
        ejbca ca listcas 2>&1 | grep "CA Name" || true
        exit 1
    fi

    # ── Check new PKCS#11 library accessible in container ──
    substep "Verifying PKCS#11 library: ${NEW_HSM_LIB}..."
    if ss_exec "test -f '${NEW_HSM_LIB}'" 2>/dev/null; then
        ok "PKCS#11 library found: ${NEW_HSM_LIB}"
    else
        err "PKCS#11 library NOT found at ${NEW_HSM_LIB} inside SignServer container."
        if [[ "$NEW_HSM_LIB" == *"libCryptoki"* ]] || [[ "$NEW_HSM_LIB" == *"luna"* ]]; then
            err "For Luna HSM: bind-mount the client library into the SignServer container."
            err "Add to docker-compose: volumes: - /usr/safenet/lunaclient/lib:/<same-path>:ro"
        fi
        exit 1
    fi

    # ── Check deploy.properties has new HSM registered ──
    substep "Checking SignServer deploy.properties for SHAREDLIBRARYNAME='${NEW_HSM_NAME}'..."
    local dp_check
    dp_check=$(ss_exec "find /opt/keyfactor /opt/signserver -name signserver_deploy.properties 2>/dev/null | head -1" || true)
    if [[ -n "$dp_check" ]]; then
        if ss_exec "grep -q '${NEW_HSM_NAME}' '${dp_check}'" 2>/dev/null; then
            ok "HSM '${NEW_HSM_NAME}' registered in deploy.properties"
        else
            err "SHAREDLIBRARYNAME '${NEW_HSM_NAME}' not found in deploy.properties."
            err "Current registered libraries:"
            ss_exec "grep 'cryptotoken.p11.lib' '${dp_check}'" 2>/dev/null || true
            err "Add an entry like: cryptotoken.p11.lib.2.name = ${NEW_HSM_NAME}"
            err "               cryptotoken.p11.lib.2.file = ${NEW_HSM_LIB}"
            err "Then restart SignServer and re-run this script."
            exit 1
        fi
    else
        warn "Could not locate signserver_deploy.properties — skipping library check"
    fi

    # ── Resolve HSM PIN ──
    substep "Resolving HSM PIN..."
    resolve_hsm_pin
    ok "HSM PIN resolved (${#HSM_PIN} chars)"

    # ── Confirmation ──
    if ! $DRY_RUN && ! $FORCE; then
        echo
        warn "=========================================================="
        warn "  HSM RE-KEY MIGRATION — DESTRUCTIVE OPERATION"
        warn "=========================================================="
        warn "  This will:"
        warn "    1. Generate NEW private keys in: ${NEW_HSM_NAME}"
        warn "    2. Re-issue NEW certificates from EJBCA"
        warn "    3. REPLACE all worker signing keys"
        warn ""
        warn "  OLD keys in SoftHSM2 (softhsm-tokens/) will be ABANDONED."
        warn "  Old certificates will remain valid until expiry."
        warn ""
        if [[ -n "$TARGET_WORKER" ]]; then
            warn "  Target: WORKER ${TARGET_WORKER} only"
        else
            warn "  Target: ALL 6 workers"
        fi
        warn "=========================================================="
        echo -n "  Type 'yes' to continue: "
        read -r CONFIRM
        if [[ "$CONFIRM" != "yes" ]]; then
            info "Aborted."
            exit 0
        fi
    fi

    ok "Pre-flight complete"
}

# ════════════════════════════════════════════════════════════
# PHASE 2: Backup Current Worker Configs
# ════════════════════════════════════════════════════════════
phase_backup() {
    step "Phase 2: Backup Current Worker Configs"

    if $SKIP_BACKUP; then
        warn "Backup skipped (--skip-backup)"
        return
    fi

    run mkdir -p "$BACKUP_DIR"
    info "Backup directory: $BACKUP_DIR"

    for worker_def in "${ALL_WORKERS[@]}"; do
        IFS='|' read -r WID WNAME _ _ _ _ <<< "$worker_def"

        if [[ -n "$TARGET_WORKER" && "$WID" != "$TARGET_WORKER" ]]; then
            continue
        fi

        substep "Backing up Worker ${WID} (${WNAME})..."

        # Export current worker properties
        if $DRY_RUN; then
            info "  Would export: signserver getproperties ${WID}"
        else
            {
                echo "# Worker ${WID} (${WNAME}) — backup before HSM re-key migration"
                echo "# $(date -u +%Y-%m-%dT%H:%M:%SZ)"
                echo "# ────────────────────────────────"
                docker exec "$SS_CONT" /opt/signserver/bin/signserver getproperties \
                    -- "$WID" 2>/dev/null || echo "# (could not read properties)"
            } > "${BACKUP_DIR}/worker_${WID}_${WNAME}.properties" 2>/dev/null || true

            # Export current signer certificate
            docker exec "$SS_CONT" /opt/signserver/bin/signserver getstatus \
                brief "$WID" 2>/dev/null \
                >> "${BACKUP_DIR}/worker_${WID}_${WNAME}.status" || true

            ok "  Worker ${WID} config saved to ${BACKUP_DIR}/"
        fi
    done

    ok "Backup complete: ${BACKUP_DIR}"
}

# ════════════════════════════════════════════════════════════
# PHASE 3: Initialize New HSM Token
# ════════════════════════════════════════════════════════════
phase_init_token() {
    step "Phase 3: Initialize New HSM Token"

    # For hardware HSMs (Luna, Utimaco): token is initialized externally via
    # the HSM management console. This phase only applies to SoftHSM2.
    if [[ "$NEW_HSM_LIB" != *"softhsm"* ]]; then
        info "Hardware HSM detected — skipping software token initialization."
        info "Ensure token label '${NEW_TOKEN_LABEL}' is provisioned on the HSM."
        return
    fi

    substep "Checking if SoftHSM2 token '${NEW_TOKEN_LABEL}' already exists..."

    local token_exists=false
    if ss_exec "SOFTHSM2_CONF=\${SOFTHSM2_CONF:-/opt/keyfactor/persistent/softhsm/softhsm2.conf} \
        softhsm2-util --show-slots 2>/dev/null | grep -q 'Label.*${NEW_TOKEN_LABEL}'" 2>/dev/null; then
        token_exists=true
    fi

    if $token_exists; then
        if [[ "$TARGET_WORKER" == "" ]]; then
            # Full migration: we want a clean token. Ask before reinitializing.
            warn "Token '${NEW_TOKEN_LABEL}' already exists."
            warn "For a clean re-key migration, consider using --new-token-label SignServerToken-v2"
            if ! $FORCE && ! $DRY_RUN; then
                echo -n "  Re-use existing token? (yes/no): "
                read -r TOKEN_REUSE
                if [[ "$TOKEN_REUSE" != "yes" ]]; then
                    info "Use --new-token-label <label> to create a fresh token."
                    exit 0
                fi
            fi
        fi
        ok "Using existing token '${NEW_TOKEN_LABEL}'"
    else
        substep "Initializing new SoftHSM2 token: '${NEW_TOKEN_LABEL}'..."
        if $DRY_RUN; then
            info "Would run: softhsm2-util --init-token --free --label ${NEW_TOKEN_LABEL}"
        else
            ss_exec "SOFTHSM2_CONF=\${SOFTHSM2_CONF:-/opt/keyfactor/persistent/softhsm/softhsm2.conf} \
                softhsm2-util --init-token --free \
                    --label '${NEW_TOKEN_LABEL}' \
                    --pin '${HSM_PIN}' \
                    --so-pin '${HSM_SO_PIN}'"
            ok "Token '${NEW_TOKEN_LABEL}' initialized"
        fi
    fi
}

# ════════════════════════════════════════════════════════════
# PHASE 4: Per-Worker Re-Key
# ════════════════════════════════════════════════════════════

# Rollback: restore old P12-based config from backup
rollback_worker() {
    local WID="$1"
    local WNAME="$2"
    local BACKUP_FILE="${BACKUP_DIR}/worker_${WID}_${WNAME}.properties"

    warn "  Rolling back Worker ${WID} (${WNAME})..."

    if [[ ! -f "$BACKUP_FILE" ]]; then
        warn "  No backup found at ${BACKUP_FILE} — cannot rollback automatically"
        warn "  Worker ${WID} may be left in a broken state"
        return
    fi

    # Restore properties from backup
    docker cp "$BACKUP_FILE" "${SS_CONT}:/tmp/rollback_${WID}.properties" 2>/dev/null || true
    docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperties \
        -- "/tmp/rollback_${WID}.properties" 2>/dev/null || true
    docker exec "$SS_CONT" /opt/signserver/bin/signserver reload -- "$WID" 2>/dev/null || true
    warn "  Worker ${WID} rollback attempted"
}

migrate_worker() {
    local worker_def="$1"
    IFS='|' read -r WID WNAME KEY_ALIAS CERT_PROFILE EE_PROFILE CERT_CN <<< "$worker_def"

    banner "  Worker ${WID}: ${WNAME} (CN=${CERT_CN})"

    local EE_USERNAME="ivf-signer-${WID}"
    local CSR_FILE="/tmp/rekey_${WID}_csr.pem"
    local CERT_FILE="/tmp/rekey_${WID}_cert.pem"
    local CHAIN_FILE="/tmp/rekey_${WID}_chain.pem"

    # ── Step 4.1: Set PKCS#11 crypto token properties ──
    substep "[${WID}] Configuring PKCS#11 token properties..."
    if $DRY_RUN; then
        info "  Would set: CRYPTOTOKEN_IMPLEMENTATION_CLASS = PKCS11CryptoToken"
        info "  Would set: SHAREDLIBRARYNAME = ${NEW_HSM_NAME}"
        info "  Would set: SLOTLABELVALUE = ${NEW_TOKEN_LABEL}"
        info "  Would set: DEFAULTKEY = ${KEY_ALIAS}"
    else
        # Remove old crypto token properties that conflict
        for prop in KEYSTOREPATH KEYSTOREPASSWORD KEYSTORETYPE SHAREDLIBRARY SET_PERMISSIONS; do
            docker exec "$SS_CONT" /opt/signserver/bin/signserver removeproperty \
                -- "$WID" "$prop" 2>/dev/null || true
        done

        # Set new PKCS#11 properties
        docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperty \
            -- "$WID" CRYPTOTOKEN_IMPLEMENTATION_CLASS \
               "org.signserver.server.cryptotokens.PKCS11CryptoToken"
        docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperty \
            -- "$WID" SHAREDLIBRARYNAME "$NEW_HSM_NAME"
        docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperty \
            -- "$WID" SLOTLABELTYPE "SLOT_LABEL"
        docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperty \
            -- "$WID" SLOTLABELVALUE "$NEW_TOKEN_LABEL"
        docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperty \
            -- "$WID" PIN "$HSM_PIN"
        docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperty \
            -- "$WID" DEFAULTKEY "$KEY_ALIAS"

        # If hardware HSM uses slot number instead of label
        if [[ -n "$NEW_HSM_SLOT" ]]; then
            docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperty \
                -- "$WID" SLOTLABELTYPE "SLOT_NUMBER"
            docker exec "$SS_CONT" /opt/signserver/bin/signserver setproperty \
                -- "$WID" SLOTLABELVALUE "$NEW_HSM_SLOT"
        fi

        ok "  [${WID}] PKCS#11 properties set"
    fi

    # ── Step 4.2: Reload + activate crypto token ──
    substep "[${WID}] Reloading and activating crypto token..."
    if $DRY_RUN; then
        info "  Would run: reload ${WID} && activatecryptotoken ${WID} <PIN>"
    else
        docker exec "$SS_CONT" /opt/signserver/bin/signserver reload -- "$WID"
        docker exec "$SS_CONT" /opt/signserver/bin/signserver activatecryptotoken \
            -- "$WID" "$HSM_PIN" 2>&1 | tail -3
        ok "  [${WID}] Crypto token activated"
    fi

    # ── Step 4.3: Generate new RSA-4096 key on HSM ──
    substep "[${WID}] Generating RSA-4096 key '${KEY_ALIAS}' on ${NEW_HSM_NAME}..."
    if $DRY_RUN; then
        info "  Would run: generatekey ${WID} -keyalg RSA -keyspec 4096 -alias ${KEY_ALIAS}"
    else
        docker exec "$SS_CONT" /opt/signserver/bin/signserver generatekey \
            -- "$WID" RSA 4096 "$KEY_ALIAS" 2>&1 | tail -5
        ok "  [${WID}] RSA-4096 key '${KEY_ALIAS}' generated (CKA_EXTRACTABLE=false)"
    fi

    # ── Step 4.4: Reload + re-activate after key generation ──
    if ! $DRY_RUN; then
        docker exec "$SS_CONT" /opt/signserver/bin/signserver reload -- "$WID"
        docker exec "$SS_CONT" /opt/signserver/bin/signserver activatecryptotoken \
            -- "$WID" "$HSM_PIN" 2>&1 | tail -2
    fi

    # ── Step 4.5: Generate CSR ──
    local CERT_DN="CN=${CERT_CN},O=IVF Healthcare,OU=Digital Signing,C=VN"
    substep "[${WID}] Generating CSR: '${CERT_DN}'..."
    if $DRY_RUN; then
        info "  Would run: generatecertreq ${WID} '${CERT_DN}' SHA256WithRSA ${CSR_FILE}"
    else
        docker exec "$SS_CONT" /opt/signserver/bin/signserver generatecertreq \
            -- "$WID" "$CERT_DN" SHA256WithRSA "$CSR_FILE" 2>&1 | tail -3

        # Verify CSR was created
        if ! docker exec "$SS_CONT" test -s "$CSR_FILE" 2>/dev/null; then
            err "  [${WID}] CSR not generated at ${CSR_FILE}"
            return 1
        fi
        ok "  [${WID}] CSR generated: ${CSR_FILE}"
    fi

    # ── Step 4.6: Transfer CSR from SignServer → EJBCA ──
    substep "[${WID}] Transferring CSR to EJBCA..."
    if ! $DRY_RUN; then
        local CSR_DATA
        CSR_DATA=$(docker exec "$SS_CONT" cat "$CSR_FILE")
        echo "$CSR_DATA" | docker exec -i "$EJBCA_CONT" bash -c "cat > ${CSR_FILE}"
        ok "  [${WID}] CSR available in EJBCA container"
    fi

    # ── Step 4.7: Reset EJBCA end entity ──
    substep "[${WID}] Resetting EJBCA end entity '${EE_USERNAME}'..."
    if $DRY_RUN; then
        info "  Would run: ra setendentitystatus ${EE_USERNAME} 10 + setclearpwd"
    else
        # Ensure end entity exists; create if missing
        if ! ejbca ra findendentity -- "--username" "$EE_USERNAME" 2>&1 | grep -q "$EE_USERNAME"; then
            info "  [${WID}] End entity not found — creating..."
            ejbca ra addendentity \
                --username "$EE_USERNAME" \
                --dn "\"${CERT_DN}\"" \
                --caname "$ISSUING_CA" \
                --type 1 \
                --token P12 \
                --certprofile "$CERT_PROFILE" \
                --eeprofile "$EE_PROFILE"
        fi
        ejbca ra setendentitystatus -- "--username" "$EE_USERNAME" --status 10
        ejbca ra setclearpwd -- "--username" "$EE_USERNAME" --password "$KEYSTORE_PASSWORD"
        ok "  [${WID}] End entity reset for re-signing"
    fi

    # ── Step 4.8: Sign CSR with EJBCA ──
    substep "[${WID}] Signing certificate with EJBCA..."
    if $DRY_RUN; then
        info "  Would run: createcert --username ${EE_USERNAME} -c ${CSR_FILE}"
    else
        ejbca ra createcert \
            --username "$EE_USERNAME" \
            --password "$KEYSTORE_PASSWORD" \
            -c "$CSR_FILE" \
            -f "$CERT_FILE" 2>&1 | tail -3

        if ! docker exec "$EJBCA_CONT" test -s "$CERT_FILE" 2>/dev/null; then
            err "  [${WID}] Signed certificate not created at ${CERT_FILE}"
            return 1
        fi
        ok "  [${WID}] Certificate signed by ${ISSUING_CA}"
    fi

    # ── Step 4.9: Extract PEM cert + transfer to SignServer ──
    if ! $DRY_RUN; then
        local CERT_PEM
        CERT_PEM=$(docker exec "$EJBCA_CONT" \
            sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' "$CERT_FILE")
        echo "$CERT_PEM" | docker exec -i "$SS_CONT" bash -c "cat > ${CERT_FILE}"
        ok "  [${WID}] Certificate transferred to SignServer"
    fi

    # ── Step 4.10: Upload signer certificate ──
    substep "[${WID}] Uploading signer certificate..."
    if $DRY_RUN; then
        info "  Would run: uploadsignercertificate ${WID} GLOB ${CERT_FILE}"
    else
        docker exec "$SS_CONT" /opt/signserver/bin/signserver uploadsignercertificate \
            -- "$WID" GLOB "$CERT_FILE" 2>&1 | tail -3
        ok "  [${WID}] Signer certificate uploaded"
    fi

    # ── Step 4.11: Build and upload full certificate chain ──
    substep "[${WID}] Building and uploading certificate chain..."
    if $DRY_RUN; then
        info "  Would assemble: signed_cert + sub-ca + root-ca → chain"
    else
        # Get Sub-CA cert from EJBCA
        local SUB_CA_PEM ROOT_CA_PEM
        SUB_CA_PEM=$(docker exec "$EJBCA_CONT" \
            /opt/keyfactor/bin/ejbca.sh ca getcacert \
            --caname "$ISSUING_CA" 2>/dev/null | \
            sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p')
        ROOT_CA_PEM=$(docker exec "$EJBCA_CONT" \
            /opt/keyfactor/bin/ejbca.sh ca getcacert \
            --caname "IVF-Root-CA" 2>/dev/null | \
            sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p')

        # Assemble chain: leaf → sub-CA → root CA
        {
            docker exec "$SS_CONT" cat "$CERT_FILE"
            echo "$SUB_CA_PEM"
            echo "$ROOT_CA_PEM"
        } | docker exec -i "$SS_CONT" bash -c "cat > ${CHAIN_FILE}"

        docker exec "$SS_CONT" /opt/signserver/bin/signserver uploadsignercertificatechain \
            -- "$WID" GLOB "$CHAIN_FILE" 2>&1 | tail -3
        ok "  [${WID}] Certificate chain uploaded (leaf → SubCA → RootCA)"
    fi

    # ── Step 4.12: Final reload + verify ACTIVE ──
    substep "[${WID}] Final reload and status check..."
    if $DRY_RUN; then
        info "  Would run: reload ${WID} && getstatus brief ${WID}"
    else
        docker exec "$SS_CONT" /opt/signserver/bin/signserver reload -- "$WID"

        local STATUS
        STATUS=$(docker exec "$SS_CONT" /opt/signserver/bin/signserver getstatus \
            brief "$WID" 2>&1)
        echo "$STATUS" | grep -E "Status|Worker" || true

        if echo "$STATUS" | grep -qi "Active"; then
            ok "  [${WID}] Worker ${WID} (${WNAME}): ✅ ACTIVE"
            MIGRATED_WORKERS+=("${WID}:${WNAME}")
        else
            err "  [${WID}] Worker ${WID} is NOT Active after migration"
            echo "$STATUS"
            return 1
        fi
    fi

    # ── Cleanup temp files ──
    if ! $DRY_RUN; then
        docker exec "$SS_CONT" rm -f "$CSR_FILE" "$CERT_FILE" "$CHAIN_FILE" 2>/dev/null || true
        docker exec "$EJBCA_CONT" rm -f "$CSR_FILE" "$CERT_FILE" 2>/dev/null || true
    fi
}

phase_migrate_workers() {
    step "Phase 4: Per-Worker Re-Key Migration"

    local WORKERS_TO_MIGRATE=()

    if [[ -n "$TARGET_WORKER" ]]; then
        # Single worker mode
        for worker_def in "${ALL_WORKERS[@]}"; do
            IFS='|' read -r WID _ _ _ _ _ <<< "$worker_def"
            if [[ "$WID" == "$TARGET_WORKER" ]]; then
                WORKERS_TO_MIGRATE+=("$worker_def")
                break
            fi
        done
        if [[ ${#WORKERS_TO_MIGRATE[@]} -eq 0 ]]; then
            err "Worker ID '${TARGET_WORKER}' not found. Valid IDs: 1, 100, 272, 444, 597, 907"
            exit 1
        fi
    else
        WORKERS_TO_MIGRATE=("${ALL_WORKERS[@]}")
    fi

    info "Migrating ${#WORKERS_TO_MIGRATE[@]} worker(s)..."

    for worker_def in "${WORKERS_TO_MIGRATE[@]}"; do
        IFS='|' read -r WID WNAME _ _ _ _ <<< "$worker_def"

        if migrate_worker "$worker_def"; then
            : # ok — already tracked in MIGRATED_WORKERS inside migrate_worker
        else
            err "Worker ${WID} (${WNAME}) migration FAILED"
            FAILED_WORKERS+=("${WID}:${WNAME}")

            if $ROLLBACK_ON_FAILURE && ! $DRY_RUN; then
                rollback_worker "$WID" "$WNAME"
            fi
        fi
    done
}

# ════════════════════════════════════════════════════════════
# PHASE 5: Verification & Summary
# ════════════════════════════════════════════════════════════
phase_verify() {
    step "Phase 5: Final Verification"

    if $DRY_RUN; then
        info "DRY RUN — no actual changes were made"
        return
    fi

    substep "Verifying all migrated worker statuses..."
    local all_ok=true

    for worker_def in "${ALL_WORKERS[@]}"; do
        IFS='|' read -r WID WNAME _ _ _ _ <<< "$worker_def"

        if [[ -n "$TARGET_WORKER" && "$WID" != "$TARGET_WORKER" ]]; then
            continue
        fi

        local STATUS
        STATUS=$(docker exec "$SS_CONT" /opt/signserver/bin/signserver getstatus \
            brief "$WID" 2>&1 || true)

        if echo "$STATUS" | grep -qi "Active"; then
            ok "  Worker ${WID} (${WNAME}): ✅ ACTIVE"
        else
            err "  Worker ${WID} (${WNAME}): ❌ NOT ACTIVE"
            echo "$STATUS" | grep -E "Status|Error|Offline" || echo "$STATUS" | tail -5
            all_ok=false
        fi
    done

    # ── Smoke test: sign a minimal PDF ──
    substep "Smoke test: signing a minimal PDF with Worker 1 (PDFSigner)..."
    local test_pdf="/tmp/rekey_smoketest.pdf"
    local signed_pdf="/tmp/rekey_smoketest_signed.pdf"

    # Create minimal valid PDF for smoke test
    docker exec "$SS_CONT" bash -c "printf '%%PDF-1.4\n1 0 obj\n<</Type /Catalog>>\nendobj\ntrailer\n<</Root 1 0 R>>' > ${test_pdf}" 2>/dev/null || true

    if docker exec "$SS_CONT" bash -c "
        curl -sf -o '${signed_pdf}' \
            -F 'workerName=PDFSigner' \
            -F 'data=@${test_pdf};type=application/pdf' \
            http://localhost:8080/signserver/process" 2>/dev/null; then
        ok "  Smoke test passed — PDFSigner signing functional"
    else
        warn "  Smoke test inconclusive (curl failed — may be port/network)"
        warn "  Check via: docker exec ${SS_CONT} curl -sf ... /signserver/process"
    fi
    docker exec "$SS_CONT" rm -f "$test_pdf" "$signed_pdf" 2>/dev/null || true

    echo
    echo -e "${MAGENTA}════════════════════════════════════════════════${NC}"
    echo -e "${BOLD}  HSM RE-KEY MIGRATION SUMMARY${NC}"
    echo -e "${MAGENTA}════════════════════════════════════════════════${NC}"
    echo -e "  New HSM lib:   ${NEW_HSM_LIB}"
    echo -e "  HSM name:      ${NEW_HSM_NAME}"
    echo -e "  Token label:   ${NEW_TOKEN_LABEL}"
    echo -e "  Issuing CA:    ${ISSUING_CA}"
    echo

    if [[ ${#MIGRATED_WORKERS[@]} -gt 0 ]]; then
        echo -e "  ${GREEN}Migrated workers:${NC}"
        for w in "${MIGRATED_WORKERS[@]}"; do
            IFS=':' read -r WID WNAME <<< "$w"
            echo -e "    ✅  Worker ${WID}: ${WNAME}"
        done
    fi

    if [[ ${#FAILED_WORKERS[@]} -gt 0 ]]; then
        echo
        echo -e "  ${RED}Failed workers:${NC}"
        for w in "${FAILED_WORKERS[@]}"; do
            IFS=':' read -r WID WNAME <<< "$w"
            echo -e "    ❌  Worker ${WID}: ${WNAME}"
        done
        echo
        err "Some workers failed migration. Check logs and failed worker status."
        err "Backup configs available in: ${BACKUP_DIR}"
        $all_ok || exit 1
    fi

    if $all_ok; then
        echo
        ok "All migrated workers are ACTIVE on ${NEW_HSM_NAME}"
        echo
        info "Next steps:"
        echo -e "  1. Update wsadmins access control:    signserver wsadmins -allowany false"
        echo -e "  2. Monitor workers:                   signserver getstatus brief all"
        echo -e "  3. Test PDF signing via API:           POST /api/signing/sign-pdf"
        echo -e "  4. Verify new certs in EJBCA admin:   https://10.200.0.1:8443/ejbca/adminweb/"
        if [[ -n "$BACKUP_DIR" ]] && [[ -d "$BACKUP_DIR" ]]; then
            echo -e "  5. Old config backup retained at:     ${BACKUP_DIR}"
            echo -e "     Remove when confident:              rm -rf ${BACKUP_DIR}"
        fi
    fi
}

# ════════════════════════════════════════════════════════════
# MAIN
# ════════════════════════════════════════════════════════════
main() {
    parse_args "$@"

    echo -e "${BOLD}${MAGENTA}"
    echo "  ╔══════════════════════════════════════════════════╗"
    echo "  ║    IVF PKI — HSM Re-Key Migration Tool          ║"
    echo "  ║    $(date -u +'%Y-%m-%d %H:%M:%S UTC')                       ║"
    echo "  ╚══════════════════════════════════════════════════╝"
    echo -e "${NC}"

    if $DRY_RUN; then
        warn "DRY-RUN MODE — no changes will be made to any container or HSM"
        echo
    fi

    discover_containers
    phase_preflight
    phase_backup
    phase_init_token
    phase_migrate_workers
    phase_verify

    echo
    ok "Done."
}

main "$@"
