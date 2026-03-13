#!/bin/bash
# =====================================================
# Cloudflare WAF — Deploy Managed Rulesets
# =====================================================
# Deploys Cloudflare WAF Managed Rulesets via API v4:
#   1. Cloudflare Managed Ruleset (threat intelligence)
#   2. Cloudflare OWASP Core Ruleset (ModSecurity CRS equivalent)
#   3. Custom WAF rules (scanner blocking, login protection)
#   4. Rate limiting rules (API + login throttling)
#
# Prerequisites:
#   - Cloudflare account with domain on Pro/Business/Enterprise plan
#     (Free plan has limited WAF — only 5 custom rules, no managed rulesets)
#   - API Token with Zone.Firewall Services permissions
#   - Domain proxied through Cloudflare (orange cloud ☁️)
#
# Environment variables:
#   CF_API_TOKEN  — Cloudflare API token
#   CF_ZONE_ID    — Cloudflare Zone ID
#
# Usage:
#   export CF_API_TOKEN="your-api-token"
#   export CF_ZONE_ID="your-zone-id"
#   ./scripts/deploy-cloudflare-waf.sh deploy
#   ./scripts/deploy-cloudflare-waf.sh status
#   ./scripts/deploy-cloudflare-waf.sh remove
# =====================================================

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }
log_step()  { echo -e "${CYAN}[STEP]${NC} $1"; }

CF_API_TOKEN="${CF_API_TOKEN:-}"
CF_ZONE_ID="${CF_ZONE_ID:-}"
CF_BASE="https://api.cloudflare.com/client/v4"

# ─── Validation ───
validate_env() {
    if [ -z "$CF_API_TOKEN" ]; then
        log_error "CF_API_TOKEN not set. Export your Cloudflare API token."
        exit 1
    fi
    if [ -z "$CF_ZONE_ID" ]; then
        log_error "CF_ZONE_ID not set. Export your Cloudflare Zone ID."
        exit 1
    fi

    # Verify token
    local result
    result=$(curl -sf -H "Authorization: Bearer ${CF_API_TOKEN}" \
        "${CF_BASE}/user/tokens/verify" 2>/dev/null || echo '{"success":false}')

    if echo "$result" | grep -q '"success":true'; then
        log_info "Cloudflare API token verified ✓"
    else
        log_error "Invalid Cloudflare API token"
        exit 1
    fi
}

# ─── Helper: API call ───
cf_api() {
    local method="$1"
    local path="$2"
    local data="${3:-}"

    local args=(-sf -X "$method" \
        -H "Authorization: Bearer ${CF_API_TOKEN}" \
        -H "Content-Type: application/json" \
        "${CF_BASE}${path}")

    if [ -n "$data" ]; then
        args+=(-d "$data")
    fi

    curl "${args[@]}" 2>/dev/null
}

# ─── Deploy Managed WAF Rulesets ───
deploy_managed_waf() {
    log_step "Deploying Cloudflare Managed WAF Rulesets..."

    # The entrypoint ruleset for the zone's managed WAF phase
    # This creates/updates the zone-level ruleset that "executes" managed rulesets
    local payload
    payload=$(cat <<'EOF'
{
  "description": "IVF System — WAF Managed Rulesets",
  "rules": [
    {
      "action": "execute",
      "action_parameters": {
        "id": "efb7b8c949ac4650a09736fc376e9aee",
        "overrides": {
          "rules": [
            {
              "id": "5de7edfa648c4d6891dc3e7f84534ffa",
              "action": "log",
              "enabled": true
            }
          ]
        }
      },
      "expression": "true",
      "description": "Execute Cloudflare Managed Ruleset",
      "enabled": true
    },
    {
      "action": "execute",
      "action_parameters": {
        "id": "4814384a9e5d4991b9815dcfc25d2f1f",
        "overrides": {
          "categories": [
            {
              "category": "paranoia-level-2",
              "enabled": false
            },
            {
              "category": "paranoia-level-3",
              "enabled": false
            },
            {
              "category": "paranoia-level-4",
              "enabled": false
            }
          ]
        }
      },
      "expression": "true",
      "description": "Execute Cloudflare OWASP Core Ruleset (Paranoia Level 1)",
      "enabled": true
    }
  ]
}
EOF
    )

    local result
    result=$(cf_api PUT "/zones/${CF_ZONE_ID}/rulesets/phases/http_request_firewall_managed/entrypoint" "$payload")

    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Managed WAF Rulesets deployed"
        log_info "    • Cloudflare Managed Ruleset (threat intelligence)"
        log_info "    • OWASP Core Ruleset (Paranoia Level 1)"
    else
        log_error "  ✗ Failed to deploy managed rulesets"
        echo "$result" | python3 -m json.tool 2>/dev/null || echo "$result"
        return 1
    fi
}

