#!/bin/bash

# Fix SSH permissions on VPS
# This script should be run ON the VPS (not locally)

set -e

echo "🔧 Fixing SSH Permissions on VPS"
echo "=================================="
echo ""

# Step 1: Create .ssh directory if it doesn't exist
if [ ! -d ~/.ssh ]; then
    echo "📁 Creating ~/.ssh directory..."
    mkdir -p ~/.ssh
fi

# Step 2: Fix permissions
echo "🔐 Setting permissions..."
chmod 700 ~/.ssh
chmod 600 ~/.ssh/authorized_keys

echo "✅ Permissions fixed!"
echo ""

# Step 3: Verify setup
echo "📊 Current SSH Setup:"
echo "===================="
echo ""

echo "Directory permissions:"
ls -ld ~/.ssh
echo ""

echo "File permissions:"
ls -l ~/.ssh/authorized_keys
echo ""

echo "Total authorized keys:"
wc -l < ~/.ssh/authorized_keys
echo ""

echo "Key details:"
grep -E 'ssh-rsa|ssh-ed25519' ~/.ssh/authorized_keys | while IFS= read -r line; do
    # Extract the key type and comment
    key_type=$(echo "$line" | awk '{print $1}')
    comment=$(echo "$line" | awk '{print $NF}')
    echo "  • $key_type → $comment"
done
echo ""

echo "✨ SSH setup complete!"
echo ""
echo "Test connection from local machine:"
echo "  ssh root@10.200.0.1"
