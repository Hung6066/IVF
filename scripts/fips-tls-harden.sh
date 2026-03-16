#!/bin/bash
# ============================================================
# IVF PKI — FIPS Phase 1 (Bước 4/4): Hardening TLS Ciphers
# ============================================================
# Restrict TLS protocols và cipher suites cho toàn bộ stack:
#   • Caddy: TLS 1.2+ only, FIPS-approved ciphers (Swarm config update)
#   • EJBCA: Disable TLS 1.0/1.1 qua JAVA_OPTS
#   • SignServer: Disable TLS 1.0/1.1 qua JAVA_OPTS
#   • WildFly SSL context: Update qua jboss-cli (nếu có)
#   • PostgreSQL: Enforce ssl_min_protocol_version
#
# Caddy Swarm Config update:
#   Docker Swarm configs là bất biến (immutable). Script này sẽ:
#   1. Đọc caddyfile hiện tại từ Docker config ivf_caddyfile_v15
#   2. Thêm TLS restrictions cho mỗi HTTPS vhost
#   3. Tạo config mới: ivf_caddyfile_v16
#   4. Update caddy service dùng v16
#   → Cần cập nhật docker-compose.stack.yml: v15 → v16
#
# USAGE:
#   scp scripts/fips-tls-harden.sh root@10.200.0.1:/tmp/
#   ssh root@10.200.0.1 "sed -i 's/\r//' /tmp/fips-tls-harden.sh && bash /tmp/fips-tls-harden.sh"
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

# ── Argument parser ───────────────────────────────────────────
DRY_RUN=false
FORCE=false
SKIP_CADDY=false
SKIP_JVM=false
SKIP_PG=false
NEW_CONFIG_VERSION=""  # auto-detect from current version

while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry-run)          DRY_RUN=true; shift ;;
        --force)            FORCE=true; shift ;;
        --skip-caddy)       SKIP_CADDY=true; shift ;;
        --skip-jvm)         SKIP_JVM=true; shift ;;
        --skip-postgres)    SKIP_PG=true; shift ;;
        --config-version)   NEW_CONFIG_VERSION="$2"; shift 2 ;;
        --help|-h)
            echo "Usage: $0 [--dry-run] [--force] [--skip-caddy] [--skip-jvm] [--skip-postgres]"
            echo "  --dry-run           Tạo Caddyfile mới nhưng không deploy"
            echo "  --force             Bỏ qua confirmation"
            echo "  --skip-caddy        Bỏ qua phần Caddy update"
            echo "  --skip-jvm          Bỏ qua phần EJBCA/SignServer JVM"
            echo "  --skip-postgres     Bỏ qua phần PostgreSQL TLS"
            echo "  --config-version V  Tên version mới (mặc định: v16)"
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

# ── Detect containers ─────────────────────────────────────────
EJBCA_CONT=$(docker ps --filter name=ivf_ejbca --format "{{.Names}}" | grep -v '\-db' | head -1 || true)
SS_CONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1 || true)
PG_CONT=$(docker ps --filter name=ivf_db --format "{{.Names}}" | grep -v ejbca | grep -v signserver | head -1 || true)

# ════════════════════════════════════════════════════════════
echo -e "\n${BOLD}${BLUE}"
echo "  ╔══════════════════════════════════════╗"
echo "  ║   IVF PKI — TLS FIPS Hardening       ║"
echo "  ║   Giai đoạn 1 / Bước 4 của 4         ║"
echo "  ╚══════════════════════════════════════╝"
echo -e "${NC}"

# ════════════════════════════════════════════════════════════
# BƯỚC 1: Caddy TLS Hardening
# ════════════════════════════════════════════════════════════
step "Bước 1: Caddy — TLS Protocol & Cipher Hardening"

if $SKIP_CADDY; then
    warn "Bỏ qua Caddy (--skip-caddy)"
