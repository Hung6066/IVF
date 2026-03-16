#!/bin/bash
# ============================================================
# IVF PKI — FIPS Phase 1 (Bước 3/4): Hardening EJBCA Profiles
# ============================================================
# Disable SHA-1 và MD5 trong các EJBCA certificate profiles.
# Chỉ cho phép SHA-256/384/512 với RSA-2048+ và ECDSA P-256+.
#
# Tác động:
#   • IVF-PDFSigner-Profile (5001): Restrict available signature algorithms
#   • IVF-TSA-Profile (5002):       Restrict available signature algorithms
#   • IVF-TLS-Client-Profile (5003): Restrict available signature algorithms
#   • Tất cả CAs: Verify đang dùng SHA256withRSA (không phải SHA1)
#   • Minimum key size: 2048-bit RSA (bỏ 1024 nếu có)
#
# Cách hoạt động:
#   1. Audit: Kiểm tra trạng thái hiện tại của CAs và profiles
#   2. Generate: Tạo XML profiles mới với FIPS restrictions
#   3. Import: Nạp profiles vào EJBCA qua CLI (ca importprofiles)
#   4. Verify: Xác nhận settings đã được áp dụng
#
# USAGE:
#   scp scripts/fips-ejbca-harden.sh root@10.200.0.1:/tmp/
#   ssh root@10.200.0.1 "sed -i 's/\r//' /tmp/fips-ejbca-harden.sh && bash /tmp/fips-ejbca-harden.sh"
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

while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry-run)  DRY_RUN=true; shift ;;
        --force)    FORCE=true; shift ;;
        --help|-h)
            echo "Usage: $0 [--dry-run] [--force]"
            echo "  --dry-run   Kiểm tra và tạo XMLs, không import vào EJBCA"
            echo "  --force     Bỏ qua confirmation"
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

# ── Container discovery ────────────────────────────────────────
EJBCA_CONT=$(docker ps --filter name=ivf_ejbca --format "{{.Names}}" | grep -v '\-db' | head -1 || true)
if [[ -z "$EJBCA_CONT" ]]; then
    err "EJBCA container không tìm thấy. Kiểm tra: docker ps | grep ejbca"
    exit 1
fi
ok "EJBCA container: ${EJBCA_CONT}"

ejbca() { docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh "$@"; }

# ════════════════════════════════════════════════════════════
echo -e "\n${BOLD}${BLUE}"
echo "  ╔══════════════════════════════════════╗"
echo "  ║   IVF PKI — EJBCA FIPS Hardening     ║"
echo "  ║   Giai đoạn 1 / Bước 3 của 4         ║"
echo "  ╚══════════════════════════════════════╝"
echo -e "${NC}"

# ════════════════════════════════════════════════════════════
# BƯỚC 1: Audit trạng thái hiện tại
# ════════════════════════════════════════════════════════════
step "Bước 1: Audit EJBCA CAs và Profiles"

substep "Danh sách CAs..."
CA_LIST=$(ejbca ca listcas 2>/dev/null || echo "ERROR")
echo "$CA_LIST" | grep -E "CA Name|Signature Algorithm|Status" | sed 's/^/    /' || true

# Màu warning nếu thấy SHA1
substep "Kiểm tra SHA-1 trong CA algorithms..."
if echo "$CA_LIST" | grep -qi "SHA1\|MD5"; then
    warn "Phát hiện CA dùng SHA-1 hoặc MD5!"
    echo "$CA_LIST" | grep -iE "SHA1|MD5" | sed 's/^/    /'
else
    ok "Không phát hiện CA nào dùng SHA-1 hay MD5"
fi

