#!/bin/bash
# ============================================================
# IVF PKI – Phases 3, 4, 5: SoftHSM2 + PKCS#11 Worker Setup
# ============================================================
# Run ON the VPS (not locally):
#   scp scripts/setup-pkcs11-workers.sh root@10.200.0.1:/tmp/
#   ssh root@10.200.0.1 "sed -i 's/\r//' /tmp/setup-pkcs11-workers.sh && bash /tmp/setup-pkcs11-workers.sh 2>&1 | tee /tmp/pkcs11-setup.log"
#
# Phases:
#   Phase 3 – Copy SoftHSM2 to persistent volume + install environment-hsm hook
#             + restart SignServer so WildFly loads the P11 library at startup
#   Phase 4 – Create/reset end entities in EJBCA (using IVF profiles)
#   Phase 5 – Configure each worker for PKCS#11, generate RSA-4096 keys,
#             sign CSR with EJBCA, upload cert+chain to SignServer
# ============================================================
set -euo pipefail

# ── Colors ──────────────────────────────────────────────────
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'
BLUE='\033[0;34m'; CYAN='\033[0;36m'; MAGENTA='\033[0;35m'; NC='\033[0m'
ok()     { echo -e "${GREEN}[OK]${NC} $*"; }
info()   { echo -e "${BLUE}[INFO]${NC} $*"; }
warn()   { echo -e "${YELLOW}[WARN]${NC} $*"; }
err()    { echo -e "${RED}[ERROR]${NC} $*"; }
step()   { echo -e "\n${MAGENTA}════════════════════════════════════════════${NC}"; \
           echo -e "${MAGENTA}  $*${NC}"; \
           echo -e "${MAGENTA}════════════════════════════════════════════${NC}"; }
substep(){ echo -e "${CYAN}  ── $*${NC}"; }

# ── Discover containers ──────────────────────────────────────
find_ss() { docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1; }

EJBCA_CONT=$(docker ps --filter name=ivf_ejbca --format "{{.Names}}" | grep -v '\-db' | head -1)
SS_CONT=$(find_ss)

if [ -z "$EJBCA_CONT" ] || [ -z "$SS_CONT" ]; then
    err "Could not locate containers. Running:"
    docker ps --format "  {{.Names}}" | grep ivf || true
    exit 1
fi
ok "EJBCA container:      $EJBCA_CONT"
ok "SignServer container: $SS_CONT"

# ── Configuration ────────────────────────────────────────────
EJBCA_CLI="/opt/keyfactor/bin/ejbca.sh"
SS_CLI="/opt/signserver/bin/signserver"

TOKEN_LABEL="SignServerToken"
SOFTHSM_PERSISTENT_DIR="/opt/keyfactor/persistent/softhsm"
SOFTHSM_TOKEN_DIR="${SOFTHSM_PERSISTENT_DIR}/tokens"
SOFTHSM_LIB_PERSISTENT="${SOFTHSM_PERSISTENT_DIR}/lib/libsofthsm2.so"
SOFTHSM_UTIL_PERSISTENT="${SOFTHSM_PERSISTENT_DIR}/bin/softhsm2-util"
SOFTHSM_CONF_PERSISTENT="${SOFTHSM_PERSISTENT_DIR}/softhsm2.conf"

KEY_DIR="/opt/keyfactor/persistent/keys"
ISSUING_CA="IVF-Signing-SubCA"
KEYSTORE_PASSWORD="changeit"

PDF_CERT_PROFILE="IVF-PDFSigner-Profile"
PDF_EE_PROFILE="IVF-PDFSigner-EEProfile"
TSA_CERT_PROFILE="IVF-TSA-Profile"
TSA_EE_PROFILE="IVF-TSA-EEProfile"

# Format: "ID|Name|KeyAlias|CertProfile|EEProfile|CN"
PDF_WORKERS=(
    "1|PDFSigner|signer|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|IVF PDF Signer"
    "272|PDFSigner_technical|pdfsigner_technical|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|Ky Thuat Vien IVF"
    "444|PDFSigner_head_department|pdfsigner_head_department|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|Truong Khoa IVF"
    "597|PDFSigner_doctor1|pdfsigner_doctor1|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|Bac Si IVF"
    "907|PDFSigner_admin|pdfsigner_admin|${PDF_CERT_PROFILE}|${PDF_EE_PROFILE}|Quan Tri IVF"
)
TSA_WORKER="100|TimeStampSigner|tsa|${TSA_CERT_PROFILE}|${TSA_EE_PROFILE}|IVF Timestamp Authority"

