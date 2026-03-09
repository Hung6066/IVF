#!/bin/bash
# ═══════════════════════════════════════════════════════════
# Setup Discord Alerts for Grafana
# Creates Discord contact point and notification policies
#
# Usage:
#   ./setup-discord-alerts.sh <DISCORD_WEBHOOK_URL>
#
# Example:
#   ./setup-discord-alerts.sh https://discord.com/api/webhooks/xxx/yyy
# ═══════════════════════════════════════════════════════════

set -euo pipefail

WEBHOOK_URL="${1:-}"
GRAFANA_URL="http://localhost:3000/grafana"
GRAFANA_USER="admin"
GRAFANA_PASS="wDDaI8zzSTBPyzfGp3wRc6JkDGgIv6ZF"

if [ -z "$WEBHOOK_URL" ]; then
  echo "❌ Usage: $0 <DISCORD_WEBHOOK_URL>"
  echo ""
  echo "Tạo Discord Webhook:"
  echo "  1. Mở Discord → Server Settings → Integrations → Webhooks"
  echo "  2. Click 'New Webhook' → chọn channel"
  echo "  3. Copy Webhook URL"
  echo "  4. Chạy: $0 <url>"
  exit 1
fi

echo "🔧 Cấu hình Discord alerts cho Grafana..."

# Wait for Grafana to be ready
echo "⏳ Chờ Grafana khởi động..."
for i in $(seq 1 30); do
  if curl -sf "${GRAFANA_URL}/api/health" > /dev/null 2>&1; then
    echo "✅ Grafana sẵn sàng"
    break
  fi
  sleep 2
done

# Check if contact point already exists
EXISTING=$(curl -sf -u "${GRAFANA_USER}:${GRAFANA_PASS}" \
  "${GRAFANA_URL}/api/v1/provisioning/contact-points" 2>/dev/null | \
  python3 -c "import sys,json; cps=json.load(sys.stdin); print('found' if any(cp['name']=='discord-ivf' for cp in cps) else 'missing')" 2>/dev/null || echo "missing")

if [ "$EXISTING" = "found" ]; then
  echo "📝 Contact point 'discord-ivf' đã tồn tại — cập nhật webhook URL..."

  # Get UID
  CP_UID=$(curl -sf -u "${GRAFANA_USER}:${GRAFANA_PASS}" \
    "${GRAFANA_URL}/api/v1/provisioning/contact-points" | \
    python3 -c "import sys,json; cps=json.load(sys.stdin); [print(cp['uid']) for cp in cps if cp['name']=='discord-ivf']" 2>/dev/null)

  # Update
  curl -sf -X PUT -u "${GRAFANA_USER}:${GRAFANA_PASS}" \
    -H "Content-Type: application/json" \
    -H "X-Disable-Provenance: true" \
    "${GRAFANA_URL}/api/v1/provisioning/contact-points/${CP_UID}" \
    -d "{
      \"name\": \"discord-ivf\",
      \"type\": \"discord\",
      \"settings\": {
        \"url\": \"${WEBHOOK_URL}\",
        \"avatar_url\": \"https://grafana.com/static/assets/img/fav32.png\",
        \"use_discord_username\": true
      },
      \"disableResolveMessage\": false
    }" > /dev/null

  echo "✅ Contact point cập nhật thành công"
else
  echo "📝 Tạo contact point 'discord-ivf'..."

  HTTP_CODE=$(curl -sf -o /dev/null -w "%{http_code}" -X POST -u "${GRAFANA_USER}:${GRAFANA_PASS}" \
    -H "Content-Type: application/json" \
    -H "X-Disable-Provenance: true" \
    "${GRAFANA_URL}/api/v1/provisioning/contact-points" \
    -d "{
      \"name\": \"discord-ivf\",
      \"type\": \"discord\",
      \"settings\": {
        \"url\": \"${WEBHOOK_URL}\",
        \"avatar_url\": \"https://grafana.com/static/assets/img/fav32.png\",
        \"use_discord_username\": true
      },
      \"disableResolveMessage\": false
    }")

  if [ "$HTTP_CODE" = "202" ] || [ "$HTTP_CODE" = "200" ]; then
    echo "✅ Contact point tạo thành công"
  else
    echo "❌ Lỗi tạo contact point (HTTP ${HTTP_CODE})"
    exit 1
  fi