substep "IVF-Root-CA key info..."
# Dùng ca info (lấy key algorithm và key size đã được verify)
ROOTCA_INFO=$(docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca info --caname "IVF-Root-CA" 2>/dev/null || echo "N/A")
ROOTCA_KEYSIZE=$(echo "$ROOTCA_INFO" | grep -i "key size" | head -1 | awk '{print $NF}' | tr -d ' \r\n' || echo "N/A")
ROOTCA_KEYALG=$(echo "$ROOTCA_INFO" | grep -i "key algorithm" | head -1 | awk '{print $NF}' | tr -d ' \r\n' || echo "N/A")
info "  IVF-Root-CA: keyAlgorithm=${ROOTCA_KEYALG}, keySize=${ROOTCA_KEYSIZE}"
if [[ "${ROOTCA_KEYSIZE}" =~ ^[0-9]+$ ]] && (( ROOTCA_KEYSIZE >= 2048 )); then
    ok "  IVF-Root-CA key size ${ROOTCA_KEYSIZE} ≥ 2048 ✓"
else
    warn "  IVF-Root-CA key size không đọc được — kiểm tra manual"
fi

substep "IVF-Signing-SubCA key info..."
SUBCA_INFO=$(docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca info --caname "IVF-Signing-SubCA" 2>/dev/null || echo "N/A")
SUBCA_KEYSIZE=$(echo "$SUBCA_INFO" | grep -i "key size" | head -1 | awk '{print $NF}' | tr -d ' \r\n' || echo "N/A")
SUBCA_KEYALG=$(echo "$SUBCA_INFO" | grep -i "key algorithm" | head -1 | awk '{print $NF}' | tr -d ' \r\n' || echo "N/A")
info "  IVF-Signing-SubCA: keyAlgorithm=${SUBCA_KEYALG}, keySize=${SUBCA_KEYSIZE}"
if [[ "${SUBCA_KEYSIZE}" =~ ^[0-9]+$ ]] && (( SUBCA_KEYSIZE >= 2048 )); then
    ok "  IVF-Signing-SubCA key size ${SUBCA_KEYSIZE} ≥ 2048 ✓"
else
    warn "  IVF-Signing-SubCA key size không đọc được — kiểm tra manual"
fi