# ─── Deploy Custom WAF Rules ───
deploy_custom_rules() {
    log_step "Deploying Custom WAF Rules..."

    local payload
    payload=$(cat <<'EOF'
{
  "description": "IVF System — Custom WAF Rules",
  "rules": [
    {
      "action": "block",
      "expression": "(http.request.uri.path contains \"/wp-admin\") or (http.request.uri.path contains \"/wp-login\") or (http.request.uri.path contains \"/xmlrpc.php\") or (http.request.uri.path contains \"/phpmyadmin\") or (http.request.uri.path contains \"/.env\") or (http.request.uri.path contains \"/.git\") or (http.request.uri.path contains \"/actuator\") or (http.request.uri.path contains \"/console\")",
      "description": "Block common vulnerability scanner paths",
      "enabled": true
    },
    {
      "action": "managed_challenge",
      "expression": "(http.request.uri.path eq \"/api/auth/login\") and (cf.threat_score gt 20)",
      "description": "Challenge suspicious login attempts (threat score > 20)",
      "enabled": true
    },
    {
      "action": "block",
      "expression": "(http.request.uri.path contains \"/api/\") and (http.request.method eq \"POST\") and (http.request.body.size gt 10485760)",
      "description": "Block oversized POST requests to API (>10MB)",
      "enabled": true
    },
    {
      "action": "managed_challenge",
      "expression": "(cf.threat_score gt 50)",
      "description": "Challenge all high-threat traffic (score > 50)",
      "enabled": true
    },
    {
      "action": "block",
      "expression": "(http.request.uri.path contains \"../\") or (http.request.uri.path contains \"%2e%2e\") or (http.request.uri.path contains \"%252e%252e\")",
      "description": "Block path traversal attempts",
      "enabled": true
    }
  ]
}
EOF
    )

    local result
    result=$(cf_api PUT "/zones/${CF_ZONE_ID}/rulesets/phases/http_request_firewall_custom/entrypoint" "$payload")

    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Custom WAF Rules deployed"
        log_info "    • Scanner path blocking (wp-admin, phpmyadmin, .env, .git)"
        log_info "    • Suspicious login challenge (threat score > 20)"
        log_info "    • Oversized request blocking (> 10MB)"
        log_info "    • High-threat challenge (score > 50)"
        log_info "    • Path traversal blocking"
    else
        log_error "  ✗ Failed to deploy custom rules"
        echo "$result" | python3 -m json.tool 2>/dev/null || echo "$result"
        return 1
    fi
}

# ─── Deploy Rate Limiting Rules ───
deploy_rate_limiting() {
    log_step "Deploying Rate Limiting Rules..."

    local payload
    payload=$(cat <<'EOF'
{
  "description": "IVF System — Rate Limiting",
  "rules": [
    {
      "action": "block",
      "ratelimit": {
        "characteristics": ["cf.colo.id", "ip.src"],
        "period": 60,
        "requests_per_period": 5,
        "mitigation_timeout": 3600
      },
      "expression": "(http.request.uri.path eq \"/api/auth/login\") and (http.request.method eq \"POST\")",
      "description": "Login rate limit: 5 attempts/min per IP (1h block)",
      "enabled": true
    },
    {
      "action": "block",
      "ratelimit": {
        "characteristics": ["cf.colo.id", "ip.src"],
        "period": 60,
        "requests_per_period": 10,
        "mitigation_timeout": 600
      },
      "expression": "(http.request.uri.path contains \"/api/auth/\") and (http.request.method eq \"POST\")",
      "description": "Auth endpoint rate limit: 10 req/min per IP (10m block)",
      "enabled": true
    },
    {
      "action": "managed_challenge",
      "ratelimit": {
        "characteristics": ["cf.colo.id", "ip.src"],
        "period": 60,
        "requests_per_period": 200,
        "mitigation_timeout": 60
      },
      "expression": "(http.request.uri.path contains \"/api/\")",
      "description": "API rate limit: 200 req/min per IP (challenge on exceed)",
      "enabled": true
    }
  ]
}
EOF
    )

    local result
    result=$(cf_api PUT "/zones/${CF_ZONE_ID}/rulesets/phases/http_ratelimit/entrypoint" "$payload")

    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Rate Limiting Rules deployed"
        log_info "    • Login: 5 req/min per IP (1h block)"
        log_info "    • Auth endpoints: 10 req/min per IP (10m block)"
        log_info "    • API global: 200 req/min per IP (challenge)"
    else
        log_error "  ✗ Failed to deploy rate limiting rules"
        echo "$result" | python3 -m json.tool 2>/dev/null || echo "$result"
        return 1
    fi
}