# ── Shorthand helpers ─────────────────────────────────────────
ejbca()   { docker exec           "$EJBCA_CONT" "$EJBCA_CLI" "$@"; }
ss()      { docker exec           "$SS_CONT"    "$SS_CLI"    "$@"; }
ss_root() { docker exec --user root "$SS_CONT"  bash -c "$1"; }


# ════════════════════════════════════════════════════════════
# PHASE 3: SoftHSM2 Persistent Setup + SignServer Restart
# ════════════════════════════════════════════════════════════
step "Phase 3: SoftHSM2 Persistent Setup + environment-hsm Hook"

# ── Ensure AlmaLinux-native SoftHSM2 binaries exist on VPS host ───
if [ ! -f /tmp/libsofthsm2.so ] || [ ! -f /tmp/softhsm2-util ]; then
    info "Pulling AlmaLinux 9 native SoftHSM2 v2.6.1 via temp container..."
    docker rm -f softhsm-src 2>/dev/null || true
    docker run -d --name softhsm-src almalinux:9 \
        sh -c "dnf install -y softhsm 2>&1 | tail -2 && sleep 30" 2>/dev/null
    sleep 20
    docker cp softhsm-src:/usr/lib64/pkcs11/libsofthsm2.so /tmp/libsofthsm2.so
    docker cp softhsm-src:/usr/bin/softhsm2-util            /tmp/softhsm2-util
    docker rm -f softhsm-src 2>/dev/null || true
    ok "AlmaLinux 9 SoftHSM2 binaries downloaded to /tmp/"
else
    ok "SoftHSM2 binaries already at /tmp/ (skipping download)"
fi

# ── Create persistent directory structure on SignServer volume ──
info "Creating persistent directories..."
docker exec --user root "$SS_CONT" bash -c "
    mkdir -p '${SOFTHSM_PERSISTENT_DIR}/lib' '${SOFTHSM_PERSISTENT_DIR}/bin' '${SOFTHSM_TOKEN_DIR}'
    chown -R 10001:root '${SOFTHSM_PERSISTENT_DIR}'
    chmod -R 750 '${SOFTHSM_PERSISTENT_DIR}'
    echo 'directories created'
"

# ── Copy SoftHSM2 library + util to persistent volume ─────────
#    These files survive SignServer container restarts because they
#    are on the ivf_signserver_persistent Docker volume.
info "Copying SoftHSM2 files to persistent volume..."
docker cp /tmp/libsofthsm2.so "$SS_CONT:${SOFTHSM_LIB_PERSISTENT}"
docker cp /tmp/softhsm2-util  "$SS_CONT:${SOFTHSM_UTIL_PERSISTENT}"
docker exec --user root "$SS_CONT" bash -c "
    chmod 644 '${SOFTHSM_LIB_PERSISTENT}'
    chmod 755 '${SOFTHSM_UTIL_PERSISTENT}'
    chown 10001:root '${SOFTHSM_LIB_PERSISTENT}' '${SOFTHSM_UTIL_PERSISTENT}'
    ls -la '${SOFTHSM_PERSISTENT_DIR}/lib/' '${SOFTHSM_PERSISTENT_DIR}/bin/'
"
ok "SoftHSM2 files copied to ${SOFTHSM_PERSISTENT_DIR}"

# ── Write softhsm2.conf to persistent volume ──────────────────
info "Writing softhsm2.conf to persistent volume..."
docker exec --user root "$SS_CONT" bash -c "
cat > '${SOFTHSM_CONF_PERSISTENT}' << 'SEOF'
directories.tokendir = ${SOFTHSM_TOKEN_DIR}
objectstore.backend = file
log.level = ERROR
slots.removable = false
SEOF
chown 10001:root '${SOFTHSM_CONF_PERSISTENT}'
echo 'softhsm2.conf written'
"
ok "softhsm2.conf saved at ${SOFTHSM_CONF_PERSISTENT}"