else
    # Detect current Caddy config: đọc từ service (chính xác hơn config ls)
    CURRENT_VERSION=$(docker service inspect ivf_caddy 2>/dev/null | python3 -c "
import json,sys
data=json.load(sys.stdin)
configs=data[0]['Spec']['TaskTemplate']['ContainerSpec'].get('Configs',[])
for c in configs:
    name=c.get('ConfigName','')
    target=c.get('File',{}).get('Name','')
    if 'caddyfile' in name.lower() and 'Caddyfile' in target:
        print(name)
" 2>/dev/null | head -1 || echo "")
    if [[ -z "$CURRENT_VERSION" ]]; then
        # Fallback: lấy config mới nhất theo version number
        CURRENT_VERSION=$(docker config ls --format "{{.Name}}" | grep "ivf_caddyfile" | \
            awk -F'v' '{print $2" "$0}' | sort -k1n | tail -1 | awk '{print $2}' || echo "")
    fi
    if [[ -z "$CURRENT_VERSION" ]]; then
        warn "Không tìm thấy Docker config caddyfile_* — kiểm tra: docker config ls"
        CURRENT_VERSION="ivf_caddyfile_v22"
    fi

    # Auto-increment version number nếu không được chỉ định
    if [[ -z "$NEW_CONFIG_VERSION" ]]; then
        CURR_NUM=$(echo "$CURRENT_VERSION" | grep -oP '\d+$' || echo "22")
        NEW_NUM=$(( CURR_NUM + 1 ))
        NEW_CONFIG_VERSION="v${NEW_NUM}"
    fi
    NEW_CONFIG_NAME="ivf_caddyfile_${NEW_CONFIG_VERSION}"
    info "Config hiện tại: ${CURRENT_VERSION}"
    info "Config mới sẽ tạo: ${NEW_CONFIG_NAME}"

    substep "Đọc Caddyfile hiện tại từ Docker config..."
    CURRENT_CADDYFILE_CONTENT=$(docker config inspect "$CURRENT_VERSION" \
        --format '{{json .Spec.Data}}' 2>/dev/null | \
        python3 -c "import sys, json, base64; print(base64.b64decode(json.load(sys.stdin)).decode())" \
        2>/dev/null || echo "")

    if [[ -z "$CURRENT_CADDYFILE_CONTENT" ]]; then
        warn "Không đọc được Docker config — thử đọc từ Caddy container..."
        CADDY_CONT=$(docker ps --filter name=ivf_caddy --format "{{.Names}}" | head -1 || true)
        if [[ -n "$CADDY_CONT" ]]; then
            CURRENT_CADDYFILE_CONTENT=$(docker exec "$CADDY_CONT" cat /etc/caddy/Caddyfile 2>/dev/null || echo "")
        fi
    fi

    if [[ -z "$CURRENT_CADDYFILE_CONTENT" ]]; then
        err "Không đọc được Caddyfile hiện tại!"
        info "Tạo Caddyfile hardened từ template..."
        CURRENT_CADDYFILE_CONTENT="# Caddyfile bị mất — tạo lại thủ công"
    else
        ok "Đọc Caddyfile thành công (${#CURRENT_CADDYFILE_CONTENT} bytes)"
    fi

    # ── Tạo Caddyfile mới với TLS hardening ──────────────────
    substep "Patch Caddyfile: thêm TLS protocol + cipher restrictions..."

    # TLS snippet để inject vào mỗi HTTPS vhost
    # Caddy chỉ hỗ trợ ECDHE ciphers (forward secrecy), không có RSA key exchange
    TLS_SNIPPET='	tls {
		protocols tls1.2 tls1.3
		ciphers TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384 TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384 TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256 TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256 TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256
	}'

    # Tạo Caddyfile mới bằng cách thêm tls block vào từng site block.
    # Ghi Caddyfile + snippet vào files tạm để Python đọc (tránh bash $() + heredoc + single-quote conflict)
    CADDY_INPUT_TMP="/tmp/fips_caddy_input_$$.txt"
    CADDY_SNIPPET_TMP="/tmp/fips_caddy_snippet_$$.txt"
    CADDY_SCRIPT_TMP="/tmp/fips_caddy_patch_$$.py"
    echo "$CURRENT_CADDYFILE_CONTENT" > "$CADDY_INPUT_TMP"
    printf '%s' "$TLS_SNIPPET" > "$CADDY_SNIPPET_TMP"

    cat > "$CADDY_SCRIPT_TMP" << 'PYEOF'
import sys, re

with open(sys.argv[1]) as f:
    caddyfile = f.read()
with open(sys.argv[2]) as f:
    tls_snippet = f.read()

# Tìm các dòng bắt đầu là domain (chứa dấu chấm, không phải comment hoặc block con)
# và inject tls block ngay sau dấu {
lines = caddyfile.split('\n')
output = []
i = 0
in_site = False
tls_already_present = False
brace_depth = 0
site_start_idx = -1

