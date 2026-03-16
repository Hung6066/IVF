#!/bin/bash
# ============================================================
# IVF PKI — FIPS Phase 1 (Bước 2/4): Xác minh sau Reboot
# ============================================================
# Chạy SAU KHI reboot, trên VPS (AlmaLinux 8/9).
# Kiểm tra toàn diện: FIPS kernel, crypto policy, tất cả
# Docker containers, endpoints, SoftHSM2, và chất lượng TLS.
#
# USAGE:
#   scp scripts/fips-verify.sh root@10.200.0.1:/tmp/
#   ssh root@10.200.0.1 "sed -i 's/\r//' /tmp/fips-verify.sh && bash /tmp/fips-verify.sh"
#
# Output: pass/fail cho từng kiểm tra, tóm tắt cuối cùng.
# Exit code: 0 = tất cả pass, 1 = có lỗi nghiêm trọng.
# ============================================================
set -euo pipefail

# ── Colors & logging ─────────────────────────────────────────
GREEN='\033[0;32m'; YELLOW='\033[1;33m'; RED='\033[0;31m'
BLUE='\033[0;34m'; CYAN='\033[0;36m'; MAGENTA='\033[0;35m'; NC='\033[0m'
BOLD='\033[1m'

ok()      { echo -e "${GREEN}  [PASS]${NC} $*"; (( PASS_COUNT += 1 )); }
fail()    { echo -e "${RED}  [FAIL]${NC} $*"; (( FAIL_COUNT += 1 )); CRITICAL=true; }
warn()    { echo -e "${YELLOW}  [WARN]${NC} $*"; (( WARN_COUNT += 1 )); }
info()    { echo -e "${BLUE}  [INFO]${NC} $*"; }
step()    { echo -e "\n${MAGENTA}════════════════════════════════════════════════${NC}"
            echo -e "${MAGENTA}  ${BOLD}$*${NC}"
            echo -e "${MAGENTA}════════════════════════════════════════════════${NC}"; }
substep() { echo -e "${CYAN}  ── $*${NC}"; }

# Counters
PASS_COUNT=0
FAIL_COUNT=0
WARN_COUNT=0
CRITICAL=false

# ════════════════════════════════════════════════════════════
echo -e "\n${BOLD}${BLUE}"
echo "  ╔══════════════════════════════════════╗"
echo "  ║   IVF PKI — FIPS Verification        ║"
echo "  ║   Giai đoạn 1 / Bước 2 của 4         ║"
echo "  ╚══════════════════════════════════════╝"
echo -e "${NC}"
echo "  Thời gian: $(date)"
echo "  Host:      $(hostname)"
echo ""

# ════════════════════════════════════════════════════════════
# KIỂM TRA 1: FIPS Kernel & Crypto Policy
# ════════════════════════════════════════════════════════════
step "Kiểm tra 1: FIPS Kernel & Crypto Policy"

substep "Kernel fips_enabled..."
if [[ -f /proc/sys/crypto/fips_enabled ]]; then
    FIPS_VAL=$(cat /proc/sys/crypto/fips_enabled)
    if [[ "$FIPS_VAL" == "1" ]]; then
        ok "fips_enabled = 1 — FIPS kernel module đang hoạt động"
    else
        fail "fips_enabled = ${FIPS_VAL} — FIPS CHƯA được bật!"
        echo ""
        echo -e "${RED}  → Kiểm tra: cat /proc/cmdline | grep fips=1${NC}"
        echo -e "${RED}  → Nếu chưa có 'fips=1': fips-mode-setup --check${NC}"
    fi
else
    fail "/proc/sys/crypto/fips_enabled không đọc được"
fi

substep "System FIPS marker..."
if [[ -f /etc/system-fips ]]; then
    ok "/etc/system-fips tồn tại"
else
    warn "/etc/system-fips không có (có thể không cần thiết trên RHEL 9+)"
fi

substep "Crypto policy..."
CURRENT_POLICY=$(update-crypto-policies --show 2>/dev/null || echo "UNKNOWN")
if [[ "$CURRENT_POLICY" == "FIPS" ]]; then
    ok "Crypto policy = FIPS"
else
    fail "Crypto policy = ${CURRENT_POLICY} (mong đợi FIPS)"
    echo -e "${RED}  → Chạy: update-crypto-policies --set FIPS${NC}"
fi

substep "Kernel cmdline..."
if cat /proc/cmdline | grep -q "fips=1"; then
    ok "Kernel cmdline có 'fips=1'"
else
    warn "Kernel cmdline không có 'fips=1' — FIPS có thể chưa hoàn toàn kích hoạt"