# ── Install environment-hsm startup hook ──────────────────────
# /opt/keyfactor/bin/start.sh sources every file matching
# /opt/keyfactor/*/environment-hsm at container startup.
# Writing this hook to the persistent volume means it runs on EVERY
# container restart BEFORE WildFly loads signserver_deploy.properties.
# The hook will:
#   1. Export SOFTHSM2_CONF
#   2. Initialize the PKCS#11 token (idempotent)
#   3. Register the library in signserver_deploy.properties
info "Writing persistent environment-hsm startup hook..."
docker exec --user root "$SS_CONT" bash << 'HEREDOC'
cat > '/opt/keyfactor/persistent/environment-hsm' << 'HOOKEOF'
#!/bin/bash
# SoftHSM2 persistent startup hook
# Sourced by /opt/keyfactor/bin/start.sh before WildFly starts
PDIR="/opt/keyfactor/persistent/softhsm"
LIB="${PDIR}/lib/libsofthsm2.so"
UTIL="${PDIR}/bin/softhsm2-util"
TOKEN_DIR="${PDIR}/tokens"
CONF="${PDIR}/softhsm2.conf"

[ -f "${LIB}" ] || { echo "[softhsm] Library not found at ${LIB}, skipping"; return 0; }

export SOFTHSM2_CONF="${CONF}"
chmod +x "${UTIL}" 2>/dev/null || true
mkdir -p "${TOKEN_DIR}" && chown -R 10001:root "${TOKEN_DIR}" 2>/dev/null || true

LABEL="${SOFTHSM_TOKEN_LABEL:-SignServerToken}"
PIN=$(cat "${SOFTHSM_USER_PIN_FILE:-/run/secrets/softhsm_pin}" 2>/dev/null || echo "changeit")
SO_PIN=$(cat "${SOFTHSM_SO_PIN_FILE:-/run/secrets/softhsm_so_pin}" 2>/dev/null || echo "changeit")

if ! "${UTIL}" --show-slots 2>/dev/null | grep -q "${LABEL}"; then
    echo "[softhsm] Initializing token: ${LABEL}"
    "${UTIL}" --init-token --free --label "${LABEL}" --so-pin "${SO_PIN}" --pin "${PIN}" \
        && echo "[softhsm] Token initialized" \
        || echo "[softhsm] WARNING: token init failed"
    chown -R 10001:root "${TOKEN_DIR}" 2>/dev/null || true
    chmod -R 750 "${TOKEN_DIR}"
else
    echo "[softhsm] Token '${LABEL}' already exists"
fi

DEPLOY="/opt/keyfactor/signserver-custom/conf/signserver_deploy.properties"
if [ -f "${DEPLOY}" ] && ! grep -q 'SoftHSM' "${DEPLOY}" 2>/dev/null; then
    printf '\n# SoftHSM2 PKCS#11 (auto-configured by environment-hsm hook)\n' >> "${DEPLOY}"
    echo "cryptotoken.p11.lib.83.name = SoftHSM"   >> "${DEPLOY}"
    echo "cryptotoken.p11.lib.83.file = ${LIB}"     >> "${DEPLOY}"
    echo "cryptotoken.p11.lib.84.name = SoftHSM 2"  >> "${DEPLOY}"
    echo "cryptotoken.p11.lib.84.file = ${LIB}"     >> "${DEPLOY}"
    echo "[softhsm] Registered library in ${DEPLOY}"
elif [ ! -f "${DEPLOY}" ]; then
    echo "[softhsm] WARNING: deploy.properties not found at ${DEPLOY}"
else
    echo "[softhsm] SoftHSM already registered in deploy.properties"
fi
export HSM_PKCS11_LIBRARY="${LIB}"
echo "[softhsm] Startup hook complete. Library=${LIB} Token=${LABEL}"
HOOKEOF
chmod +x '/opt/keyfactor/persistent/environment-hsm'
chown 10001:root '/opt/keyfactor/persistent/environment-hsm'
echo "Hook installed"
HEREDOC
ok "environment-hsm hook installed at /opt/keyfactor/persistent/environment-hsm"

# ── Restart SignServer so start.sh runs the hook + WildFly loads P11 lib ──
info "Restarting SignServer service..."
docker service update --force ivf_signserver 2>&1 | tail -3

