#!/bin/bash
# ============================================================
# IVF PKI — FIPS Phase 1 (Bước 1/4): Bật FIPS Mode AlmaLinux
# ============================================================
# Chạy TRÊN VPS (AlmaLinux 8/9). Yêu cầu reboot sau khi hoàn thành.
# Thực hiện một lần duy nhất. An toàn khi chạy lại (idempotent).
#
# Tác dụng sau khi bật FIPS mode:
#   KERNEL:   Chỉ cho phép thuật toán được FIPS 140-2 Level 1 phê duyệt
#   OPENSSL:  Tự động từ chối MD5, RC4, 3DES, SHA-1 (trong TLS), DH <2048
#   POLICY:   /etc/crypto-policies/config → FIPS (thay DEFAULT)
#   MARKER:   /etc/system-fips được tạo
#
# Những gì KHÔNG thay đổi ngay (cần script tiếp theo):
#   - Certs EJBCA vẫn còn SHA-1 profile → chạy fips-ejbca-harden.sh
#   - TLS ciphers Caddy vẫn chưa restrict → chạy fips-tls-harden.sh
#   - Java trong EJBCA/SignServer container → chạy fips-tls-harden.sh
#
# QUAN TRỌNG: Sau khi script này chạy xong, BẮT BUỘC reboot!
#   ssh root@10.200.0.1 "reboot"
#   # Chờ VPS khởi động lại (~2-3 phút)
#   ssh root@10.200.0.1 "bash /tmp/fips-verify.sh"
#
# USAGE:
#   scp scripts/fips-enable.sh root@10.200.0.1:/tmp/
#   ssh root@10.200.0.1 "sed -i 's/\r//' /tmp/fips-enable.sh && bash /tmp/fips-enable.sh"
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
FORCE=false
DRY_RUN=false
SKIP_REBOOT_PROMPT=false

while [[ $# -gt 0 ]]; do
    case "$1" in
        --force)        FORCE=true; shift ;;
        --dry-run)      DRY_RUN=true; shift ;;
        --no-reboot)    SKIP_REBOOT_PROMPT=true; shift ;;
        --help|-h)
            echo "Usage: $0 [--force] [--dry-run] [--no-reboot]"
            echo "  --force         Bỏ qua confirmation prompt"
            echo "  --dry-run       Chỉ kiểm tra, không thay đổi"
            echo "  --no-reboot     Không hỏi reboot sau khi xong"
            exit 0 ;;
        *) err "Unknown option: $1"; exit 1 ;;
    esac
done

run() {
    if $DRY_RUN; then
        echo -e "  ${YELLOW}[DRY-RUN]${NC} $*"
    else
        "$@"
    fi
}

# ════════════════════════════════════════════════════════════
echo -e "${BOLD}${BLUE}"
echo "  ╔══════════════════════════════════════╗"
echo "  ║   IVF PKI — FIPS Mode Enabler        ║"
echo "  ║   Giai đoạn 1 / Bước 1 của 4         ║"
echo "  ╚══════════════════════════════════════╝"
echo -e "${NC}"

# ════════════════════════════════════════════════════════════
# BƯỚC 0: Kiểm tra điều kiện tiên quyết
# ════════════════════════════════════════════════════════════
step "Bước 0: Kiểm tra điều kiện"

# Root check
if [[ $EUID -ne 0 ]]; then
    err "Script này phải chạy với quyền root (sudo bash $0)"
    exit 1
fi
ok "Đang chạy với quyền root"

# OS check — yêu cầu AlmaLinux 8 hoặc 9
if ! grep -qi "almalinux\|rhel\|centos" /etc/os-release 2>/dev/null; then
    err "Script này chỉ dành cho AlmaLinux/RHEL 8+."
    err "OS hiện tại: $(cat /etc/os-release | grep PRETTY_NAME || echo 'unknown')"
    exit 1
fi

OS_VERSION=$(grep -oP '(?<=VERSION_ID=")[^"]+' /etc/os-release | cut -d. -f1)
OS_NAME=$(grep -oP '(?<=NAME=")[^"]+' /etc/os-release 2>/dev/null || echo "Unknown")
ok "OS: ${OS_NAME} (version ${OS_VERSION})"

