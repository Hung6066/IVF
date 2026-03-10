#!/bin/bash
# =============================================
# IVF Failover Script — Chạy trên VPS2 khi VPS1 (manager) bị down
#
# Kịch bản: VPS1 (45.134.226.56) không phản hồi → VPS2 tự đứng lên làm manager
#
# Cách dùng:
#   ./scripts/failover-manager.sh [--dry-run]
#
# Cài trên VPS2:
#   scp scripts/failover-manager.sh root@194.163.181.19:/opt/ivf/scripts/
#   ssh root@194.163.181.19 "chmod +x /opt/ivf/scripts/failover-manager.sh"
#
# Sau đó bật watchdog (scripts/watchdog-vps1.sh) để tự động gọi khi VPS1 down.
# =============================================

set -euo pipefail

DRY_RUN=false
[[ "${1:-}" == "--dry-run" ]] && DRY_RUN=true

VPS1_IP="45.134.226.56"
VPS2_IP="194.163.181.19"
STACK_NAME="ivf"
DISCORD_WEBHOOK="${DISCORD_WEBHOOK_URL:-}"
LOG="/var/log/ivf-failover.log"

log()  { echo "$(date -u '+%Y-%m-%dT%H:%M:%SZ') [FAILOVER] $1" | tee -a "$LOG"; }
alert() {
    log "ALERT: $1"
    if [[ -n "$DISCORD_WEBHOOK" ]]; then
        curl -s -X POST "$DISCORD_WEBHOOK" \
            -H "Content-Type: application/json" \
            -d "{\"content\":\"🚨 **IVF FAILOVER**: $1\"}" || true
    fi
}
run() {
    if [[ "$DRY_RUN" == true ]]; then
        log "[DRY-RUN] $*"
    else
        "$@"
    fi
}

# ─── 1. Kiểm tra xem VPS1 có thực sự down không ───
check_vps1_down() {
    log "Kiểm tra kết nối VPS1 ($VPS1_IP)..."
    for i in 1 2 3; do
        if curl -sk --connect-timeout 5 "https://natra.site/api/health/live" | grep -q "Healthy" 2>/dev/null; then
            log "VPS1 vẫn alive (lần $i). Hủy failover."
            exit 0
        fi
        sleep 5
    done
    log "VPS1 không phản hồi sau 3 lần test. Bắt đầu failover..."
}

# ─── 2. Kiểm tra VPS2 có phải là manager chưa ───
check_current_role() {
    local role
    role=$(docker info --format '{{.Swarm.LocalNodeState}}' 2>/dev/null || echo "unknown")
    log "VPS2 Swarm state: $role"
    if [[ "$role" != "active" ]]; then
        log "ERROR: VPS2 không ở trong Swarm. Không thể failover tự động."
        alert "VPS2 không ở trong Swarm! Cần can thiệp thủ công."
        exit 1
    fi
    local is_manager
    is_manager=$(docker info --format '{{.Swarm.ControlAvailable}}' 2>/dev/null || echo "false")
    if [[ "$is_manager" == "true" ]]; then
        log "VPS2 đã là manager. Tiếp tục..."
    else
        log "VPS2 là worker. Thực hiện force-promote..."
        run docker swarm init --force-new-cluster
        log "VPS2 đã được promote thành manager."
    fi
}

# ─── 3. Tag VPS2 nhận role=primary (để services có thể schedule ở đây) ───
promote_vps2_labels() {
    local node_id
    node_id=$(docker node ls --format '{{.ID}}' --filter "role=manager" | head -1)
    log "Gán label role=primary cho node VPS2 ($node_id)..."
    run docker node update --label-add role=primary "$node_id"
    run docker node update --label-add role=standby "$node_id" || true  # cho db-standby vẫn chạy được
}

# ─── 4. Promote PostgreSQL standby → primary ───
promote_postgres() {
    log "Promote PostgreSQL standby → primary..."
    local standby_container
    standby_container=$(docker ps -q -f "label=com.docker.swarm.service.name=${STACK_NAME}_db-standby" 2>/dev/null | head -1)

    if [[ -z "$standby_container" ]]; then
        log "WARNING: Không tìm thấy container db-standby. Bỏ qua postgres promote."
        return
    fi

    # Promote standby thành primary
    run docker exec "$standby_container" touch /tmp/promote-trigger 2>/dev/null || \
    run docker exec "$standby_container" pg_ctl promote -D /var/lib/postgresql/data 2>/dev/null || \
    log "WARNING: Không promote được PostgreSQL standby. Kiểm tra thủ công."

    log "PostgreSQL standby đã được promote thành primary."
    alert "PostgreSQL standby đã được promote thành primary trên VPS2."
}

# ─── 5. Promote Redis replica → master ───
promote_redis() {
    log "Promote Redis replica → master..."
    local redis_container
    redis_container=$(docker ps -q -f "label=com.docker.swarm.service.name=${STACK_NAME}_redis-replica" 2>/dev/null | head -1)

    if [[ -z "$redis_container" ]]; then
        log "WARNING: Không tìm thấy container redis-replica. Bỏ qua."
        return
    fi

    run docker exec "$redis_container" redis-cli SLAVEOF NO ONE
    log "Redis replica đã được promote thành master."
}

# ─── 6. Redeploy stack với constraints mới ───
redeploy_stack() {
    log "Redeploy stack để reschedule services lên VPS2..."
    local stack_file="/opt/ivf/docker-compose.stack.yml"

    if [[ ! -f "$stack_file" ]]; then
        log "ERROR: Không tìm thấy stack file tại $stack_file"
        alert "Failover cần can thiệp thủ công: stack file không tồn tại tại $stack_file"
        return
    fi

    # Force redeploy — Swarm sẽ reschedule các service còn lại lên VPS2
    run docker stack deploy -c "$stack_file" "$STACK_NAME" --with-registry-auth

    log "Stack đã redeploy. Đợi services khởi động..."
    sleep 30

    # Kiểm tra trạng thái
    docker service ls --filter "label=com.docker.stack.namespace=$STACK_NAME" 2>/dev/null || true
}

# ─── 7. Xác nhận failover thành công ───
verify_failover() {
    log "Kiểm tra sức khỏe sau failover..."
    sleep 20
    local status
    for i in 1 2 3 4 5; do
        status=$(curl -sk --connect-timeout 10 "https://natra.site/api/health/live" 2>/dev/null || echo "")
        if echo "$status" | grep -q "Healthy"; then
            log "✅ Failover thành công! API đã hoạt động trên VPS2."
            alert "✅ Failover hoàn thành thành công! API đang chạy trên VPS2 ($VPS2_IP). VPS1 ($VPS1_IP) cần kiểm tra."
            return
        fi
        log "Chờ API khởi động... ($i/5)"
        sleep 15
    done
    log "WARNING: API chưa healthy sau failover. Cần kiểm tra thủ công."
    alert "⚠️ Failover hoàn tất nhưng API chưa health. Kiểm tra thủ công tại VPS2 ($VPS2_IP)."
}

# ─── MAIN ───
main() {
    log "=== BẮT ĐẦU QUY TRÌNH FAILOVER ==="
    alert "🔄 Bắt đầu failover từ VPS1 → VPS2..."

    check_vps1_down
    check_current_role
    promote_vps2_labels
    promote_postgres
    promote_redis
    redeploy_stack
    verify_failover

    log "=== QUY TRÌNH FAILOVER HOÀN TẤT ==="
}

main "$@"