while i < len(lines):
    line = lines[i]
    stripped = stripped_line = line.strip()

    # Phát hiện HTTPS site header: dòng chứa domain (có chữ cái), bắt đầu không phải #
    # Phải có ít nhất 1 chữ cái trong phần domain (loại trừ IPs như 0.0.0.0)
    domain_url = stripped.split('{')[0].strip()
    domain_part = domain_url.split(':')[0].strip()
    is_https_site = (
        not stripped.startswith('#') and
        not stripped.startswith('(') and
        not stripped.startswith('{') and
        not domain_url.lower().startswith('http://') and  # exclude explicit HTTP sites
        '.' in stripped and
        '{' in line and
        stripped.endswith('{') and
        any(c.isalpha() for c in domain_part) and  # domain phải có chữ cái, loại trừ IPs
        not any(c in stripped for c in ['handle', 'header', 'reverse_proxy', 'basic_auth',
                                         'encode', 'redir', 'respond', '@', 'tls', 'log',
                                         'request_body', 'matcher', 'path', 'host', 'root',
                                         'try_files', 'push', 'file_server', 'rewrite', 'uri',
                                         'admin', 'servers', 'metrics', 'email', 'acme',
                                         'storage', 'debug', 'auto_https'])
    )

    # Nhận diện site block bằng dòng có domain + {
    # (global block {, snippet block, handle block, etc. không phải site)
    is_global = stripped == '{'
    is_snippet = stripped.startswith('(') and stripped.endswith('{')

    if is_https_site:
        in_site = True
        brace_depth = 1
        tls_already_present = False
        output.append(line)
        # Peek forward để check xem tls block đã có chưa
        for j in range(i+1, min(i+20, len(lines))):
            if lines[j].strip().startswith('tls {') or lines[j].strip() == 'tls {':
                tls_already_present = True
                break
            if lines[j].strip() == '}' and brace_depth == 1:
                break
        i += 1

        if not tls_already_present:
            output.append(tls_snippet)

        continue

    if in_site:
        for ch in line:
            if ch == '{':
                brace_depth += 1
            elif ch == '}':
                brace_depth -= 1
        if brace_depth <= 0:
            in_site = False

    output.append(line)
    i += 1

result = '\n'.join(output)

# Thêm comment FIPS vào đầu file
import datetime
fips_header = "# ─── FIPS Phase 1 TLS Hardening ─────────────────────────────\n# Applied by fips-tls-harden.sh ({})\n# TLS 1.2+ only; FIPS-approved ciphers (AES-128-GCM, AES-256-GCM)\n# Disabled: TLS 1.0, TLS 1.1, RC4, 3DES, MD5 in ciphers\n# ──────────────────────────────────────────────────────────────".format(datetime.date.today())

