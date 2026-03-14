#!/bin/bash

# Diagnose SSH key mismatch between local machine and VPS
# Usage: ./diagnose-ssh-keys.sh -p "password" [-i 10.200.0.1] [-t "totp_code"]

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Default values
VPS_IP="10.200.0.1"
ROOT_PASSWORD=""
TOTP_CODE=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -p|--password)
            ROOT_PASSWORD="$2"
            shift 2
            ;;
        -i|--ip)
            VPS_IP="$2"
            shift 2
            ;;
        -t|--totp)
            TOTP_CODE="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 -p PASSWORD [-i IP] [-t TOTP_CODE]"
            exit 1
            ;;
    esac
done

if [ -z "$ROOT_PASSWORD" ]; then
    echo "❌ Password required. Usage: $0 -p PASSWORD"
    exit 1
fi

echo -e "${CYAN}🔍 SSH Key Diagnostic Tool${NC}"
echo -e "${CYAN}===========================${NC}"
echo ""

# Step 1: Find local SSH keys
echo -e "${YELLOW}📁 Local SSH Public Keys:${NC}"
echo -e "${YELLOW}-------------------${NC}"

LOCAL_KEYS=()
KEY_FILES=()

if [ -f ~/.ssh/id_rsa.pub ]; then
    LOCAL_KEYS+=("$(cat ~/.ssh/id_rsa.pub)")
    KEY_FILES+=("id_rsa.pub (RSA)")
    echo -e "${GREEN}✓ id_rsa.pub found (RSA 2048-bit)${NC}"
fi

if [ -f ~/.ssh/id_ed25519_wsl.pub ]; then
    LOCAL_KEYS+=("$(cat ~/.ssh/id_ed25519_wsl.pub)")
    KEY_FILES+=("id_ed25519_wsl.pub (Ed25519)")
    echo -e "${GREEN}✓ id_ed25519_wsl.pub found (Ed25519)${NC}"
fi

if [ -f ~/.ssh/id_ed25519.pub ]; then
    LOCAL_KEYS+=("$(cat ~/.ssh/id_ed25519.pub)")
    KEY_FILES+=("id_ed25519.pub (Ed25519)")
    echo -e "${GREEN}✓ id_ed25519.pub found (Ed25519)${NC}"
fi