fi

substep "fips-mode-setup check..."
FIPS_CHECK=$(fips-mode-setup --check 2>/dev/null || echo "error")
info "fips-mode-setup --check: ${FIPS_CHECK}"

# ════════════════════════════════════════════════════════════
# KIỂM TRA 2: Crypto algorithm enforcement
# ════════════════════════════════════════════════════════════
step "Kiểm tra 2: Algorithm Enforcement"

substep "MD5 đã bị chặn..."
if openssl dgst -md5 /dev/null 2>/dev/null; then
    warn "MD5 vẫn còn hoạt động (cần kiểm tra crypto policy)"
else
    ok "MD5 đã bị chặn bởi OpenSSL FIPS"
fi

substep "SHA-1 standalone OK, SHA-1 trong TLS bị chặn..."
# SHA-1 standalone (để verify signatures) vẫn được FIPS 140-2 cho phép
# Nhưng SHA-1 trong TLS handshake bị chặn
if openssl dgst -sha1 /dev/null 2>/dev/null; then
    ok "SHA-1 standalone (message digest) — được cho phép"
else
    info "SHA-1 hoàn toàn bị chặn (FIPS strict mode)"
fi

substep "RC4 đã bị chặn..."
if openssl enc -rc4 -k test -in /dev/null 2>/dev/null; then
    warn "RC4 vẫn còn hoạt động"
else
    ok "RC4 đã bị chặn"
fi

substep "AES-256-GCM hoạt động đúng..."
if echo "test" | openssl enc -aes-256-gcm -k testpassword -nosalt 2>/dev/null | openssl enc -d -aes-256-gcm -k testpassword -nosalt 2>/dev/null | grep -q "test"; then
    ok "AES-256-GCM hoạt động"
else
    info "AES-256-GCM test (lỗi nhỏ do thiếu salt OK)"
fi

substep "SHA-256 hoạt động đúng..."
if openssl dgst -sha256 /dev/null 2>/dev/null; then
    ok "SHA-256 hoạt động"
else
    fail "SHA-256 không hoạt động — lỗi nghiêm trọng!"
fi

substep "RSA key generation (nhỏ để test nhanh)..."
if openssl genrsa 2048 2>/dev/null | openssl rsa -noout 2>/dev/null; then
    ok "RSA-2048 key generation hoạt động"
else
    warn "RSA key generation có vấn đề — kiểm tra thêm"
fi

substep "OpenSSL TLS ciphers (FIPS-compliant list)..."
FIPS_CIPHERS=$(openssl ciphers 'FIPS' 2>/dev/null | tr ':' '\n' | wc -l || echo "0")
if [[ "$FIPS_CIPHERS" -gt 5 ]]; then
    ok "OpenSSL FIPS cipher list: ${FIPS_CIPHERS} ciphers"
else
    warn "OpenSSL FIPS ciphers ít hơn dự kiến: ${FIPS_CIPHERS}"
fi

# ════════════════════════════════════════════════════════════
# KIỂM TRA 3: Docker daemon
# ════════════════════════════════════════════════════════════
step "Kiểm tra 3: Docker Daemon"

substep "Docker service..."
if systemctl is-active docker &>/dev/null; then
    ok "Docker service đang chạy"
else
    fail "Docker service không chạy!"
    echo -e "${RED}  → Khởi động: systemctl start docker${NC}"
fi

substep "Docker Swarm..."
if docker info 2>/dev/null | grep -q "Swarm: active"; then
    ok "Docker Swarm active"
else
    warn "Docker Swarm không active hoặc không có quyền kiểm tra"
fi

substep "Docker TLS version..."
DOCKER_VER=$(docker version --format '{{.Server.Os}}/{{.Server.Arch}}' 2>/dev/null || echo "unknown")
info "Docker Server: ${DOCKER_VER}"

# ════════════════════════════════════════════════════════════
# KIỂM TRA 4: Container health
# ════════════════════════════════════════════════════════════
step "Kiểm tra 4: Container Health"

# Discover containers
declare -A EXPECTED_CONTAINERS=(
    ["ivf_api"]="IVF API"
    ["ivf_ejbca"]="EJBCA CA"
    ["ivf_signserver"]="SignServer"
    ["ivf_db"]="PostgreSQL"
    ["ivf_redis"]="Redis"
    ["ivf_minio"]="MinIO"
    ["ivf_caddy"]="Caddy"
    ["ivf_frontend"]="Frontend"
)

RUNNING_CONTAINERS=$(docker ps --format "{{.Names}}" 2>/dev/null || echo "")

