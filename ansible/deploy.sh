#!/bin/bash

# ═══════════════════════════════════════════════════════════════
#  IVF Production Deployment Script — WSL Compatible
# ═══════════════════════════════════════════════════════════════
#
#  Sử dụng:
#    ./deploy.sh --help
#    ./deploy.sh --backend       # Deploy backend only
#    ./deploy.sh --frontend      # Deploy frontend only
#    ./deploy.sh --full          # Deploy backend + frontend (default)
#    ./deploy.sh --tag sha-abc123 --full
#
# ═══════════════════════════════════════════════════════════════

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Defaults
DEPLOY_BACKEND=true
DEPLOY_FRONTEND=true
IMAGE_TAG=""
GHCR_TOKEN="${GHCR_TOKEN:-}"
DRY_RUN=false

# Functions
print_help() {
    cat << 'EOF'
╔════════════════════════════════════════════════════════════════════╗
║           IVF Production Deployment Script — WSL                   ║
╚════════════════════════════════════════════════════════════════════╝

USAGE:
  ./deploy.sh [OPTIONS]

OPTIONS:
  --backend              Deploy backend (API) only
  --frontend             Deploy frontend only
  --full                 Deploy both backend and frontend (default)
  --tag TAG              Docker image tag (e.g., sha-c7d4766)
  --token TOKEN          GitHub Container Registry token
  --dry-run              Show what would be deployed (no changes)
  --help                 Show this help message

EXAMPLES:
  # Deploy both (interactive - will prompt for token)
  ./deploy.sh --full

  # Deploy only backend with specific tag
  ./deploy.sh --backend --tag sha-c7d4766

  # Deploy only frontend from environment variable
  GHCR_TOKEN=ghp_xxx ./deploy.sh --frontend --tag latest

  # Dry run to see deployment details
  ./deploy.sh --dry-run

ENVIRONMENT VARIABLES:
  GHCR_TOKEN             GitHub Container Registry token
  IMAGE_TAG              Override default image tag

EOF
}

log_info() {
    echo -e "${BLUE}ℹ ${NC}$1"
}

log_success() {
    echo -e "${GREEN}✅ ${NC}$1"
}

log_warning() {
    echo -e "${YELLOW}⚠️  ${NC}$1"
}

log_error() {
    echo -e "${RED}❌ ${NC}$1"
}

validate_prerequisites() {
    log_info "Checking prerequisites..."
    
    if ! command -v ansible-playbook &> /dev/null; then
        log_error "ansible-playbook not found. Install Ansible first:"
        echo "  pip install ansible"
        exit 1
    fi
    
    if ! command -v ssh &> /dev/null; then
        log_error "ssh not found"
        exit 1
    fi
    
    log_success "Prerequisites OK"
}

check_ssh_connection() {
    log_info "Testing SSH connection to manager..."
    
    if ssh -o ConnectTimeout=5 root@45.134.226.56 "echo 'SSH OK'" > /dev/null 2>&1; then
        log_success "SSH connection successful"
    else
        log_error "Cannot connect to VPS manager. Check SSH key and network."
        exit 1
    fi
}

get_image_tag() {
    if [ -z "$IMAGE_TAG" ]; then
        # Try to get from git
        if command -v git &> /dev/null && git rev-parse --git-dir > /dev/null 2>&1; then
            SHORT_SHA=$(git rev-parse --short HEAD 2>/dev/null || echo "latest")
            IMAGE_TAG="sha-${SHORT_SHA}"
            log_info "Using git commit: ${IMAGE_TAG}"
        else
            IMAGE_TAG="latest"
            log_warning "Using default tag: ${IMAGE_TAG}"
        fi
    fi
}