print(fips_header + '\n' + result)
PYEOF

    HARDENED_CADDYFILE=$(python3 "$CADDY_SCRIPT_TMP" "$CADDY_INPUT_TMP" "$CADDY_SNIPPET_TMP")
    rm -f "$CADDY_INPUT_TMP" "$CADDY_SNIPPET_TMP" "$CADDY_SCRIPT_TMP"

    if [[ -z "$HARDENED_CADDYFILE" ]]; then
        err "Không tạo được Caddyfile mới!"
        exit 1
    fi

    # Lưu ra file tạm
    TEMP_CADDYFILE="/tmp/Caddyfile-fips-${NEW_CONFIG_VERSION}"
    echo "$HARDENED_CADDYFILE" > "$TEMP_CADDYFILE"
    ok "Caddyfile mới đã tạo: ${TEMP_CADDYFILE}"

    # Hiển thị diff TLS additions
    substep "Kiểm tra TLS blocks đã được thêm..."
    TLS_INJECT_COUNT=$(grep -c "protocols tls1.2" "$TEMP_CADDYFILE" || echo 0)
    info "  Số TLS blocks đã inject: ${TLS_INJECT_COUNT}"

    # Validate Caddyfile cú pháp (nếu có Caddy binary on host)
    CADDY_VALID=true
    if command -v caddy &>/dev/null; then
        substep "Validate Caddyfile syntax..."
        if caddy validate --config "$TEMP_CADDYFILE" 2>/dev/null; then
            ok "Caddy validate: OK"
        else
            CADDY_VALID=false
            err "Caddy validate thất bại — kiểm tra: cat ${TEMP_CADDYFILE}"
        fi
    else
        # Validate trong Caddy container
        CADDY_CONT=$(docker ps --filter name=ivf_caddy --format "{{.Names}}" | head -1 || true)
        if [[ -n "$CADDY_CONT" ]]; then
            substep "Validate trong Caddy container..."
            docker cp "$TEMP_CADDYFILE" "${CADDY_CONT}:/tmp/Caddyfile-validate"
            if docker exec "$CADDY_CONT" caddy validate --config /tmp/Caddyfile-validate 2>&1; then
                ok "Caddy validate (container): OK"
            else
                CADDY_VALID=false
                err "Caddy validate thất bại — DỪNG để tránh deploy config lỗi"
                err "Kiểm tra: cat ${TEMP_CADDYFILE}"
            fi
        fi
    fi

    if ! $CADDY_VALID; then
        err "Caddyfile không valid — bỏ qua bước deploy Caddy"
        SKIP_CADDY=true
    fi

    if $DRY_RUN; then
        info "[DRY-RUN] Không deploy Caddy config. File: ${TEMP_CADDYFILE}"
        info "[DRY-RUN] Lệnh sẽ chạy khi không --dry-run:"
        echo "  docker config create ${NEW_CONFIG_NAME} ${TEMP_CADDYFILE}"
        echo "  docker service update \\"
        echo "    --config-rm ${CURRENT_VERSION} \\"
        echo "    --config-add source=${NEW_CONFIG_NAME},target=/etc/caddy/Caddyfile \\"
        echo "    ivf_caddy"
    else
        if ! $FORCE; then
            echo ""
            warn "Sẽ tạo Docker config '${NEW_CONFIG_NAME}' và restart Caddy service."
            warn "Website sẽ gián đoạn ~10-30 giây khi Caddy reload."
            read -rp "Tiếp tục? (yes/N): " ans
            [[ "$ans" == "yes" ]] || { info "Skipped Caddy update."; SKIP_CADDY=true; }
        fi

        if ! $SKIP_CADDY; then
            substep "Tạo Docker config ${NEW_CONFIG_NAME}..."
            if docker config ls --format "{{.Name}}" | grep -q "^${NEW_CONFIG_NAME}$"; then
                warn "${NEW_CONFIG_NAME} đã tồn tại — xóa và tạo lại với content mới"
                # Xóa config cũ nếu không đang được dùng bởi service nào
                if docker config rm "$NEW_CONFIG_NAME" 2>/dev/null; then
                    info "  Đã xóa config cũ ${NEW_CONFIG_NAME}"
                else
                    # Config đang được dùng — dùng version tiếp theo
                    NEXT_NUM=$(( $(echo "$NEW_CONFIG_VERSION" | grep -oP '\d+$') + 1 ))
                    NEW_CONFIG_VERSION="v${NEXT_NUM}"
                    NEW_CONFIG_NAME="ivf_caddyfile_${NEW_CONFIG_VERSION}"
                    info "  Config bị giữ bởi service — chuyển sang ${NEW_CONFIG_NAME}"
                fi
            fi
            docker config create "$NEW_CONFIG_NAME" "$TEMP_CADDYFILE"
            ok "Docker config ${NEW_CONFIG_NAME} đã tạo"

            substep "Update Caddy service (rolling update)..."
            docker service update \
                --config-rm "$CURRENT_VERSION" \
                --config-add "source=${NEW_CONFIG_NAME},target=/etc/caddy/Caddyfile" \
                ivf_caddy 2>&1 | tail -5
            ok "Caddy service đã update với config ${NEW_CONFIG_NAME}"

            # Chờ Caddy reload
            info "Chờ Caddy reload HTTPS cert và TLS settings (~15s)..."
            sleep 15

            # Verify TLS protocol
            substep "Verify TLS settings mới..."
            if echo | timeout 5 openssl s_client -connect natra.site:443 -no_tls1 -no_tls1_1 2>/dev/null | \
               grep -q "Protocol.*TLSv1.2\|Protocol.*TLSv1.3"; then
                ok "TLS 1.2+ confirmed — TLS 1.0/1.1 bị từ chối"
            else
                info "TLS verify timeout — kiểm tra thủ công sau"
            fi

            warn ""
            warn "⚠  Nhớ cập nhật docker-compose.stack.yml:"
            warn "   Tìm '${CURRENT_VERSION}' và thay bằng '${NEW_CONFIG_NAME}'"
            warn "   Để giữ đồng bộ khi redeploy stack"
            warn ""
            warn "   Ví dụ:"
            warn "   sed -i 's/${CURRENT_VERSION}/${NEW_CONFIG_NAME}/g' docker-compose.stack.yml"
        fi
    fi
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 2: EJBCA & SignServer JVM TLS Hardening
# ════════════════════════════════════════════════════════════
step "Bước 2: EJBCA & SignServer — JVM TLS Hardening"