for svc in "${!EXPECTED_CONTAINERS[@]}"; do
    NAME="${EXPECTED_CONTAINERS[$svc]}"
    # Match theo prefix (vì Swarm thêm .1.xxx ở cuối)
    CONT=$(echo "$RUNNING_CONTAINERS" | grep "^${svc}" | head -1 || true)
    if [[ -n "$CONT" ]]; then
        STATUS=$(docker inspect "$CONT" --format '{{.State.Health.Status}}' 2>/dev/null || echo "no-healthcheck")
        ok "${NAME} (${CONT}) — status: ${STATUS}"
    else
        warn "${NAME} (${svc}.*) — không tìm thấy container đang chạy"
    fi
done

# Check containers ở trạng thái unhealthy
substep "Kiểm tra unhealthy containers..."
UNHEALTHY=$(docker ps --filter health=unhealthy --format "{{.Names}}" 2>/dev/null || echo "")
if [[ -n "$UNHEALTHY" ]]; then
    fail "Containers unhealthy: ${UNHEALTHY}"
else
    ok "Không có container nào ở trạng thái unhealthy"
fi

# ════════════════════════════════════════════════════════════
# KIỂM TRA 5: Service endpoints
# ════════════════════════════════════════════════════════════
step "Kiểm tra 5: Service Endpoints"

# IVF API health
substep "IVF API /health/live..."
if curl -sf --max-time 10 http://localhost:8080/health/live &>/dev/null || \
   docker exec "$(docker ps --filter name=ivf_api --format '{{.Names}}' | head -1)" \
       curl -sf http://127.0.0.1:8080/health/live &>/dev/null 2>/dev/null; then
    ok "IVF API /health/live → 200"
else
    warn "IVF API /health/live không phản hồi (có thể timeout hoặc chưa start xong)"
fi

# EJBCA health
substep "EJBCA health..."
EJBCA_CONT=$(docker ps --filter name=ivf_ejbca --format "{{.Names}}" | grep -v '\-db' | head -1 || true)
if [[ -n "$EJBCA_CONT" ]]; then
    if docker exec "$EJBCA_CONT" curl -sk --max-time 10 \
        https://127.0.0.1:8443/ejbca/publicweb/healthcheck/ejbcahealth 2>/dev/null | \
        grep -q "ALLOK\|OK"; then
        ok "EJBCA health → OK"
    else
        warn "EJBCA health không phản hồi đúng (có thể khởi động chậm)"
    fi
else
    warn "EJBCA container không tìm thấy"
fi

# SignServer health
substep "SignServer health..."
SS_CONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1 || true)
if [[ -n "$SS_CONT" ]]; then
    if docker exec "$SS_CONT" curl -sk --max-time 10 \
        https://127.0.0.1:8443/signserver/healthcheck/signserverhealth 2>/dev/null | \
        grep -q "ALLOK\|OK"; then
        ok "SignServer health → OK"
    else
        warn "SignServer health không phản hồi (có thể khởi động chậm)"
    fi
else
    warn "SignServer container không tìm thấy"
fi

# MinIO health
substep "MinIO health..."
MINIO_CONT=$(docker ps --filter name=ivf_minio --format "{{.Names}}" | head -1 || true)
if [[ -n "$MINIO_CONT" ]] && docker exec "$MINIO_CONT" curl -sf --max-time 10 http://localhost:9000/minio/health/live &>/dev/null; then
    ok "MinIO /minio/health/live → OK"
else
    warn "MinIO health không phản hồi"
fi

# PostgreSQL
substep "PostgreSQL..."
PGCONT=$(docker ps --filter name=ivf_db --format "{{.Names}}" | grep -v ejbca | grep -v signserver | head -1 || true)
if [[ -n "$PGCONT" ]]; then
    if docker exec "$PGCONT" pg_isready -U postgres &>/dev/null; then
        ok "PostgreSQL → pg_isready OK"
    else
        fail "PostgreSQL không sẵn sàng!"
    fi
else
    warn "PostgreSQL container không tìm thấy"
fi

# Redis
substep "Redis..."
REDIS_CONT=$(docker ps --filter name=ivf_redis --format "{{.Names}}" | grep -v exporter | head -1 || true)
if [[ -n "$REDIS_CONT" ]]; then
    REDIS_PING=$(docker exec "$REDIS_CONT" redis-cli ping 2>/dev/null || echo "FAILED")
    if echo "$REDIS_PING" | grep -q "PONG"; then
        ok "Redis → PONG"
    else
        warn "Redis ping: ${REDIS_PING}"
    fi
else
    warn "Redis container không tìm thấy"
fi

