#!/bin/bash

# Add SSH public key to VPS authorized_keys
# Usage: ./add-ssh-key-to-vps.sh -p "password" [-f "id_rsa.pub"] [-i 10.200.0.1]

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m'

# Defaults
ROOT_PASSWORD=""
KEY_FILE=~/.ssh/id_rsa.pub
VPS_IP="10.200.0.1"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -p|--password)
            ROOT_PASSWORD="$2"
            shift 2
            ;;
        -f|--file)
            KEY_FILE="$2"
            shift 2
            ;;
        -i|--ip)
            VPS_IP="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 -p PASSWORD [-f KEY_FILE] [-i IP]"
            exit 1
            ;;
    esac
done

if [ -z "$ROOT_PASSWORD" ]; then
    echo "❌ Password required"
    echo "Usage: $0 -p PASSWORD [-f KEY_FILE] [-i IP]"
    exit 1
fi

echo -e "${CYAN}🔑 Adding SSH Public Key to VPS${NC}"
echo -e "${CYAN}================================${NC}"
echo ""

# Expand ~ in path
KEY_FILE="${KEY_FILE/#\~/$HOME}"

# Check if key exists
if [ ! -f "$KEY_FILE" ]; then
    echo -e "${RED}❌ Key file not found: $KEY_FILE${NC}"
    echo ""
    echo -e "${YELLOW}Available keys:${NC}"
    ls ~/.ssh/*.pub 2>/dev/null | while read -r key; do
        echo "  • $(basename "$key")"
    done
    exit 1
fi

# Read key
KEY_CONTENT=$(cat "$KEY_FILE")
KEY_TYPE=$(echo "$KEY_CONTENT" | awk '{print $1}')
KEY_COMMENT=$(echo "$KEY_CONTENT" | awk '{print $NF}')

echo -e "${GREEN}✓ Key file found${NC}"
echo -e "${GRAY}  File: $(basename "$KEY_FILE")${NC}"
echo -e "${GRAY}  Type: $KEY_TYPE${NC}"
echo -e "${GRAY}  Comment: $KEY_COMMENT${NC}"
echo ""

# Escape key for bash
ESCAPED_KEY=$(echo "$KEY_CONTENT" | sed 's/\\/\\\\/g' | sed 's/"/\\"/g' | sed "s/'/\\\\'/g")

echo -e "${YELLOW}🔄 Connecting to VPS...${NC}"

# Create SSH command
SSH_COMMAND=$(cat <<'EOF'
# Add key to authorized_keys if not already present
KEY="$1"
AUTHKEYS=$HOME/.ssh/authorized_keys

# Create .ssh directory if missing
mkdir -p ~/.ssh

# Add key if not already present
if ! echo "$KEY" | grep -q "$(echo "$KEY" | awk '{print $1 " " $2}' | cut -d' ' -f1-2)" "$AUTHKEYS" 2>/dev/null; then
  echo "$KEY" >> "$AUTHKEYS"
  echo "✨ Key added to authorized_keys"
else
  echo "ℹ️  Key already present in authorized_keys"
fi

# Fix permissions
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys

# Verify
echo ""
echo "📊 Final check:"
echo "  Total keys: $(wc -l < $AUTHKEYS)"
EOF
)

# Use sshpass if available
if command -v sshpass &> /dev/null; then
    echo -e "${GRAY}  Using sshpass${NC}"
    RESULT=$(echo "$ESCAPED_KEY" | sshpass -p "$ROOT_PASSWORD" ssh -o PubkeyAuthentication=no -o StrictHostKeyChecking=no root@"$VPS_IP" bash 2>&1 << 'BASH_END'
KEY=$(cat -)
AUTHKEYS=$HOME/.ssh/authorized_keys

# Create .ssh if missing
mkdir -p ~/.ssh

# Add key if not present
KEY_DATA=$(echo "$KEY" | awk '{print $1 " " $2}')
if ! grep -q "$(echo "$KEY_DATA" | cut -d' ' -f1)" "$AUTHKEYS" 2>/dev/null; then
  echo "$KEY" >> "$AUTHKEYS"
  echo "✨ Key added to authorized_keys"
else
  echo "ℹ️  Key already present"
fi

# Fix permissions
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys

# Verify
echo ""
echo "📊 Final check:"
echo "  Total keys: $(wc -l < $AUTHKEYS 2>/dev/null || echo 'N/A')"
echo "  Your key: $(grep -q "$(echo "$KEY" | awk '{print $2}' | cut -c1-20)" "$AUTHKEYS" 2>/dev/null && echo '✅ FOUND' || echo '❌ NOT FOUND')"
BASH_END
)
else
    echo -e "${YELLOW}  sshpass not found, you'll be prompted for password${NC}"
    RESULT=$(echo "$ESCAPED_KEY" | ssh -o PubkeyAuthentication=no root@"$VPS_IP" bash 2>&1 << 'BASH_END'
KEY=$(cat -)
AUTHKEYS=$HOME/.ssh/authorized_keys

mkdir -p ~/.ssh

KEY_DATA=$(echo "$KEY" | awk '{print $1 " " $2}')
if ! grep -q "$(echo "$KEY_DATA" | cut -d' ' -f1)" "$AUTHKEYS" 2>/dev/null; then
  echo "$KEY" >> "$AUTHKEYS"
  echo "✨ Key added to authorized_keys"
else
  echo "ℹ️  Key already present"
fi

chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys

echo ""
echo "📊 Final check:"
echo "  Total keys: $(wc -l < $AUTHKEYS 2>/dev/null || echo 'N/A')"
BASH_END
)
fi

if [ $? -eq 0 ]; then
    echo ""
    echo -e "${GREEN}✅ VPS Response:${NC}"
    echo -e "${GREEN}=================${NC}"
    echo "$RESULT"
    echo ""
    echo -e "${CYAN}✨ Done! Test your SSH connection:${NC}"
    echo -e "${GRAY}  ssh root@$VPS_IP${NC}"
else
    echo -e "${RED}❌ Error connecting to VPS${NC}"
    exit 1
fi