info "Waiting for SignServer to become healthy (up to 5 min)..."
for i in $(seq 1 30); do
    sleep 10
    if curl -fsk https://localhost:9443/signserver/healthcheck/signserverhealth >/dev/null 2>&1; then
        ok "SignServer healthy (attempt $i)"
        break
    fi
    echo -n "."
    if [ "$i" -eq 30 ]; then
        err "SignServer did not become healthy within 5 min"
        exit 1
    fi
done

SS_CONT=$(find_ss)
ok "New container: $SS_CONT"

# Also copy binaries to /usr/ paths (some tools look there)
docker cp /tmp/libsofthsm2.so "$SS_CONT:/usr/lib64/pkcs11/libsofthsm2.so" 2>/dev/null || true
docker cp /tmp/softhsm2-util  "$SS_CONT:/usr/bin/softhsm2-util"            2>/dev/null || true

# Read actual PINs from Docker secrets
SOFTHSM_PIN=$(docker exec "$SS_CONT" cat /run/secrets/softhsm_pin 2>/dev/null || echo "changeit")
SOFTHSM_SO_PIN=$(docker exec "$SS_CONT" cat /run/secrets/softhsm_so_pin 2>/dev/null || echo "changeit")
info "PINs read from Docker secrets"

# Ensure token is initialized in new container context
info "Verifying/initializing SoftHSM2 token..."
docker exec --user root "$SS_CONT" bash -c "
    export SOFTHSM2_CONF='${SOFTHSM_CONF_PERSISTENT}'
    UTIL='${SOFTHSM_UTIL_PERSISTENT}'
    if ! \$UTIL --show-slots 2>/dev/null | grep -q '${TOKEN_LABEL}'; then
        echo 'Initializing token...'
        \$UTIL --init-token --free --label '${TOKEN_LABEL}' \
            --so-pin '${SOFTHSM_SO_PIN}' --pin '${SOFTHSM_PIN}'
        chown -R 10001:root '${SOFTHSM_TOKEN_DIR}' 2>/dev/null; chmod -R 750 '${SOFTHSM_TOKEN_DIR}'
        echo 'Token initialized'
    else
        echo 'Token already exists'
    fi
    \$UTIL --show-slots 2>/dev/null | grep -E 'Label|Slot|Token' | head -8
" 2>&1
ok "SoftHSM2 token verified"