if [[ "$OS_VERSION" -lt 8 ]]; then
    err "Yêu cầu AlmaLinux/RHEL 8 trở lên. Hiện tại: version ${OS_VERSION}"
    exit 1
fi

# Check đã bật FIPS chưa
if [[ -f /proc/sys/crypto/fips_enabled ]] && [[ "$(cat /proc/sys/crypto/fips_enabled)" == "1" ]]; then
    ok "FIPS mode đã được bật (fips_enabled=1). Không cần làm gì thêm."
    # Vẫn tiếp tục kiểm tra crypto policy
    ALREADY_FIPS=true
else
    info "FIPS mode chưa được bật."
    ALREADY_FIPS=false
fi

# Check tool fips-mode-setup
if ! command -v fips-mode-setup &>/dev/null; then
    info "Đang cài đặt gói 'crypto-policies-scripts'..."
    run dnf install -y crypto-policies-scripts dracut-fips
fi

# Check không gian đĩa (initramfs rebuild cần ~500MB)
BOOT_FREE=$(df /boot --output=avail -k | tail -1)
if [[ "$BOOT_FREE" -lt 51200 ]]; then  # < 50MB
    warn "Dung lượng /boot còn lại chỉ $((BOOT_FREE/1024))MB — có thể thiếu cho dracut rebuild."
    warn "Kiểm tra: df -h /boot"
    if ! $FORCE; then
        read -rp "Tiếp tục không? (y/N): " ans
        [[ "$ans" =~ ^[Yy]$ ]] || { info "Đã hủy."; exit 0; }
    fi
fi
ok "Dung lượng /boot: $((BOOT_FREE/1024))MB còn trống"

# Check không gian / cho dracut
ROOT_FREE=$(df / --output=avail -k | tail -1)
if [[ "$ROOT_FREE" -lt 524288 ]]; then  # < 512MB
    warn "Dung lượng / còn lại $((ROOT_FREE/1024/1024))GB — thấp."
fi
ok "Dung lượng /: $((ROOT_FREE/1024/1024))GB còn trống"

# ════════════════════════════════════════════════════════════
# BƯỚC 1: Hiển thị trạng thái hiện tại
# ════════════════════════════════════════════════════════════
step "Bước 1: Trạng thái crypto hiện tại"

substep "Kernel FIPS status..."
if [[ -f /proc/sys/crypto/fips_enabled ]]; then
    FIPS_VAL=$(cat /proc/sys/crypto/fips_enabled)
    if [[ "$FIPS_VAL" == "1" ]]; then
        ok "fips_enabled = 1 (ĐÃ BẬT)"
    else
        warn "fips_enabled = ${FIPS_VAL} (chưa bật)"
    fi
else
    warn "Không đọc được /proc/sys/crypto/fips_enabled"
fi

substep "Crypto policy hiện tại..."
CURRENT_POLICY=$(update-crypto-policies --show 2>/dev/null || echo "N/A")
info "Crypto policy: ${CURRENT_POLICY}"

substep "OpenSSL version..."
openssl version 2>/dev/null || warn "openssl không có trong PATH"

substep "Các weaker algorithms (MD5/SHA1) hiện có:"
# Test MD5 — nếu FIPS mode đã bật, lệnh này sẽ fail
if openssl dgst -md5 /dev/null 2>/dev/null; then
    warn "  MD5 còn được phép (sẽ bị chặn sau FIPS)"
else
    ok "  MD5 đã bị chặn"
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 2: Backup boot config
# ════════════════════════════════════════════════════════════
step "Bước 2: Backup cấu hình boot"

BACKUP_DIR="/root/fips-backup-$(date +%Y%m%d-%H%M%S)"
if ! $DRY_RUN; then
    mkdir -p "$BACKUP_DIR"
fi
info "Backup sẽ lưu vào: ${BACKUP_DIR}"

substep "Backup /etc/default/grub..."
run cp -p /etc/default/grub "${BACKUP_DIR}/grub.bak" 2>/dev/null || warn "Không có /etc/default/grub"

substep "Backup /etc/sysconfig/kernel (nếu có)..."
run cp -p /etc/sysconfig/kernel "${BACKUP_DIR}/kernel.bak" 2>/dev/null || true

substep "Backup current crypto policy..."
run cp -rp /etc/crypto-policies/ "${BACKUP_DIR}/crypto-policies.bak" 2>/dev/null || true