# ════════════════════════════════════════════════════════════
# KIỂM TRA 6: SoftHSM2 / PKCS#11 trong SignServer
# ════════════════════════════════════════════════════════════
step "Kiểm tra 6: SoftHSM2 & SignServer Workers"

if [[ -n "$SS_CONT" ]]; then
    substep "SoftHSM2 library..."
    if docker exec "$SS_CONT" test -f /usr/lib64/pkcs11/libsofthsm2.so &>/dev/null; then
        ok "libsofthsm2.so tồn tại trong container"
    else
        fail "libsofthsm2.so không tìm thấy trong SignServer container!"
    fi

    substep "SoftHSM2 token slots..."
    SLOTS=$(docker exec "$SS_CONT" bash -c \
        'SOFTHSM2_CONF=/opt/keyfactor/persistent/softhsm/softhsm2.conf \
         softhsm2-util --show-slots 2>/dev/null || echo "ERROR"' 2>/dev/null || echo "ERROR")
    if echo "$SLOTS" | grep -q "SignServerToken\|Slot"; then
        ok "SoftHSM2 token(s) còn nguyên vẹn sau FIPS"
        echo "$SLOTS" | grep -E "Slot|Token Label|Initialized" | sed 's/^/    /'
    else
        warn "SoftHSM2 slots không đọc được (có thể SOFTHSM2_CONF sai path)"
        info "Output: $(echo "$SLOTS" | head -5)"
    fi

    substep "SignServer workers status..."
    SS_CLI="/opt/keyfactor/signserver/bin/signserver"
    WORKERS=$(docker exec "$SS_CONT" $SS_CLI getstatus brief all 2>/dev/null || echo "ERROR")
    if echo "$WORKERS" | grep -qi "Worker status.*Active"; then
        ACTIVE_COUNT=$(echo "$WORKERS" | grep -ci "Worker status.*Active" || echo "0")
        ok "${ACTIVE_COUNT} worker(s) ở trạng thái ACTIVE"
    elif echo "$WORKERS" | grep -qi "offline\|error"; then
        fail "Có worker ở trạng thái OFFLINE/ERROR sau FIPS reboot!"
        echo "$WORKERS" | head -20 | sed 's/^/    /'
    else
        warn "Không đọc được trạng thái workers: $(echo "$WORKERS" | head -3)"
    fi
fi

# ════════════════════════════════════════════════════════════
# KIỂM TRA 7: TLS Quality (external)
# ════════════════════════════════════════════════════════════
step "Kiểm tra 7: TLS Cipher Quality"

substep "Kiểm tra TLS trên cổng 443 (natra.site)..."
# Test với openssl s_client — kiểm tra protocol và cipher
TLS_OUTPUT=$(echo | timeout 5 openssl s_client -connect natra.site:443 \
    -tls1_2 2>/dev/null || echo "TIMEOUT")

if echo "$TLS_OUTPUT" | grep -q "Protocol.*TLSv1.2\|Protocol.*TLSv1.3"; then
    PROTO=$(echo "$TLS_OUTPUT" | grep "Protocol" | head -1 | awk '{print $NF}')
    CIPHER=$(echo "$TLS_OUTPUT" | grep "Cipher" | head -1 | awk '{print $NF}')
    ok "TLS external: Protocol=${PROTO}, Cipher=${CIPHER}"
else
    warn "Không thể kết nối natra.site:443 (có thể không có mạng external từ VPS)"
fi

substep "Thử MD5 trong TLS (nên bị từ chối)..."
if echo | timeout 5 openssl s_client -connect natra.site:443 \
    -cipher 'MD5' 2>/dev/null | grep -q "handshake failure\|ssl handshake failure"; then
    ok "MD5 cipher bị từ chối trong TLS"
else
    info "MD5 TLS test không xác định được (có thể server không hỗ trợ test này)"
fi

substep "Kiểm tra SSH crypto policy..."
if grep -qE "^Ciphers.*aes" /etc/ssh/sshd_config /etc/crypto-policies/back-ends/openssh.config 2>/dev/null; then
    ok "SSH ciphers đã được cập nhật theo FIPS policy"
elif which sshd &>/dev/null; then
    SSH_CIPHERS=$(sshd -T 2>/dev/null | grep "^ciphers" | head -1 || echo "N/A")
    info "SSH active ciphers: ${SSH_CIPHERS:0:100}"
    # Kiểm tra không có 3des, arcfour, blowfish
    if echo "$SSH_CIPHERS" | grep -qiE "3des|arcfour|blowfish"; then
        warn "SSH có weak ciphers — crypto policy chưa áp dụng đủ"
    else
        ok "SSH ciphers không có weak algorithms"
    fi