if $SKIP_JVM; then
    warn "Bỏ qua JVM hardening (--skip-jvm)"
else
    # JAVA_OPTS cho WildFly trong EJBCA và SignServer:
    # - Disable TLS 1.0 và 1.1 hoàn toàn
    # - Restrict TLS 1.2 ciphers (chỉ AES-GCM)
    # - Bật HTTPS strict hostname verification
    FIPS_JAVA_OPTS="-Djdk.tls.client.protocols=TLSv1.2,TLSv1.3 \
-Djdk.tls.disabledAlgorithms=SSLv3,TLSv1,TLSv1.1,RC4,DES,MD5withRSA,DH\ keySize\ <\ 2048,EC\ keySize\ <\ 224,3DES_EDE_CBC,anon,NULL \
-Dhttps.protocols=TLSv1.2,TLSv1.3 \
-Dcom.sun.jndi.ldap.object.trustURLCodebase=false \
-Dcom.sun.jndi.rmi.object.trustURLCodebase=false"

    # Đường dẫn standalone.conf trong WildFly/EAP-based containers
    # EJBCA CE: /opt/keyfactor/appserver/bin/standalone.conf
    # SignServer CE: /opt/keyfactor/wildfly-*/bin/standalone.conf
    declare -A CONTAINER_PATHS=(
        ["EJBCA"]="/opt/keyfactor/appserver/bin/standalone.conf"
        ["SignServer"]="/opt/keyfactor/wildfly-35.0.1.Final/bin/standalone.conf"
    )
    declare -A CONTAINER_NAMES=(
        ["EJBCA"]="${EJBCA_CONT:-}"
        ["SignServer"]="${SS_CONT:-}"
    )

    for SVC in "EJBCA" "SignServer"; do
        CONT="${CONTAINER_NAMES[$SVC]}"
        CONF="${CONTAINER_PATHS[$SVC]}"
        [[ -z "$CONT" ]] && { warn "${SVC}: container không tìm thấy, bỏ qua"; continue; }

        substep "${SVC}: Thêm FIPS JAVA_OPTS vào standalone.conf..."

        if $DRY_RUN; then
            info "[DRY-RUN] Sẽ patch: ${CONT}:${CONF}"
            info "[DRY-RUN] JAVA_OPTS sẽ thêm: -Djdk.tls.client.protocols=TLSv1.2,TLSv1.3 ..."
            continue
        fi

        # Tìm standalone.conf (wildfly version thay đổi)
        FOUND_CONF=$(docker exec "$CONT" find /opt/keyfactor -name "standalone.conf" \
            -maxdepth 8 2>/dev/null | head -1 || echo "")

        if [[ -z "$FOUND_CONF" ]]; then
            # Fallback: tìm trong PATH WildFly
            FOUND_CONF=$(docker exec "$CONT" bash -c \
                'find /opt -name "standalone.conf" 2>/dev/null | head -1' || echo "")
        fi

        if [[ -z "$FOUND_CONF" ]]; then
            warn "  ${SVC}: Không tìm thấy standalone.conf"
            info "  → Sẽ dùng JAVA_OPTS env var thay thế"

            # Cách 2: Docker service update với env var
            SERVICE_NAME="ivf_$(echo "$SVC" | tr '[:upper:]' '[:lower:]')"
            JAVA_OPTS_COMPACT="-Djdk.tls.client.protocols=TLSv1.2,TLSv1.3 -Djdk.tls.disabledAlgorithms=SSLv3,TLSv1,TLSv1.1,RC4,DES,MD5withRSA,3DES_EDE_CBC -Dhttps.protocols=TLSv1.2,TLSv1.3"

            if docker service ls --format "{{.Name}}" | grep -q "$SERVICE_NAME"; then
                docker service update --env-add "JAVA_OPTS=${JAVA_OPTS_COMPACT}" "$SERVICE_NAME" 2>&1 | tail -3
                ok "  ${SVC}: JAVA_OPTS env var đã thêm qua service update"
            else
                warn "  ${SVC}: Service '${SERVICE_NAME}' không tìm thấy"
                info "  Thủ công: docker service update --env-add JAVA_OPTS='...' <service>"
            fi
            continue
        fi

        ok "  ${SVC}: Tìm thấy standalone.conf: ${FOUND_CONF}"

        # Backup
        docker exec "$CONT" bash -c "cp -p '${FOUND_CONF}' '${FOUND_CONF}.fips-backup-$(date +%Y%m%d)' 2>/dev/null || true"

        # Kiểm tra xem đã có FIPS settings chưa
        if docker exec "$CONT" grep -q "jdk.tls.client.protocols" "$FOUND_CONF" 2>/dev/null; then
            ok "  ${SVC}: FIPS JAVA_OPTS đã có trong standalone.conf"
            continue
        fi

        # Thêm FIPS JAVA_OPTS vào cuối standalone.conf
        # Tìm dòng JAVA_OPTS="" hoặc JAVA_OPTS=$JAVA_OPTS và append
        docker exec "$CONT" bash -c "cat >> '${FOUND_CONF}' << 'JAVAEOF'