substep "Ghi lại kernel cmdline hiện tại..."
if ! $DRY_RUN; then
    cat /proc/cmdline > "${BACKUP_DIR}/cmdline.bak"
fi
ok "Backup hoàn tất tại ${BACKUP_DIR}"

# ════════════════════════════════════════════════════════════
# BƯỚC 3: Cảnh báo & xác nhận
# ════════════════════════════════════════════════════════════
step "Bước 3: Xác nhận trước khi bật FIPS"

echo ""
echo -e "${YELLOW}══ NHỮNG THAY ĐỔI SẼ THỰC HIỆN ══${NC}"
echo ""
echo "  1. Cài đặt: dracut-fips, crypto-policies-scripts (nếu chưa có)"
echo "  2. Chạy: fips-mode-setup --enable"
echo "     → Thêm 'fips=1' vào kernel boot parameters"
echo "     → Rebuild initramfs với FIPS module (dracut -f)"
echo "     → Tạo /etc/system-fips"
echo "  3. Chạy: update-crypto-policies --set FIPS"
echo "     → Toàn bộ OpenSSL/NSS/GnuTLS sẽ từ chối SHA-1 (TLS), MD5, RC4"
echo "     → SSH: bỏ hmac-md5, cipher 3des-cbc, diffie-hellman-group1-sha1"
echo "     → TLS: chỉ TLS 1.2 và 1.3"
echo ""
echo -e "${YELLOW}══ SAU KHI REBOOT ══${NC}"
echo ""
echo "  • Docker containers tiếp tục chạy bình thường"
echo "    (container userspace dùng host kernel crypto)"
echo "  • EJBCA/SignServer vẫn hoạt động (Java crypto riêng của JVM)"
echo "  • SSH không bị ảnh hưởng (dùng thuật toán mạnh: aes256, sha256)"
echo "  • Caddy TLS không bị ảnh hưởng (Go crypto tự quản lý)"
echo ""
echo -e "${RED}  ⚠  YÊU CẦU REBOOT VPS ⚠${NC}"
echo "     → Tất cả dịch vụ bị gián đoạn ~2-3 phút khi reboot"
echo ""

if ! $FORCE && ! $DRY_RUN; then
    read -rp "Xác nhận bật FIPS mode và reboot VPS? (yes/N): " ans
    if [[ "$ans" != "yes" ]]; then
        info "Đã hủy bởi người dùng."
        exit 0
    fi
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 4: Cài gói cần thiết
# ════════════════════════════════════════════════════════════
step "Bước 4: Cài đặt gói FIPS"

substep "Cài dracut-fips, crypto-policies-scripts..."
run dnf install -y dracut-fips crypto-policies-scripts 2>&1 | \
    grep -E "Installed|Already installed|Complete|Error" || true
ok "Gói FIPS đã sẵn sàng"

# Cài thêm dracut-fips-aesni nếu CPU hỗ trợ AES-NI (tất cả VPS hiện đại)
if grep -q "aes" /proc/cpuinfo 2>/dev/null; then
    substep "CPU hỗ trợ AES-NI — cài dracut-fips-aesni..."
    run dnf install -y dracut-fips-aesni 2>&1 | \
        grep -E "Installed|Already installed|Complete|Error" || true
    ok "dracut-fips-aesni đã cài"
else
    info "CPU không có AES-NI, bỏ qua dracut-fips-aesni"
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 5: Bật FIPS mode
# ════════════════════════════════════════════════════════════
step "Bước 5: Bật FIPS mode"

if $ALREADY_FIPS; then
    ok "FIPS mode đã bật từ trước, bỏ qua bước này."
else
    substep "Chạy fips-mode-setup --enable..."
    if $DRY_RUN; then
        echo -e "  ${YELLOW}[DRY-RUN]${NC} fips-mode-setup --enable"
    else
        if fips-mode-setup --enable; then
            ok "fips-mode-setup --enable thành công"
        else
            err "fips-mode-setup --enable thất bại!"
            err "Thử thủ công: fips-mode-setup --check để xem lỗi"
            exit 1
        fi
    fi
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 6: Cập nhật Crypto Policy
# ════════════════════════════════════════════════════════════
step "Bước 6: Cập nhật Crypto Policy → FIPS"