if [ ${#LOCAL_KEYS[@]} -eq 0 ]; then
    echo -e "${RED}❌ No SSH keys found in ~/.ssh/${NC}"
    echo ""
    echo -e "${YELLOW}Generate new key pair:${NC}"
    echo "  ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519 -N ''"
    exit 1
fi

echo ""

# Step 2: Get VPS authorized_keys
echo -e "${YELLOW}📡 Fetching VPS authorized_keys...${NC}"

# Try different methods to connect
VPS_KEYS=""

if command -v sshpass &> /dev/null; then
    echo -e "${GRAY}  Using sshpass for authentication${NC}"
    VPS_KEYS=$(sshpass -p "$ROOT_PASSWORD" ssh -o PubkeyAuthentication=no -o StrictHostKeyChecking=no root@"$VPS_IP" "grep -E 'ssh-rsa|ssh-ed25519' ~/.ssh/authorized_keys 2>/dev/null | sort" 2>/dev/null || echo "")
else
    echo -e "${YELLOW}  sshpass not found, using expect...${NC}"
    
    # Try with expect if available
    if command -v expect &> /dev/null; then
        expect_script=$(mktemp)
        cat > "$expect_script" << EOF
#!/usr/bin/expect -f
set timeout 10
spawn ssh -o PubkeyAuthentication=no root@$VPS_IP
expect "password:"
send "$ROOT_PASSWORD\r"
expect "#"
send "grep -E 'ssh-rsa|ssh-ed25519' ~/.ssh/authorized_keys 2>/dev/null | sort\r"
expect "#"
send "exit\r"
expect eof
EOF
        chmod +x "$expect_script"
        VPS_KEYS=$(expect "$expect_script" 2>/dev/null || echo "")
        rm -f "$expect_script"
    else
        echo -e "${YELLOW}  Please enter password when prompted...${NC}"
        VPS_KEYS=$(ssh -o PubkeyAuthentication=no root@"$VPS_IP" "grep -E 'ssh-rsa|ssh-ed25519' ~/.ssh/authorized_keys 2>/dev/null | sort" 2>/dev/null || echo "")
    fi
fi

echo ""
echo -e "${YELLOW}🔑 VPS authorized_keys:${NC}"
echo -e "${YELLOW}----------------------${NC}"

if [ -z "$VPS_KEYS" ]; then
    echo -e "${RED}❌ No keys found in authorized_keys (or connection failed)!${NC}"
    VPS_KEYS=""
else
    echo "$VPS_KEYS" | while IFS= read -r line; do
        if [[ $line =~ ssh-ed25519 ]]; then
            TYPE="Ed25519"
        else
            TYPE="RSA"
        fi
        COMMENT=$(echo "$line" | awk '{print $NF}')
        echo -e "  ${CYAN}• $TYPE → $COMMENT${NC}"
    done
fi

echo ""

# Step 3: Compare keys
echo -e "${YELLOW}🔄 Comparing Local vs VPS Keys:${NC}"
echo -e "${YELLOW}------------------------------${NC}"
echo ""

MISSING_KEYS=()
MISSING_KEY_FILES=()
FOUND_COUNT=0

for i in "${!LOCAL_KEYS[@]}"; do
    LOCAL_KEY="${LOCAL_KEYS[$i]}"
    KEY_FILE="${KEY_FILES[$i]}"
    
    # Extract key data (first two fields)
    LOCAL_KEY_DATA=$(echo "$LOCAL_KEY" | awk '{print $1 " " $2}')
    
    IS_FOUND=false
    
    if [ -n "$VPS_KEYS" ]; then
        while IFS= read -r vps_key; do
            VPS_KEY_DATA=$(echo "$vps_key" | awk '{print $1 " " $2}')
            if [ "$LOCAL_KEY_DATA" = "$VPS_KEY_DATA" ]; then
                IS_FOUND=true
                break
            fi
        done <<< "$VPS_KEYS"
    fi
    
    if [ "$IS_FOUND" = true ]; then
        echo -e "${GREEN}✅ $KEY_FILE - FOUND on VPS${NC}"
        ((FOUND_COUNT++))
    else
        echo -e "${RED}❌ $KEY_FILE - MISSING on VPS${NC}"
        MISSING_KEYS+=("$LOCAL_KEY")
        MISSING_KEY_FILES+=("$KEY_FILE")
    fi
done

echo ""

# Step 4: Recommendation
if [ ${#MISSING_KEYS[@]} -eq 0 ]; then
    echo -e "${GREEN}✨ All local keys are authorized on VPS!${NC}"
    echo ""
    echo -e "${YELLOW}Troubleshooting:${NC}"
    echo "  • SSH config might be blocking the key"
    echo "  • Check SSH config at ~/.ssh/config"
    echo "  • Verify key permissions: ls -la ~/.ssh/id_*"
    echo "  • Try connecting with verbose: ssh -vv root@$VPS_IP"
    exit 0
fi

echo -e "${RED}⚠️  FOUND MISMATCH - ${#MISSING_KEYS[@]} key(s) not authorized on VPS${NC}"
echo ""
echo -e "${YELLOW}Missing keys:${NC}"
for i in "${!MISSING_KEY_FILES[@]}"; do
    echo -e "  ${RED}• ${MISSING_KEY_FILES[$i]}${NC}"
done

echo ""
echo -e "${GREEN}Solution:${NC}"
echo ""

# Option 1: Show key content
KEY_TO_ADD="${MISSING_KEYS[0]}"
KEY_FILE_TO_ADD="${MISSING_KEY_FILES[0]}"

echo -e "${GREEN}Option 1️⃣  - Copy-paste to VPS console${NC}"
echo ""
echo "  1. Log in to VPS provider's web console"
echo "  2. Copy this entire key:"
echo ""
echo -e "${GRAY}     $KEY_TO_ADD${NC}"
echo ""
echo "  3. Run on VPS console:"
echo -e "${GRAY}     echo '$KEY_TO_ADD' >> ~/.ssh/authorized_keys${NC}"
echo -e "${GRAY}     chmod 600 ~/.ssh/authorized_keys${NC}"

echo ""
echo -e "${GREEN}Option 2️⃣  - Use add-ssh-key-to-vps.sh script${NC}"
echo ""
echo "  ./scripts/add-ssh-key-to-vps.sh -p 'password' -f '$KEY_FILE_TO_ADD'"

echo ""
echo ""
echo -e "${YELLOW}Then test:${NC}"
echo -e "${CYAN}  ssh root@$VPS_IP${NC}"