# ─── Deploy Security Level + Bot Fight Mode ───
deploy_security_settings() {
    log_step "Configuring zone security settings..."

    # Set security level to "high"
    local result
    result=$(cf_api PATCH "/zones/${CF_ZONE_ID}/settings/security_level" '{"value":"high"}')
    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Security level: high"
    fi

    # Enable Bot Fight Mode
    result=$(cf_api PUT "/zones/${CF_ZONE_ID}/bot_management/fight_mode" '{"enabled":true}')
    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Bot Fight Mode: enabled"
    else
        log_warn "  ⚠ Bot Fight Mode may require Pro+ plan"
    fi

    # Enable Browser Integrity Check
    result=$(cf_api PATCH "/zones/${CF_ZONE_ID}/settings/browser_check" '{"value":"on"}')
    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Browser Integrity Check: enabled"
    fi

    # Enable Always Use HTTPS
    result=$(cf_api PATCH "/zones/${CF_ZONE_ID}/settings/always_use_https" '{"value":"on"}')
    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Always Use HTTPS: enabled"
    fi

    # Set minimum TLS version to 1.2
    result=$(cf_api PATCH "/zones/${CF_ZONE_ID}/settings/min_tls_version" '{"value":"1.2"}')
    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Minimum TLS version: 1.2"
    fi

    # Enable TLS 1.3
    result=$(cf_api PATCH "/zones/${CF_ZONE_ID}/settings/tls_1_3" '{"value":"on"}')
    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ TLS 1.3: enabled"
    fi

    # Enable Opportunistic Encryption
    result=$(cf_api PATCH "/zones/${CF_ZONE_ID}/settings/opportunistic_encryption" '{"value":"on"}')
    if echo "$result" | grep -q '"success":true'; then
        log_info "  ✓ Opportunistic Encryption: enabled"
    fi
}