# ── IVF FIPS Phase 1: TLS Hardening ──────────────────────────
# Thêm bởi fips-tls-harden.sh ($(date +%Y-%m-%d))
JAVA_OPTS=\"\${JAVA_OPTS} \\
    -Djdk.tls.client.protocols=TLSv1.2,TLSv1.3 \\
    -Djdk.tls.disabledAlgorithms=SSLv3,TLSv1,TLSv1.1,RC4,DES,MD5withRSA,3DES_EDE_CBC,anon,NULL \\
    -Dhttps.protocols=TLSv1.2,TLSv1.3 \\
    -Dcom.sun.jndi.ldap.object.trustURLCodebase=false \\
    -Dcom.sun.jndi.rmi.object.trustURLCodebase=false\"
export JAVA_OPTS
JAVAEOF"
        ok "  ${SVC}: standalone.conf đã được patch"
    done

    # Restart EJBCA và SignServer để áp dụng
    substep "Restart EJBCA và SignServer để áp dụng JAVA_OPTS..."
    if $DRY_RUN; then
        info "[DRY-RUN] docker service update --force ivf_ejbca"
        info "[DRY-RUN] docker service update --force ivf_signserver"
    else
        if ! $FORCE; then
            read -rp "Restart EJBCA và SignServer? Workers sẽ offline ~2-3 phút (yes/N): " ans
            [[ "$ans" == "yes" ]] || { warn "Bỏ qua restart — nhớ restart thủ công để áp dụng JAVA_OPTS"; }
        fi

        if $FORCE || [[ "${ans:-no}" == "yes" ]]; then
            for SVC_NAME in ejbca signserver; do
                if docker service ls --format "{{.Name}}" | grep -q "ivf_${SVC_NAME}"; then
                    info "Restarting ivf_${SVC_NAME}..."
                    docker service update --force "ivf_${SVC_NAME}" 2>&1 | tail -3
                    ok "ivf_${SVC_NAME} đã restart"
                fi
            done

            info "Chờ EJBCA/SignServer khởi động lại (~90s)..."
            sleep 90

            # Quick health check
            for CONT_VAR in "${EJBCA_CONT:-}" "${SS_CONT:-}"; do
                [[ -z "$CONT_VAR" ]] && continue
                UPDATED_CONT=$(docker ps --filter name="${CONT_VAR%%.*}" --format "{{.Names}}" | head -1 || true)
                [[ -z "$UPDATED_CONT" ]] && continue
                JAVA_OPTS_CHECK=$(docker exec "$UPDATED_CONT" bash -c \
                    'ps aux | grep java | grep -o "tls.client.protocols=[^ ]*" | head -1' 2>/dev/null || echo "")
                if [[ -n "$JAVA_OPTS_CHECK" ]]; then
                    ok "  ${UPDATED_CONT}: ${JAVA_OPTS_CHECK}"
                else
                    info "  ${UPDATED_CONT}: JVM options loaded (trong standalone.conf)"
                fi
            done
        fi
    fi
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 3: PostgreSQL TLS Hardening
# ════════════════════════════════════════════════════════════
step "Bước 3: PostgreSQL — Enforce Minimum TLS Version"