get_ghcr_token() {
    if [ -z "$GHCR_TOKEN" ]; then
        GHCR_TOKEN="${GHCR_TOKEN:-}"
        if [ -z "$GHCR_TOKEN" ]; then
            read -sp "Enter GitHub Container Registry token: " GHCR_TOKEN
            echo
            if [ -z "$GHCR_TOKEN" ]; then
                log_error "GHCR token is required"
                exit 1
            fi
        fi
    fi
}

build_ansible_command() {
    local cmd="ansible-playbook -i hosts.yml deploy.yml -v"
    
    cmd="${cmd} --extra-vars 'deploy_backend=${DEPLOY_BACKEND} deploy_frontend=${DEPLOY_FRONTEND} image_tag=${IMAGE_TAG} ghcr_token=${GHCR_TOKEN}'"
    
    if [ "$DRY_RUN" = true ]; then
        cmd="${cmd} --check"
    fi
    
    echo "$cmd"
}

show_deployment_config() {
    cat << EOF

╔════════════════════════════════════════════════════════════════════╗
║                    Deployment Configuration                        ║
╚════════════════════════════════════════════════════════════════════╝

  Backend:        $([ "$DEPLOY_BACKEND" = true ] && echo -e "${GREEN}✅ ENABLED${NC}" || echo -e "${RED}❌ DISABLED${NC}")
  Frontend:       $([ "$DEPLOY_FRONTEND" = true ] && echo -e "${GREEN}✅ ENABLED${NC}" || echo -e "${RED}❌ DISABLED${NC}")
  Image Tag:      ${BLUE}${IMAGE_TAG}${NC}
  Registry:       ghcr.io
  API Image:      ghcr.io/hung6066/ivf:${IMAGE_TAG}
  FE Image:       ghcr.io/hung6066/ivf-client:${IMAGE_TAG}
  Dry Run:        $([ "$DRY_RUN" = true ] && echo -e "${YELLOW}YES${NC}" || echo "NO")

EOF
}

confirm_deployment() {
    if [ "$DRY_RUN" = true ]; then
        log_warning "DRY RUN mode - no actual changes will be made"
    fi
    
    read -p "Continue with deployment? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log_info "Deployment cancelled"
        exit 0
    fi
}

run_deployment() {
    local cmd=$(build_ansible_command)
    
    log_info "Running deployment..."
    log_info "Command: $cmd"
    echo
    
    eval "$cmd"
    
    if [ $? -eq 0 ]; then
        log_success "Deployment completed successfully"
        
        if [ "$DRY_RUN" = false ]; then
            echo
            echo "Monitor progress at:"
            echo "  📊 Grafana:     https://natra.site/grafana/"
            echo "  🏥 Health:      https://natra.site/api/health/live"
            echo "  🌐 Frontend:    https://natra.site/"
        fi
    else
        log_error "Deployment failed"
        exit 1
    fi
}

# Main
main() {
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --backend)
                DEPLOY_BACKEND=true
                DEPLOY_FRONTEND=false
                shift
                ;;
            --frontend)
                DEPLOY_BACKEND=false
                DEPLOY_FRONTEND=true
                shift
                ;;
            --full)
                DEPLOY_BACKEND=true
                DEPLOY_FRONTEND=true
                shift
                ;;
            --tag)
                IMAGE_TAG="$2"
                shift 2
                ;;
            --token)
                GHCR_TOKEN="$2"
                shift 2
                ;;
            --dry-run)
                DRY_RUN=true
                shift
                ;;
            --help)
                print_help
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                print_help
                exit 1
                ;;
        esac
    done
    
    # Show header
    echo -e "${BLUE}"
    cat << 'EOF'
╔════════════════════════════════════════════════════════════════════╗
║          IVF Production Deployment — WSL Ansible Deployer          ║
╚════════════════════════════════════════════════════════════════════╝
EOF
    echo -e "${NC}"
    
    # Execution
    validate_prerequisites
    check_ssh_connection
    get_image_tag
    get_ghcr_token
    show_deployment_config
    confirm_deployment
    run_deployment
}

main "$@"