# ─── Check WAF Status ───
check_status() {
    log_step "Checking Cloudflare WAF status..."

    echo ""
    log_info "── Managed WAF Rulesets ──"
    local managed
    managed=$(cf_api GET "/zones/${CF_ZONE_ID}/rulesets/phases/http_request_firewall_managed/entrypoint")
    if echo "$managed" | grep -q '"success":true'; then
        local rule_count
        rule_count=$(echo "$managed" | python3 -c "import sys,json; d=json.load(sys.stdin); print(len(d.get('result',{}).get('rules',[])))" 2>/dev/null || echo "?")
        log_info "  Active rules: ${rule_count}"
        echo "$managed" | python3 -c "
import sys, json
d = json.load(sys.stdin)
for r in d.get('result',{}).get('rules',[]):
    status = '✓' if r.get('enabled', True) else '✗'
    print(f'    {status} {r.get(\"description\", \"unnamed\")}')
" 2>/dev/null || true
    else
        log_warn "  No managed rulesets configured"
    fi

    echo ""
    log_info "── Custom WAF Rules ──"
    local custom
    custom=$(cf_api GET "/zones/${CF_ZONE_ID}/rulesets/phases/http_request_firewall_custom/entrypoint")
    if echo "$custom" | grep -q '"success":true'; then
        echo "$custom" | python3 -c "
import sys, json
d = json.load(sys.stdin)
for r in d.get('result',{}).get('rules',[]):
    status = '✓' if r.get('enabled', True) else '✗'
    print(f'    {status} {r.get(\"description\", \"unnamed\")}')
" 2>/dev/null || true
    else
        log_warn "  No custom rules configured"
    fi

    echo ""
    log_info "── Rate Limiting ──"
    local ratelimit
    ratelimit=$(cf_api GET "/zones/${CF_ZONE_ID}/rulesets/phases/http_ratelimit/entrypoint")
    if echo "$ratelimit" | grep -q '"success":true'; then
        echo "$ratelimit" | python3 -c "
import sys, json
d = json.load(sys.stdin)
for r in d.get('result',{}).get('rules',[]):
    status = '✓' if r.get('enabled', True) else '✗'
    rl = r.get('ratelimit', {})
    period = rl.get('period', '?')
    limit = rl.get('requests_per_period', '?')
    print(f'    {status} {r.get(\"description\", \"unnamed\")} [{limit} req/{period}s]')
" 2>/dev/null || true
    else
        log_warn "  No rate limiting rules configured"
    fi

    echo ""
    log_info "── Zone Security Settings ──"
    local sec_level
    sec_level=$(cf_api GET "/zones/${CF_ZONE_ID}/settings/security_level")
    local level_val
    level_val=$(echo "$sec_level" | python3 -c "import sys,json; print(json.load(sys.stdin).get('result',{}).get('value','?'))" 2>/dev/null || echo "?")
    log_info "  Security level: ${level_val}"

    local tls
    tls=$(cf_api GET "/zones/${CF_ZONE_ID}/settings/min_tls_version")
    local tls_val
    tls_val=$(echo "$tls" | python3 -c "import sys,json; print(json.load(sys.stdin).get('result',{}).get('value','?'))" 2>/dev/null || echo "?")
    log_info "  Minimum TLS: ${tls_val}"
}

# ─── Remove WAF Rules ───
remove_waf() {
    log_warn "Removing WAF rulesets..."
    read -p "Are you sure? This will remove all WAF protection. [y/N] " -r
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 0
    fi

    cf_api DELETE "/zones/${CF_ZONE_ID}/rulesets/phases/http_request_firewall_managed/entrypoint" >/dev/null 2>&1 || true
    cf_api DELETE "/zones/${CF_ZONE_ID}/rulesets/phases/http_request_firewall_custom/entrypoint" >/dev/null 2>&1 || true
    cf_api DELETE "/zones/${CF_ZONE_ID}/rulesets/phases/http_ratelimit/entrypoint" >/dev/null 2>&1 || true

    log_info "WAF rulesets removed"
}

# ─── Main ───
case "${1:-help}" in
    deploy)
        validate_env
        deploy_managed_waf
        deploy_custom_rules
        deploy_rate_limiting
        deploy_security_settings
        echo ""
        log_info "═══════════════════════════════════════════════════"
        log_info "  Cloudflare WAF deployed successfully!"
        log_info ""
        log_info "  Protection layers:"
        log_info "    1. Cloudflare Managed Ruleset (threat intelligence)"
        log_info "    2. OWASP Core Ruleset (ModSecurity CRS equivalent)"
        log_info "    3. Custom rules (scanner, traversal, login protection)"
        log_info "    4. Rate limiting (login, auth, API endpoints)"
        log_info "    5. Security settings (TLS 1.2+, bot fight, HTTPS)"
        log_info ""
        log_info "  Dashboard: https://dash.cloudflare.com → Security → WAF"
        log_info "═══════════════════════════════════════════════════"
        ;;
    status)
        validate_env
        check_status
        ;;
    remove)
        validate_env
        remove_waf
        ;;
    *)
        echo "Usage: $0 {deploy|status|remove}"
        echo ""
        echo "Commands:"
        echo "  deploy    Deploy all WAF rulesets + security settings"
        echo "  status    Check current WAF configuration"
        echo "  remove    Remove all WAF rules (dangerous!)"
        echo ""
        echo "Environment variables:"
        echo "  CF_API_TOKEN  Cloudflare API token (Zone.Firewall Services)"
        echo "  CF_ZONE_ID    Cloudflare Zone ID"
        echo ""
        echo "Example:"
        echo "  export CF_API_TOKEN='your-token'"
        echo "  export CF_ZONE_ID='your-zone-id'"
        echo "  $0 deploy"
        ;;
esac