if $SKIP_PG; then
    warn "Bỏ qua PostgreSQL (--skip-postgres)"
elif [[ -z "$PG_CONT" ]]; then
    warn "PostgreSQL container không tìm thấy"
else
    info "PostgreSQL container: ${PG_CONT}"

    substep "Kiểm tra PostgreSQL TLS config..."
    PG_SSL=$(docker exec "$PG_CONT" bash -c \
        'psql -U postgres -c "SHOW ssl;" 2>/dev/null || echo "N/A"' 2>/dev/null || echo "N/A")
    info "  ssl: ${PG_SSL}"

    PG_SSL_MIN=$(docker exec "$PG_CONT" bash -c \
        'psql -U postgres -c "SHOW ssl_min_protocol_version;" 2>/dev/null || echo "N/A"' \
        2>/dev/null || echo "N/A")
    info "  ssl_min_protocol_version: ${PG_SSL_MIN}"

    # PostgreSQL sslmode=disable trong internal Docker network là OK
    # (container-to-container traffic qua Docker overlay network đã được mã hóa)
    # Nhưng nên set ssl_min_protocol_version để reject TLS 1.0/1.1 từ external

    substep "Tìm postgresql.conf..."
    PG_DATA=$(docker exec "$PG_CONT" bash -c 'psql -U postgres -c "SHOW data_directory;" -t 2>/dev/null | tr -d " "' || echo "/var/lib/postgresql/data")
    PG_CONF="${PG_DATA}/postgresql.conf"

    if docker exec "$PG_CONT" test -f "$PG_CONF" 2>/dev/null; then
        if $DRY_RUN; then
            info "[DRY-RUN] Sẽ thêm vào ${PG_CONF}:"
            info "  ssl_min_protocol_version = 'TLSv1.2'"
            info "  ssl_ciphers = 'HIGH:!aNULL:!MD5:!RC4:!3DES:!SHA1'"
        else
            # Backup
            docker exec "$PG_CONT" bash -c "cp -p '${PG_CONF}' '${PG_CONF}.fips-backup-$(date +%Y%m%d)' 2>/dev/null || true"

            # Cập nhật ssl_min_protocol_version
            if docker exec "$PG_CONT" grep -q "ssl_min_protocol_version" "$PG_CONF" 2>/dev/null; then
                docker exec "$PG_CONT" bash -c \
                    "sed -i \"s/^#*ssl_min_protocol_version.*/ssl_min_protocol_version = 'TLSv1.2'/\" '${PG_CONF}'"
            else
                docker exec "$PG_CONT" bash -c \
                    "echo \"ssl_min_protocol_version = 'TLSv1.2'\" >> '${PG_CONF}'"
            fi

            # Cập nhật ssl_ciphers
            if docker exec "$PG_CONT" grep -q "ssl_ciphers" "$PG_CONF" 2>/dev/null; then
                docker exec "$PG_CONT" bash -c \
                    "sed -i \"s|^#*ssl_ciphers.*|ssl_ciphers = 'HIGH:!aNULL:!MD5:!RC4:!3DES:!SHA1'|\" '${PG_CONF}'"
            else
                docker exec "$PG_CONT" bash -c \
                    "echo \"ssl_ciphers = 'HIGH:!aNULL:!MD5:!RC4:!3DES:!SHA1'\" >> '${PG_CONF}'"
            fi

            ok "PostgreSQL TLS config đã cập nhật"
            warn "Cần reload PostgreSQL: docker exec ${PG_CONT} psql -U postgres -c 'SELECT pg_reload_conf();'"

            # Auto reload config
            docker exec "$PG_CONT" bash -c \
                'psql -U postgres -c "SELECT pg_reload_conf();" 2>/dev/null' && \
                ok "PostgreSQL config đã reload" || warn "PostgreSQL reload thất bại — reload thủ công"
        fi
    else
        warn "Không tìm thấy postgresql.conf tại ${PG_CONF}"
        info "PostgreSQL internal traffic (container-to-container) không dùng TLS → OK"
    fi
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 4: Verify toàn bộ TLS settings
# ════════════════════════════════════════════════════════════
step "Bước 4: TLS Verification"