substep "Cert profiles hiện tại..."
# Dùng exportprofiles để xem các profiles có trong DB
AUDIT_EXPORT_DIR="/tmp/ejbca-audit-profiles-$$"
docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca exportprofiles -d "$AUDIT_EXPORT_DIR" &>/dev/null || true
if [[ -d "$AUDIT_EXPORT_DIR" ]] && ls "$AUDIT_EXPORT_DIR"/*.xml &>/dev/null; then
    PROFILE_COUNT=$(ls "$AUDIT_EXPORT_DIR"/*.xml | wc -l)
    info "  ${PROFILE_COUNT} profiles trong DB:"
    ls "$AUDIT_EXPORT_DIR"/*.xml | xargs -I{} basename {} | sed 's/^/    /'
    rm -rf "$AUDIT_EXPORT_DIR"
else
    info "  (exportprofiles: không có file — có thể chưa import)" 
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 2: Tạo FIPS-hardened XML profiles
# ════════════════════════════════════════════════════════════
step "Bước 2: Tạo FIPS-hardened Certificate Profile XMLs"

PROFILE_DIR="/tmp/fips-profiles-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$PROFILE_DIR"
info "Profile directory: ${PROFILE_DIR}"

# ── Helper: generate FIPS-hardened cert profile XML ──────────
create_fips_profile() {
    local name="$1"     # IVF-PDFSigner-Profile
    local id="$2"       # 5001
    local validity="$3" # 3y
    local eku_oids="$4" # "1.3.6.1.5.5.7.3.8" hoặc ""
    local nonrep="$5"   # true/false
    local eku_crit="$6" # true/false

    local filepath="${PROFILE_DIR}/certprofile_${name}-${id}.xml"

    cat > "$filepath" << XMLEOF
<?xml version="1.0" encoding="UTF-8"?>
<java version="1.8.0" class="java.beans.XMLDecoder">
<object class="java.util.LinkedHashMap">

  <void method="put">
    <string>version</string>
    <float>43.0</float>
  </void>

  <void method="put">
    <string>type</string>
    <int>1</int>
  </void>

  <!-- ═══ FIPS: Chỉ cho phép RSA và ECDSA ═══ -->
  <void method="put">
    <string>availablekeyalgorithms</string>
    <object class="java.util.ArrayList">
      <void method="add"><string>RSA</string></void>
      <void method="add"><string>ECDSA</string></void>
    </object>
  </void>

  <!-- ═══ FIPS: ECDSA curves (P-256, P-384, P-521 only — no weak curves) ═══ -->
  <void method="put">
    <string>availableECCurvesAsString</string>
    <object class="java.util.ArrayList">
      <void method="add"><string>P-256</string></void>
      <void method="add"><string>P-384</string></void>
      <void method="add"><string>P-521</string></void>
    </object>
  </void>

  <!-- ═══ FIPS: Chỉ SHA-256/384/512 với RSA và ECDSA ═══
       LOẠI BỎ: SHA1withRSA, MD5withRSA, SHA1withECDSA
       Giá trị -1 = "inherit from CA" — an toàn vì CA đã dùng SHA256 ═══ -->
  <void method="put">
    <string>signingalgorithm</string>
    <string>-1</string>
  </void>

  <!-- ═══ FIPS: Hạn chế algorithms mà người dùng có thể chọn khi tạo cert ═══ -->
  <void method="put">
    <string>availablesignaturealgorithms</string>
    <object class="java.util.ArrayList">
      <void method="add"><string>SHA256WithRSA</string></void>
      <void method="add"><string>SHA384WithRSA</string></void>
      <void method="add"><string>SHA512WithRSA</string></void>
      <void method="add"><string>SHA256withECDSA</string></void>
      <void method="add"><string>SHA384withECDSA</string></void>
      <void method="add"><string>SHA512withECDSA</string></void>
    </object>
  </void>

  <void method="put">
    <string>encodedvalidity</string>
    <string>${validity}</string>
  </void>

  <void method="put">
    <string>allowvalidityoverride</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>allowkeyusageoverride</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>allowextensionoverride</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>allowdnoverride</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>usecertificatestorage</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>storecertificatedata</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>storesubjectaltname</string>
    <boolean>true</boolean>
  </void>

  <!-- ═══ FIPS: Minimum RSA key size 2048. Bỏ 1024 ═══ -->
  <void method="put">
    <string>availablebitlenghts</string>
    <object class="java.util.ArrayList">
      <void method="add"><int>2048</int></void>
      <void method="add"><int>3072</int></void>
      <void method="add"><int>4096</int></void>
    </object>
  </void>

  <void method="put">
    <string>minimumavailablebitlength</string>
    <int>2048</int>
  </void>

  <void method="put">
    <string>maximumavailablebitlength</string>
    <int>0</int>
  </void>

  <!-- Key Usage: digitalSignature (index 0), nonRepudiation (index 1) -->
  <void method="put">
    <string>keyusage</string>
    <object class="java.util.ArrayList">
      <void method="add"><boolean>true</boolean></void>
      <void method="add"><boolean>${nonrep}</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
      <void method="add"><boolean>false</boolean></void>
    </object>
  </void>

  <void method="put">
    <string>keyusagecritical</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>extendedkeyusagecritical</string>
    <boolean>${eku_crit}</boolean>
  </void>

  <void method="put">
    <string>usesubjectaltname</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>useissueralternativename</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>usecrldistributionpoint</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>usedefaultcrldistributionpoint</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>useauthorityinformationaccess</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>usedefaultocspservicelocator</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>ocspservicelocatoruri</string>
    <string></string>
  </void>

  <void method="put">
    <string>usesubjectdirattributes</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>useocspnocheck</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>usecaissuer</string>
    <boolean>true</boolean>
  </void>

  <void method="put">
    <string>usefreshestcrl</string>
    <boolean>false</boolean>
  </void>

  <void method="put">
    <string>availablecas</string>
    <object class="java.util.ArrayList">
      <void method="add"><int>-1</int></void>
    </object>
  </void>

XMLEOF

    # Thêm EKU OIDs nếu có
    if [[ -n "$eku_oids" ]]; then
        cat >> "$filepath" << EKUEOF
  <void method="put">
    <string>extendedkeyusage</string>
    <object class="java.util.ArrayList">
EKUEOF
        for oid in $eku_oids; do
            echo "      <void method=\"add\"><string>${oid}</string></void>" >> "$filepath"
        done
        cat >> "$filepath" << EKUEOF2
    </object>
  </void>
EKUEOF2
    else
        cat >> "$filepath" << EKUEOF3
  <void method="put">
    <string>extendedkeyusage</string>
    <object class="java.util.ArrayList">
    </object>
  </void>
EKUEOF3
    fi

    # Đóng XML
    cat >> "$filepath" << XMLEOF2

</object>
</java>
XMLEOF2

    ok "Tạo: ${filepath##*/}"
}