# Verify registration in deploy.properties
info "Checking deploy.properties after restart:"
docker exec "$SS_CONT" grep -i softhsm \
    /opt/keyfactor/signserver-custom/conf/signserver_deploy.properties 2>/dev/null \
    && ok "SoftHSM registered in deploy.properties" \
    || {
        warn "SoftHSM NOT in deploy.properties — registering manually now..."
        docker exec --user root "$SS_CONT" bash -c "
            DEPLOY='/opt/keyfactor/signserver-custom/conf/signserver_deploy.properties'
            if [ -f \"\$DEPLOY\" ]; then
                printf '\n# SoftHSM2 PKCS#11\n' >> \"\$DEPLOY\"
                echo 'cryptotoken.p11.lib.83.name = SoftHSM'   >> \"\$DEPLOY\"
                echo 'cryptotoken.p11.lib.83.file = ${SOFTHSM_LIB_PERSISTENT}' >> \"\$DEPLOY\"
                echo 'cryptotoken.p11.lib.84.name = SoftHSM 2'  >> \"\$DEPLOY\"
                echo 'cryptotoken.p11.lib.84.file = ${SOFTHSM_LIB_PERSISTENT}' >> \"\$DEPLOY\"
                echo 'Registered'
            else
                echo \"ERROR: \$DEPLOY not found\"
            fi"
        warn "NOTE: WildFly is already running — the library won't be recognized until the NEXT restart."
        warn "If Phase 5 fails with 'SHAREDLIBRARYNAME not found', run: docker service update --force ivf_signserver"
    }


# ════════════════════════════════════════════════════════════
# PHASE 4: Create / Reset EJBCA End Entities
# ════════════════════════════════════════════════════════════
step "Phase 4: EJBCA End Entity Creation"

info "Exporting CA certificates from EJBCA..."
ejbca ca getcacert --caname "$ISSUING_CA"  -f /tmp/sub-ca.pem  2>/dev/null \
    && ok "  Sub-CA cert exported"  || warn "  Could not export Sub-CA cert"
ejbca ca getcacert --caname "IVF-Root-CA" -f /tmp/root-ca.pem 2>/dev/null \
    && ok "  Root CA cert exported" || warn "  Could not export Root CA cert"

create_end_entity() {
    local username="$1" cn="$2" ou="$3" cert_profile="$4" ee_profile="$5"
    substep "$username  (CN=$cn)"
    ejbca ra addendentity \
        --username    "$username" \
        --dn          "CN=$cn,O=IVF Healthcare,OU=$ou,C=VN" \
        --caname      "$ISSUING_CA" \
        --type        1 \
        --token       P12 \
        --password    "$KEYSTORE_PASSWORD" \
        --certprofile "$cert_profile" \
        --eeprofile   "$ee_profile" 2>/dev/null \
        && ok "    Created: $username" \
        || {
            warn "    Already exists – resetting status to NEW"
            ejbca ra setendentitystatus "$username" 10 2>/dev/null || true
        }
    ejbca ra setclearpwd "$username" "$KEYSTORE_PASSWORD" 2>/dev/null || true
}

for w in "${PDF_WORKERS[@]}"; do
    IFS='|' read -r wid wname wkey wcprof weeprof wcn <<< "$w"
    create_end_entity "ivf-signer-${wid}" "$wcn" "Digital Signing" "$wcprof" "$weeprof"
done

IFS='|' read -r tsa_id tsa_name tsa_key tsa_cprof tsa_eeprof tsa_cn <<< "$TSA_WORKER"
create_end_entity "ivf-tsa-${tsa_id}" "$tsa_cn" "Timestamp Authority" "$tsa_cprof" "$tsa_eeprof"
ok "All end entities ready"


# ════════════════════════════════════════════════════════════
# PHASE 5: PKCS#11 Worker Configuration + Key Gen + Cert Enrollment
# ════════════════════════════════════════════════════════════
step "Phase 5: SignServer PKCS#11 Configuration"

set_pkcs11_token_props() {
    local worker_id="$1" key_alias="$2"
    # Remove legacy P12 props
    ss removeproperty "$worker_id" KEYSTOREPATH     2>/dev/null || true
    ss removeproperty "$worker_id" KEYSTOREPASSWORD 2>/dev/null || true
    ss removeproperty "$worker_id" KEYSTORETYPE     2>/dev/null || true
    ss removeproperty "$worker_id" SHAREDLIBRARY    2>/dev/null || true
    # Set PKCS#11 props
    ss setproperty "$worker_id" CRYPTOTOKEN_IMPLEMENTATION_CLASS \
        org.signserver.server.cryptotokens.PKCS11CryptoToken  2>/dev/null || true
    ss setproperty "$worker_id" SHAREDLIBRARYNAME SoftHSM       2>/dev/null || true
    ss setproperty "$worker_id" SLOTLABELTYPE     SLOT_LABEL     2>/dev/null || true
    ss setproperty "$worker_id" SLOTLABELVALUE    "$TOKEN_LABEL" 2>/dev/null || true
    ss setproperty "$worker_id" PIN               "$SOFTHSM_PIN" 2>/dev/null || true
    ss setproperty "$worker_id" DEFAULTKEY        "$key_alias"   2>/dev/null || true
}

enroll_pkcs11_cert() {
    local worker_id="$1" key_alias="$2" cn="$3" ou="$4" ee_username="$5"
    local csr_file="/tmp/worker_${worker_id}_csr.pem"
    local signed_cert="/tmp/worker_${worker_id}_signed.pem"

    ss reload "$worker_id" 2>/dev/null || true
    # First activation (may fail before key exists – ignore)
    ss activatecryptotoken "$worker_id" "$SOFTHSM_PIN" 2>/dev/null || true

    # Generate RSA-4096 key
    substep "Generating RSA-4096 key '${key_alias}'..."
    ss generatekey "$worker_id" \
        -keyalg RSA -keyspec 4096 -alias "$key_alias" 2>/dev/null \
        && ok "    Key '${key_alias}' generated" \
        || warn "    generatekey returned non-zero (key may already exist)"

    # Reload + activate
    ss reload "$worker_id" 2>/dev/null || true
    ACT=$(ss activatecryptotoken "$worker_id" "$SOFTHSM_PIN" 2>&1) || true
    if echo "$ACT" | grep -qi "error\|failed"; then
        err "    Token activation failed: $ACT"
        return 1
    fi
    ok "    Token activated"

    # Generate CSR
    substep "Generating CSR..."
    ss generatecertreq "$worker_id" \
        "CN=${cn},O=IVF Healthcare,OU=${ou},C=VN" \
        SHA256WithRSA \
        "$csr_file" 2>/dev/null \
        || { err "    CSR generation failed"; return 1; }
    ok "    CSR: $csr_file"

    # Copy CSR to EJBCA container
    docker exec "$SS_CONT" cat "$csr_file" | \
        docker exec -i "$EJBCA_CONT" bash -c "cat > $csr_file"

    # Reset EE to NEW
    ejbca ra setendentitystatus "$ee_username" 10         2>/dev/null || true
    ejbca ra setclearpwd        "$ee_username" "$KEYSTORE_PASSWORD" 2>/dev/null || true

    # Sign CSR
    substep "EJBCA signing CSR for ${ee_username}..."
    ejbca createcert \
        --username "$ee_username" \
        --password "$KEYSTORE_PASSWORD" \
        -c         "$csr_file" \
        -f         "$signed_cert" 2>/dev/null \
        || { err "    EJBCA cert signing failed"; return 1; }
    ok "    Cert signed by EJBCA"

    # Strip PEM header lines from cert
    docker exec "$EJBCA_CONT" bash -c \
        "sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' $signed_cert" | \
        docker exec -i "$SS_CONT" bash -c "cat > /tmp/worker_${worker_id}_cert.pem"

    # Upload signer cert
    substep "Uploading signer certificate..."
    ss uploadsignercertificate "$worker_id" GLOB \
        "/tmp/worker_${worker_id}_cert.pem" 2>/dev/null \
        || { err "    Cert upload failed"; return 1; }

    # Build cert chain: signer + Sub-CA + Root CA
    substep "Uploading certificate chain..."
    docker exec "$EJBCA_CONT" bash -c "
        sed -n '/-----BEGIN CERTIFICATE-----/,/-----END CERTIFICATE-----/p' $signed_cert
        cat /tmp/sub-ca.pem   2>/dev/null || true
        cat /tmp/root-ca.pem  2>/dev/null || true
    " | docker exec -i "$SS_CONT" bash -c "cat > /tmp/worker_${worker_id}_chain.pem"

    ss uploadsignercertificatechain "$worker_id" GLOB \
        "/tmp/worker_${worker_id}_chain.pem" 2>/dev/null \
        || warn "    Chain upload returned non-zero (may be OK)"

    ss reload "$worker_id" 2>/dev/null || true
    ok "    Enrollment complete for Worker ${worker_id}"
}

configure_pdf_worker() {
    local worker_id="$1" worker_name="$2" key_alias="$3" cn="$4"
    local ee_username="ivf-signer-${worker_id}"
    echo ""
    info "━━━━ Worker $worker_id: $worker_name ━━━━"
    set_pkcs11_token_props "$worker_id" "$key_alias"
    ss setproperty "$worker_id" CERTIFICATION_LEVEL           NOT_CERTIFIED        2>/dev/null || true
    ss setproperty "$worker_id" ADD_VISIBLE_SIGNATURE         false                2>/dev/null || true
    ss setproperty "$worker_id" REFUSE_DOUBLE_INDIRECT_OBJECTS true               2>/dev/null || true
    ss setproperty "$worker_id" REASON                        "Xac nhan bao cao y te IVF" 2>/dev/null || true
    ss setproperty "$worker_id" LOCATION                      "IVF Clinic"         2>/dev/null || true
    ss setproperty "$worker_id" TSA_WORKER                    "TimeStampSigner"    2>/dev/null || true
    ss setproperty "$worker_id" EMBED_CRL                     true                 2>/dev/null || true
    ss setproperty "$worker_id" DIGESTALGORITHM               SHA256               2>/dev/null || true
    enroll_pkcs11_cert "$worker_id" "$key_alias" "$cn" "Digital Signing" "$ee_username" || {
        warn "Worker $worker_id enrollment failed"
        return 1
    }
    STATUS=$(ss getstatus brief "$worker_id" 2>&1) || true
    if echo "$STATUS" | grep -q "Worker status : Active"; then
        ok "Worker $worker_id ($worker_name) ── PKCS#11 Active ✓"
    else
        warn "Worker $worker_id not Active:"
        echo "$STATUS" | grep -E "status|Error|WARN" | head -5 || true
        return 1
    fi
}

configure_tsa_worker() {
    local worker_id="$1" worker_name="$2" key_alias="$3" cn="$4"
    local ee_username="ivf-tsa-${worker_id}"
    echo ""
    info "━━━━ Worker $worker_id: $worker_name (TSA) ━━━━"
    set_pkcs11_token_props "$worker_id" "$key_alias"
    ss setproperty "$worker_id" DEFAULTTSAPOLICYOID "1.2.3.4.1" 2>/dev/null || true
    ss setproperty "$worker_id" ACCEPTANYPOLICY     true         2>/dev/null || true
    ss setproperty "$worker_id" ACCURACYMICROS      500          2>/dev/null || true
    ss setproperty "$worker_id" ORDERING            false        2>/dev/null || true
    ss setproperty "$worker_id" INCLUDESTATUSSTRING true         2>/dev/null || true
    enroll_pkcs11_cert "$worker_id" "$key_alias" "$cn" "Timestamp Authority" "$ee_username" || {
        warn "TSA Worker $worker_id enrollment failed"
        return 1
    }
    STATUS=$(ss getstatus brief "$worker_id" 2>&1) || true
    if echo "$STATUS" | grep -q "Worker status : Active"; then
        ok "TSA Worker $worker_id ($worker_name) ── PKCS#11 Active ✓"
    else
        warn "TSA Worker $worker_id not Active:"
        echo "$STATUS" | grep -E "status|Error|WARN" | head -5 || true
        return 1
    fi
}

# ── Configure all workers ─────────────────────────────────────
FAILED=0
for w in "${PDF_WORKERS[@]}"; do
    IFS='|' read -r wid wname wkey wcprof weeprof wcn <<< "$w"
    configure_pdf_worker "$wid" "$wname" "$wkey" "$wcn" || FAILED=$((FAILED + 1))
done

IFS='|' read -r tsa_id tsa_name tsa_key tsa_cprof tsa_eeprof tsa_cn <<< "$TSA_WORKER"
configure_tsa_worker "$tsa_id" "$tsa_name" "$tsa_key" "$tsa_cn" || FAILED=$((FAILED + 1))


# ════════════════════════════════════════════════════════════
# Final Verification
# ════════════════════════════════════════════════════════════
step "Final Verification"

ACTIVE=0; NOT_ACTIVE=0
for w in "${PDF_WORKERS[@]}" "$TSA_WORKER"; do
    IFS='|' read -r wid wname _ <<< "$w"
    STATUS=$(ss getstatus brief "$wid" 2>&1) || true
    if echo "$STATUS" | grep -q "Worker status : Active"; then
        ok "  Worker $wid ($wname): Active"
        ACTIVE=$((ACTIVE + 1))
    else
        ERR=$(echo "$STATUS" | grep -iE "error|warn|status" | head -2 || true)
        warn "  Worker $wid ($wname): NOT Active  ← $ERR"
        NOT_ACTIVE=$((NOT_ACTIVE + 1))
    fi
done

echo ""
echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"
echo -e " Total workers: $((ACTIVE + NOT_ACTIVE))   Active: ${GREEN}${ACTIVE}${NC}   Not Active: ${RED}${NOT_ACTIVE}${NC}"
echo -e "${CYAN}═══════════════════════════════════════════════════════${NC}"

if [ "$NOT_ACTIVE" -eq 0 ]; then
    ok "All 6 workers Active with SoftHSM PKCS#11 + EJBCA-signed certificates"
    echo ""
    echo "  Crypto token: SoftHSM2 v2.6.1 (PKCS#11)  label: $TOKEN_LABEL"
    echo "  Library:      $SOFTHSM_LIB_PERSISTENT (persistent volume)"
    echo "  Token dir:    $SOFTHSM_TOKEN_DIR"
    echo "  Startup hook: /opt/keyfactor/persistent/environment-hsm"
    echo "  PINs:         from Docker secrets softhsm_pin / softhsm_so_pin"
else
    warn "$NOT_ACTIVE worker(s) did not activate – check log above"
    exit 1
fi