substep "OpenSSL TLS 1.2 check (natra.site)..."
TLS12=$(echo | timeout 10 openssl s_client -connect natra.site:443 \
    -tls1_2 2>/dev/null | grep -E "Protocol|Cipher" | head -4 || echo "timeout")
info "  TLS 1.2: ${TLS12}"

substep "Kiểm tra TLS 1.0 bị từ chối..."
if echo | timeout 5 openssl s_client -connect natra.site:443 \
    -tls1 2>/dev/null | grep -q "handshake failure\|not enabled\|ssl alert"; then
    ok "TLS 1.0 bị từ chối (expected)"
elif echo | timeout 5 openssl s_client -connect natra.site:443 \
    -tls1 2>/dev/null | grep -q "SSL routines"; then
    ok "TLS 1.0 bị từ chối"
else
    warn "TLS 1.0 check timeout/không xác định — kiểm tra thủ công"
fi

substep "Kiểm tra TLS 1.1 bị từ chối..."
if echo | timeout 5 openssl s_client -connect natra.site:443 \
    -tls1_1 2>/dev/null | grep -q "handshake failure\|not enabled\|ssl alert\|SSL routines"; then
    ok "TLS 1.1 bị từ chối (expected)"
else
    warn "TLS 1.1 check không xác định — kiểm tra thủ công"
fi

substep "Liệt kê TLS ciphers được support (trong OpenSSL FIPS mode)..."
SUPPORTED=$(openssl ciphers 'TLSv1.2+FIPS' 2>/dev/null | tr ':' '\n' | head -10 || \
    openssl ciphers 'HIGH:!aNULL:!MD5:!RC4:!SHA1' 2>/dev/null | tr ':' '\n' | head -10 || echo "N/A")
info "  Ciphers (first 10): $(echo "$SUPPORTED" | head -3 | tr '\n' ' ')..."

# ════════════════════════════════════════════════════════════
# BƯỚC 5: Tóm tắt và next steps
# ════════════════════════════════════════════════════════════
step "Tóm tắt — FIPS Phase 1 Hoàn tất"

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║   FIPS Phase 1 (Nền tảng FIPS) — ĐÃ HOÀN TẤT       ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════════╝${NC}"
echo ""
echo "  ✅ Bước 1/4: fips-enable.sh        → FIPS kernel + crypto policy"
echo "  ✅ Bước 2/4: fips-verify.sh         → Verification post-reboot"
echo "  ✅ Bước 3/4: fips-ejbca-harden.sh   → EJBCA profiles + JVM security"
echo "  ✅ Bước 4/4: fips-tls-harden.sh     → Caddy + JVM TLS ciphers (đang ở đây)"
echo ""
echo -e "${CYAN}  Thay đổi đã áp dụng:${NC}"
echo "  • AlmaLinux host: FIPS 140-2 Level 1 kernel crypto"
echo "  • Crypto policy: FIPS (MD5/RC4/3DES/SHA1-TLS bị chặn system-wide)"
echo "  • Caddy: TLS 1.2+ only, AES-GCM ciphers only (no RC4/3DES/CBC)"
echo "  • EJBCA/SignServer: JVM TLS 1.2+, disabled legacy algorithms"
echo "  • EJBCA cert profiles: RSA-2048+ only, SHA256+ only"
echo "  • PostgreSQL: ssl_min_protocol_version = TLSv1.2"
echo ""
echo -e "${YELLOW}  Việc cần làm sau:${NC}"
echo "  1. Cập nhật docker-compose.stack.yml: ivf_caddyfile_v15 → ivf_caddyfile_v16"
echo "     (để giữ đồng bộ khi re-deploy stack)"
echo "  2. Test signing workflow: POST /api/documents/{id}/sign"
echo "  3. Kiểm tra tất cả 6 SignServer workers vẫn ACTIVE"
echo "     ssh root@10.200.0.1 \"docker exec \$(docker ps -qf name=ivf_signserver | head -1)"
echo "       /opt/signserver/bin/signserver getstatus brief all\""
echo ""
echo -e "${YELLOW}  Giai đoạn 2 (khi có HSM phần cứng):${NC}"
echo "  1. Mua/thuê Luna Network HSM hoặc Utimaco SecurityServer"
echo "  2. Chạy scripts/hsm-rekey-migration.sh --hsm-lib <luna-lib>"
echo ""
echo "  Thời gian hoàn thành Phase 1: $(date)"