fi

# Configure notification policy to route ALL alerts to discord-ivf
echo ""
echo "📝 Cấu hình notification policy..."

POLICY_CODE=$(curl -sf -o /dev/null -w "%{http_code}" -X PUT -u "${GRAFANA_USER}:${GRAFANA_PASS}" \
  -H "Content-Type: application/json" \
  -H "X-Disable-Provenance: true" \
  "${GRAFANA_URL}/api/v1/provisioning/policies" \
  -d "{
    \"receiver\": \"discord-ivf\",
    \"group_by\": [\"grafana_folder\", \"alertname\"],
    \"group_wait\": \"15s\",
    \"group_interval\": \"1m\",
    \"repeat_interval\": \"4h\",
    \"routes\": [
      {
        \"receiver\": \"discord-ivf\",
        \"matchers\": [\"severity=critical\"],
        \"group_wait\": \"10s\",
        \"group_interval\": \"1m\",
        \"repeat_interval\": \"1h\",
        \"continue\": false
      },
      {
        \"receiver\": \"discord-ivf\",
        \"matchers\": [\"severity=warning\"],
        \"group_wait\": \"30s\",
        \"group_interval\": \"5m\",
        \"repeat_interval\": \"4h\",
        \"continue\": false
      }
    ]
  }")

if [ "$POLICY_CODE" = "202" ] || [ "$POLICY_CODE" = "200" ]; then
  echo "✅ Notification policy cấu hình thành công — tất cả alerts → Discord"
else
  echo "⚠️  Notification policy response: HTTP ${POLICY_CODE} (có thể cần cấu hình thủ công)"
fi

# Test the webhook
echo ""
echo "📤 Gửi test alert đến Discord..."
curl -sf -X POST "${WEBHOOK_URL}" \
  -H "Content-Type: application/json" \
  -d "{
    \"embeds\": [{
      \"title\": \"✅ IVF Alert System Connected\",
      \"description\": \"Grafana alert system đã kết nối thành công với Discord.\n\n🔔 Tất cả alerts sẽ được gửi đến channel này.\n📊 [Xem Grafana](https://natra.site/grafana/alerting/list)\",
      \"color\": 5763719,
      \"timestamp\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\",
      \"footer\": {\"text\": \"IVF Infrastructure Monitor\"},
      \"fields\": [
        {\"name\": \"System\", \"value\": \"IVF Production\", \"inline\": true},
        {\"name\": \"Server\", \"value\": \"$(hostname)\", \"inline\": true},
        {\"name\": \"Status\", \"value\": \"🟢 Active\", \"inline\": true}
      ]
    }]
  }" > /dev/null && echo "✅ Test alert gửi thành công!" || echo "❌ Lỗi gửi test alert"

echo ""
echo "═══════════════════════════════════════════"
echo "🎉 Hoàn tất cấu hình Discord alerts!"
echo ""
echo "📋 Alert rules đã cài đặt:"
echo "   • API: Down, Latency, Error Rate, Memory, GC"
echo "   • PostgreSQL: Down, Replication Lag, Deadlocks"
echo "   • Redis: Down, Memory, Rejected Connections"
echo "   • MinIO: Down, Disk Full"
echo "   • Caddy: Proxy Down"
echo "   • Logs: Exception Spike, Auth Failures, DB Errors,"
echo "           Security Events, Crash Loop"
echo "   • Prometheus: Scrape Target Down"
echo ""
echo "🔗 Quản lý alerts: https://natra.site/grafana/alerting/list"
echo "═══════════════════════════════════════════"