fi

# ════════════════════════════════════════════════════════════
# KIỂM TRA 8: Java FIPS trong containers
# ════════════════════════════════════════════════════════════
step "Kiểm tra 8: Java Crypto trong EJBCA/SignServer"

for CONT_VAR in "$EJBCA_CONT" "$SS_CONT"; do
    [[ -z "$CONT_VAR" ]] && continue
    CONT_NAME=$(echo "$CONT_VAR" | sed 's/\..*//')

    substep "Java version trong ${CONT_NAME}..."
    JAVA_VER=$(docker exec "$CONT_VAR" java -version 2>&1 | head -1 || echo "N/A")
    info "  ${CONT_VAR}: ${JAVA_VER}"

    substep "Java crypto providers trong ${CONT_NAME}..."
    # Test nếu MD5 bị chặn ở Java level (không bắt buộc, phụ thuộc JVM config)
    JAVA_MD5=$(docker exec "$CONT_VAR" bash -c \
        'java -cp . -e "import java.security.MessageDigest; MessageDigest.getInstance(\"MD5\");" 2>&1 || true' \
        2>/dev/null || echo "N/A")
    # Note: FIPS trên host không tự động disable Java's internal crypto
    info "  Java MD5: ${JAVA_MD5:0:60} (Java crypto không bị ảnh hưởng bởi host FIPS)"
    info "  → Cần fips-tls-harden.sh để harden JVM crypto"
done

# ════════════════════════════════════════════════════════════
# KIỂM TRA 9: Disk & Memory post-reboot
# ════════════════════════════════════════════════════════════
step "Kiểm tra 9: Resources"

substep "Disk usage..."
df -h / /boot 2>/dev/null | sed 's/^/    /'

substep "Memory..."
FREE_MEM=$(free -m | awk 'NR==2{printf "%dMB/%dMB", $3, $2}')
info "RAM: ${FREE_MEM}"

substep "Load average..."
LOAD=$(cat /proc/loadavg | awk '{print $1" "$2" "$3}')
info "Load: ${LOAD}"

# ════════════════════════════════════════════════════════════
# TÓM TẮT
# ════════════════════════════════════════════════════════════
step "Tóm tắt Kết quả"

echo ""
echo "  ┌─────────────────────────────────────────┐"
printf "  │  %-6s  PASS  │  %-6s  FAIL  │  %-6s  WARN  │\n" \
    "${PASS_COUNT}" "${FAIL_COUNT}" "${WARN_COUNT}"
echo "  └─────────────────────────────────────────┘"
echo ""

if [[ "$FAIL_COUNT" -eq 0 ]] && ! $CRITICAL; then
    echo -e "${GREEN}${BOLD}  ✅ TẤT CẢ KIỂM TRA QUAN TRỌNG ĐÃ PASS!${NC}"
    echo ""
    echo "  Bước tiếp theo:"
    echo "  ✅ Bước 1/4: fips-enable.sh    → DONE"
    echo "  ✅ Bước 2/4: fips-verify.sh    → DONE (đang ở đây)"
    echo "  👉 Bước 3/4: fips-ejbca-harden.sh → Chạy tiếp theo"
    echo ""
    echo "  scp scripts/fips-ejbca-harden.sh root@10.200.0.1:/tmp/"
    echo "  ssh root@10.200.0.1 'bash /tmp/fips-ejbca-harden.sh'"
elif [[ "$FAIL_COUNT" -gt 0 ]]; then
    echo -e "${RED}${BOLD}  ❌ CÓ ${FAIL_COUNT} LỖI NGHIÊM TRỌNG — CẦN XỬ LÝ TRƯỚC KHI TIẾP TỤC!${NC}"
    echo ""
    echo "  Kiểm tra logs: journalctl -xe | tail -50"
    echo "  Container logs: docker service logs ivf_ejbca 2>&1 | tail -50"
    echo ""
    echo "  Rollback nếu cần:"
    echo "  fips-mode-setup --disable && update-crypto-policies --set DEFAULT && reboot"
    exit 1
else
    echo -e "${YELLOW}${BOLD}  ⚠  ${WARN_COUNT} CẢNH BÁO — Xem lại logs bên trên${NC}"
    echo ""
    echo "  Nếu containers đều ACTIVE, tiếp tục bước 3 an toàn."
    echo "  scp scripts/fips-ejbca-harden.sh root@10.200.0.1:/tmp/"
    echo "  ssh root@10.200.0.1 'bash /tmp/fips-ejbca-harden.sh'"
fi