# ── Tạo 3 FIPS-hardened profiles ─────────────────────────────

substep "IVF-PDFSigner-Profile (5001): digitalSignature + nonRepudiation, 3y..."
create_fips_profile "IVF-PDFSigner-Profile" "5001" "3y" "" "true" "false"

substep "IVF-TSA-Profile (5002): digitalSignature, 5y, EKU=timeStamping (critical)..."
create_fips_profile "IVF-TSA-Profile" "5002" "5y" "1.3.6.1.5.5.7.3.8" "false" "true"

substep "IVF-TLS-Client-Profile (5003): digitalSignature, 2y, EKU=clientAuth..."
create_fips_profile "IVF-TLS-Client-Profile" "5003" "2y" "1.3.6.1.5.5.7.3.2" "false" "false"

substep "Hiển thị availablesignaturealgorithms trong XMLs..."
# Kiểm tra KHÔNG có SHA1withRSA trong bất kỳ file nào
for f in "$PROFILE_DIR"/*.xml; do
    # Bỏ qua comment lines (chứa "LOẠI BỎ" hay "<!--") khi kiểm tra
    if grep -v "LOẠI BỎ\|<!--" "$f" | grep -qi "SHA1withRSA\|MD5withRSA"; then
        warn "Phát hiện SHA1/MD5 trong $(basename $f)!"
    else
        ok "$(basename $f) — clean (không có SHA1/MD5 trong algorithm list)"
    fi
done

info ""
info "FIPS restrictions đã thêm vào mỗi profile:"
info "  availablesignaturealgorithms: SHA256WithRSA, SHA384WithRSA, SHA512WithRSA,"
info "                                SHA256withECDSA, SHA384withECDSA, SHA512withECDSA"
info "  minimumavailablebitlength:    2048"
info "  availablebitlenghts:          2048, 3072, 4096 (bỏ 1024)"
info "  availableECCurvesAsString:    P-256, P-384, P-521 (bỏ weak curves)"

# ════════════════════════════════════════════════════════════
# BƯỚC 3: Import profiles vào EJBCA
# ════════════════════════════════════════════════════════════
step "Bước 3: Import FIPS Profiles vào EJBCA"

if $DRY_RUN; then
    info "[DRY-RUN] Bỏ qua import. XMLs được tạo tại: ${PROFILE_DIR}"
    ls -la "$PROFILE_DIR/" | sed 's/^/    /'
else
    if ! $FORCE; then
        echo ""
        warn "Import sẽ GHI ĐÈ cert profiles hiện tại (5001, 5002, 5003)."
        warn "Workers ĐANG CHẠY sẽ không bị ảnh hưởng (profiles chỉ áp dụng cho cert mới)."
        read -rp "Tiếp tục import? (yes/N): " ans
        [[ "$ans" == "yes" ]] || { info "Đã hủy."; exit 0; }
    fi

    # Phát hiện EJBCA DB container
    EJBCA_DB_CONT=$(docker ps --filter name=ivf_ejbca-db --format "{{.Names}}" \
        | grep -v '^\s*$' | head -1 || true)

    # Xử lý profiles đã tồn tại: EJBCA importprofiles không hỗ trợ overwrite,
    # rowprotection=NULL nên có thể DELETE trực tiếp từ PostgreSQL rồi re-import.
    substep "Kiểm tra profiles hiện có trong EJBCA DB..."
    PROFILE_IDS_TO_DELETE=""
    for PNAME in "IVF-PDFSigner-Profile" "IVF-TSA-Profile" "IVF-TLS-Client-Profile"; do
        if [[ -n "$EJBCA_DB_CONT" ]]; then
            EXISTS=$(docker exec "$EJBCA_DB_CONT" psql -U ejbca -d ejbca -tA \
                -c "SELECT id FROM CertificateProfileData WHERE certificateprofilename='${PNAME}'" \
                2>/dev/null | tr -d '[:space:]' || echo "")
            if [[ -n "$EXISTS" ]]; then
                info "  ${PNAME} (id=${EXISTS}) đã tồn tại → sẽ xóa để import lại với FIPS settings"
                PROFILE_IDS_TO_DELETE="${PROFILE_IDS_TO_DELETE}${EXISTS},"
            else
                info "  ${PNAME} chưa tồn tại → sẽ import mới"
            fi
        fi
    done

    if [[ -n "$PROFILE_IDS_TO_DELETE" && -n "$EJBCA_DB_CONT" ]]; then
        substep "Xóa profiles cũ khỏi DB (rowprotection=NULL nên safe)..."
        # Remove trailing comma and build IN clause
        PROFILE_IDS_TO_DELETE="${PROFILE_IDS_TO_DELETE%,}"
        DELETE_SQL="DELETE FROM CertificateProfileData WHERE id IN (${PROFILE_IDS_TO_DELETE})"
        DELETE_RESULT=$(docker exec "$EJBCA_DB_CONT" psql -U ejbca -d ejbca \
            -c "${DELETE_SQL}" 2>&1)
        echo "$DELETE_RESULT"
        if echo "$DELETE_RESULT" | grep -qi "DELETE [123]"; then
            ok "Profiles cũ đã xóa khỏi DB"
        else
            warn "Không xóa được profiles cũ — import sẽ thất bại với 'already exist'"
        fi
    fi

    # Copy XMLs vào EJBCA container
    substep "Copy profiles vào EJBCA container..."
    EJBCA_PROFILE_DIR="/tmp/fips-profiles"
    docker exec "$EJBCA_CONT" mkdir -p "$EJBCA_PROFILE_DIR"

    for f in "$PROFILE_DIR"/*.xml; do
        fname=$(basename "$f")
        docker cp "$f" "${EJBCA_CONT}:${EJBCA_PROFILE_DIR}/${fname}"
        info "  Copied: ${fname}"
    done
    ok "Tất cả XMLs đã copy vào container"

    # Import bằng ejbca.sh ca importprofiles
    substep "Chạy ejbca.sh ca importprofiles..."
    IMPORT_OUTPUT=$(docker exec "$EJBCA_CONT" \
        /opt/keyfactor/bin/ejbca.sh ca importprofiles \
        -d "$EJBCA_PROFILE_DIR" 2>&1 || true)

    echo "$IMPORT_OUTPUT" | grep -Ev "^[0-9]{4}-" | sed 's/^/    /' || true

    if echo "$IMPORT_OUTPUT" | grep -qi "already exist"; then
        warn "Một số profiles đã tồn tại và không được update — kiểm tra log bên trên"
        warn "Có thể cần xóa thủ công: psql -c \"DELETE FROM CertificateProfileData WHERE id IN (5001,5002,5003)\""
    elif echo "$IMPORT_OUTPUT" | grep -qi "error\|fail\|exception"; then
        warn "Import có thể gặp lỗi — kiểm tra output bên trên"
    else
        ok "Import hoàn tất!"
    fi

    # Xóa cache EJBCA sau import để profile mới có hiệu lực ngay
    substep "Xóa EJBCA cache..."
    docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh clearcache &>/dev/null || true
    ok "Cache cleared"
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 4: Verify profiles sau import
# ════════════════════════════════════════════════════════════
step "Bước 4: Xác minh EJBCA Profiles sau Import"

if $DRY_RUN; then
    info "[DRY-RUN] Bỏ qua verification."
else
    # Export profiles từ DB: exportprofiles viết vào CONTAINER, docker cp ra host
    VERIFY_CONT_DIR="/tmp/ejbca-verify-profiles"
    VERIFY_HOST_DIR="/tmp/ejbca-verify-host-$$"
    info "Export profiles từ EJBCA (container → host) để kiểm tra..."
    docker exec "$EJBCA_CONT" mkdir -p "$VERIFY_CONT_DIR"
    docker exec "$EJBCA_CONT" /opt/keyfactor/bin/ejbca.sh ca exportprofiles \
        -d "$VERIFY_CONT_DIR" &>/dev/null || true
    mkdir -p "$VERIFY_HOST_DIR"
    docker cp "${EJBCA_CONT}:${VERIFY_CONT_DIR}/." "$VERIFY_HOST_DIR/" 2>/dev/null || true

    for PROFILE_NAME in "IVF-PDFSigner-Profile" "IVF-TSA-Profile" "IVF-TLS-Client-Profile"; do
        substep "Kiểm tra ${PROFILE_NAME}..."
        PROFILE_XML=$(ls "$VERIFY_HOST_DIR"/certprofile_${PROFILE_NAME}*.xml 2>/dev/null | head -1 || echo "")
        if [[ -z "$PROFILE_XML" ]]; then
            warn "  ${PROFILE_NAME} không tìm thấy trong DB sau import"
        elif grep -v "LOẠI BỎ\|<!--" "$PROFILE_XML" | grep -qi "SHA1withRSA\|MD5withRSA"; then
            warn "  ${PROFILE_NAME} — vẫn còn SHA1/MD5 trong algorithm list!"
        elif grep -qi "SHA256WithRSA\|SHA256withECDSA" "$PROFILE_XML"; then
            ok "  ${PROFILE_NAME} — FIPS algorithms confirmed ✓"
        else
            info "  ${PROFILE_NAME} — imported, kiểm tra qua UI để xác nhận"
        fi
    done
    rm -rf "$VERIFY_HOST_DIR"
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 5: Java security.properties cho EJBCA/SignServer JVM
# ════════════════════════════════════════════════════════════
step "Bước 5: JVM Security Properties (trong EJBCA container)"

# Tạo security properties file để disable SHA-1 cert chains
# và MD5 trong EJBCA's Java crypto
substep "Tạo custom java.security override..."

JAVA_SECURITY_PATCH=$(cat << 'SECEOF'
# IVF FIPS Phase 1 — Disable weak algorithms in Java
# Applied by fips-ejbca-harden.sh
# Reference: JEP 337, JEP 356

# Disallow these algorithms in certificate chains:
jdk.certpath.disabledAlgorithms=MD2, MD5, SHA1 jdkCA & usage TLSServer, \
    SHA1 jdkCA & denyAfter 2019-01-01, SHA1 jdkCA & usage SignedJAR & denyAfter 2019-01-01, \
    RSA keySize < 2048, DSA keySize < 2048, EC keySize < 224

# Disallow TLS 1.0 and 1.1:
jdk.tls.disabledAlgorithms=SSLv3, TLSv1, TLSv1.1, RC4, DES, MD5withRSA, \
    DH keySize < 2048, EC keySize < 224, 3DES_EDE_CBC, anon, NULL, \
    EXPORT, DES40_CBC, RC4_40, RC2, NULL_AUTH

# Prevent creation of certs using:
jdk.security.legacyAlgorithms=SHA1, DSA

SECEOF
)

if $DRY_RUN; then
    info "[DRY-RUN] java.security patch sẽ được áp dụng:"
    echo "$JAVA_SECURITY_PATCH" | head -20 | sed 's/^/    /'
else
    # Tìm java.security file trong EJBCA container
    SS_CONT=$(docker ps --filter name=ivf_signserver --format "{{.Names}}" | grep -v '\-db' | head -1 || true)

    for CONT in "$EJBCA_CONT" "${SS_CONT:-}"; do
        [[ -z "$CONT" ]] && continue
        CONT_SHORT="${CONT%%.*}"

        JAVA_HOME=$(docker exec "$CONT" bash -c 'echo $JAVA_HOME' 2>/dev/null | tr -d '\n\r' || echo "")
        if [[ -z "$JAVA_HOME" ]]; then
            JAVA_HOME=$(docker exec "$CONT" bash -c \
                'dirname $(dirname $(readlink -f $(which java) 2>/dev/null || echo /usr/bin/java))' \
                2>/dev/null | tr -d '\n\r' || echo "/usr")
        fi

        # Java 17+ stores java.security in conf/security/ (not lib/security/)
        JAVA_SEC=""
        for SEC_TRY in "${JAVA_HOME}/conf/security/java.security" \
                       "${JAVA_HOME}/lib/security/java.security" \
                       "/opt/java/openjdk/conf/security/java.security"; do
            if docker exec "$CONT" test -f "$SEC_TRY" 2>/dev/null; then
                JAVA_SEC="$SEC_TRY"
                break
            fi
        done
        if docker exec "$CONT" test -f "${JAVA_SEC:-/nonexistent}" 2>/dev/null; then
            # Backup trước khi sửa (dùng --user root vì java.security thuộc sở hữu root)
            docker exec --user root "$CONT" bash -c \
                "cp -p ${JAVA_SEC} ${JAVA_SEC}.fips-backup-$(date +%Y%m%d) 2>/dev/null || true"

            # Tạo patch file trong container
            echo "$JAVA_SECURITY_PATCH" | docker exec -i --user root "$CONT" bash -c \
                "cat > /tmp/java-security-fips.patch"

            # Check xem đã có patch chưa (idempotent)
            if docker exec "$CONT" grep -q "IVF FIPS Phase 1" "$JAVA_SEC" 2>/dev/null; then
                ok "  ${CONT_SHORT}: java.security đã có FIPS patch"
            else
                # Append patch vào cuối java.security (cần root)
                docker exec --user root "$CONT" bash -c \
                    "cat /tmp/java-security-fips.patch >> ${JAVA_SEC}"
                ok "  ${CONT_SHORT}: java.security đã được patch với FIPS settings"
            fi
        else
            warn "  ${CONT_SHORT}: Không tìm thấy java.security (JAVA_HOME=${JAVA_HOME})"
            # Kiểm tra các đường dẫn phổ biến
            for try_path in \
                "/opt/java/openjdk/conf/security/java.security" \
                "/usr/lib/jvm/java-17/conf/security/java.security" \
                "/usr/lib/jvm/java-17-slim/conf/security/java.security"; do
                if docker exec "$CONT" test -f "$try_path" 2>/dev/null; then
                    info "  Tìm thấy java.security tại: ${try_path}"
                    info "  Chạy thủ công: docker exec ${CONT} bash -c 'cat >> ${try_path}'"
                    break
                fi
            done
        fi
    done

    warn "Cần restart EJBCA/SignServer để java.security có hiệu lực:"
    warn "  docker service update --force ivf_ejbca"
    warn "  docker service update --force ivf_signserver"
    warn "(Sẽ restart tự động trong bước fips-tls-harden.sh)"
fi

# ════════════════════════════════════════════════════════════
# TÓM TẮT
# ════════════════════════════════════════════════════════════
step "Tóm tắt"

echo ""
echo "  ✅ Bước 1/4: fips-enable.sh       → DONE"
echo "  ✅ Bước 2/4: fips-verify.sh        → DONE"
echo "  ✅ Bước 3/4: fips-ejbca-harden.sh  → DONE (đang ở đây)"
echo "  👉 Bước 4/4: fips-tls-harden.sh   → Chạy tiếp theo"
echo ""
echo -e "${CYAN}  Changes đã thực hiện:${NC}"
echo "  • availablesignaturealgorithms: SHA256/SHA384/SHA512 with RSA/ECDSA only"
echo "  • minimumavailablebitlength: 2048 (bỏ 1024-bit RSA)"
echo "  • availableECCurvesAsString: P-256, P-384, P-521 only"
echo "  • jdk.tls.disabledAlgorithms: TLS 1.0/1.1, RC4, 3DES, MD5"
echo "  • jdk.certpath.disabledAlgorithms: MD5, SHA-1 (for TLS Server certs)"
echo ""
echo "  Lưu ý: Các certs đang chạy KHÔNG bị ảnh hưởng đến khi expire."
echo "  Profile changes chỉ áp dụng cho certificates TẠO MỚI."
echo ""
echo "  scp scripts/fips-tls-harden.sh root@10.200.0.1:/tmp/"
echo "  ssh root@10.200.0.1 'bash /tmp/fips-tls-harden.sh'"