CURRENT_POLICY=$(update-crypto-policies --show 2>/dev/null || echo "UNKNOWN")

if [[ "$CURRENT_POLICY" == "FIPS" ]]; then
    ok "Crypto policy đã là FIPS — không cần thay đổi"
else
    substep "Cập nhật từ ${CURRENT_POLICY} → FIPS..."
    run update-crypto-policies --set FIPS

    # Verify
    if ! $DRY_RUN; then
        NEW_POLICY=$(update-crypto-policies --show)
        if [[ "$NEW_POLICY" == "FIPS" ]]; then
            ok "Crypto policy đã cập nhật thành: FIPS"
        else
            warn "Crypto policy hiện tại: ${NEW_POLICY} (mong đợi FIPS)"
        fi
    fi
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 7: Tạo file marker và notes
# ════════════════════════════════════════════════════════════
step "Bước 7: Ghi chú & chuẩn bị post-reboot"

# Tạo notes file cho post-reboot verification
if ! $DRY_RUN; then
    cat > /root/fips-migration-notes.txt << 'NOTES'
IVF FIPS Phase 1 Migration Notes
=================================
Được tạo bởi: fips-enable.sh
Thời gian: $(date)

Sau khi reboot, chạy theo thứ tự:
  1. bash /tmp/fips-verify.sh         → Xác minh FIPS mode + container health
  2. bash /tmp/fips-ejbca-harden.sh   → Disable SHA-1/MD5 trong EJBCA profiles
  3. bash /tmp/fips-tls-harden.sh     → Hardening TLS ciphers (Caddy + JVM)

Rollback nếu cần:
  fips-mode-setup --disable
  update-crypto-policies --set DEFAULT
  reboot

Backup: /root/fips-backup-*/
NOTES
    sed -i "s/\$(date)/$(date)/" /root/fips-migration-notes.txt
    info "Notes lưu tại: /root/fips-migration-notes.txt"
fi

# ════════════════════════════════════════════════════════════
# BƯỚC 8: Tóm tắt & reboot
# ════════════════════════════════════════════════════════════
step "Bước 8: Tóm tắt"

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║   FIPS Mode đã được cấu hình thành công!         ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════════════╝${NC}"
echo ""
echo "  ✅ fips-mode-setup --enable → XONG"
echo "  ✅ update-crypto-policies --set FIPS → XONG"
echo "  ✅ Backup configuration → ${BACKUP_DIR:-dry-run}"
echo ""
echo -e "${RED}  ⚠  CẦN REBOOT ĐỂ KÍCH HOẠT FIPS KERNEL MODULE ⚠${NC}"
echo ""

if $DRY_RUN; then
    info "[DRY-RUN] Bỏ qua reboot."
    echo ""
    echo "Lệnh sau khi chạy thực:"
    echo "  reboot"
    echo "  # sau khi VPS lên lại:"
    echo "  scp scripts/fips-verify.sh root@10.200.0.1:/tmp/"
    echo "  ssh root@10.200.0.1 'bash /tmp/fips-verify.sh'"
    exit 0
fi

if $SKIP_REBOOT_PROMPT; then
    warn "Bỏ qua reboot (--no-reboot). Nhớ reboot thủ công trước khi chạy fips-verify.sh!"
    echo ""
    echo "Lệnh reboot:"
    echo "  ssh root@10.200.0.1 'reboot'"
    exit 0
fi

echo ""
echo -e "${BOLD}Reboot ngay bây giờ?${NC}"
echo "  (Dịch vụ sẽ gián đoạn ~2-3 phút)"
read -rp "  Reboot VPS ngay? (yes/N): " REBOOT_ANS
if [[ "$REBOOT_ANS" == "yes" ]]; then
    echo ""
    info "Reboot trong 5 giây..."
    echo "  Từ client, chạy lệnh này sau khi VPS lên lại:"
    echo "  scp scripts/fips-verify.sh root@10.200.0.1:/tmp/"
    echo "  ssh root@10.200.0.1 'bash /tmp/fips-verify.sh'"
    sleep 5
    reboot
else
    warn "Chưa reboot. Nhớ reboot trước khi tiếp tục!"
    echo ""
    echo "  ssh root@10.200.0.1 'reboot'"
fi
