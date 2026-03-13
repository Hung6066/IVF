#!/bin/bash
# =====================================================
# Linkerd Service Mesh — Installation & Mesh Injection
# =====================================================
# Installs Linkerd on a Kubernetes cluster and injects
# the IVF system services into the mesh for automatic
# mTLS between all services.
#
# Prerequisites:
#   1. kubectl configured and pointing to the target cluster
#   2. Kubernetes cluster v1.25+ (EKS, AKS, GKE, or self-hosted)
#   3. linkerd CLI installed (or this script installs it)
#
# Architecture:
#   Linkerd Proxy (sidecar) ← injected into each pod
#   ├── Automatic mTLS between all meshed services
#   ├── Zero-config service discovery
#   ├── Request-level metrics (latency, success rate)
#   ├── Transparent TCP proxying
#   └── Identity: SPIFFE/X.509 certificates (auto-rotated)
#
# Usage:
#   # Full installation + mesh injection:
#   ./scripts/deploy-linkerd.sh install
#
#   # Check mesh status:
#   ./scripts/deploy-linkerd.sh check
#
#   # Inject mesh into existing namespace:
#   ./scripts/deploy-linkerd.sh inject ivf
#
#   # Dashboard:
#   ./scripts/deploy-linkerd.sh dashboard
#
#   # Uninstall:
#   ./scripts/deploy-linkerd.sh uninstall
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

NAMESPACE="${NAMESPACE:-ivf}"
LINKERD_VERSION="${LINKERD_VERSION:-stable-2.15.2}"
K8S_DIR="$(cd "$(dirname "$0")/../k8s" && pwd)"

# ─── Install linkerd CLI ───
install_cli() {
    if command -v linkerd &>/dev/null; then
        log_info "linkerd CLI already installed: $(linkerd version --client --short 2>/dev/null || echo 'unknown')"
        return
    fi

    log_step "Installing linkerd CLI..."
    curl -fsL https://run.linkerd.io/install | sh
    export PATH="$HOME/.linkerd2/bin:$PATH"

    if ! command -v linkerd &>/dev/null; then
        log_error "linkerd CLI installation failed"
        exit 1
    fi

    log_info "linkerd CLI installed: $(linkerd version --client --short)"
}

# ─── Pre-flight checks ───
preflight_check() {
    log_step "Running pre-flight checks..."

    if ! command -v kubectl &>/dev/null; then
        log_error "kubectl not found. Install kubectl first."
        exit 1
    fi

    if ! kubectl cluster-info &>/dev/null; then
        log_error "Cannot reach Kubernetes cluster. Check kubeconfig."
        exit 1
    fi

    local k8s_version
    k8s_version=$(kubectl version --short 2>/dev/null | grep "Server" | awk '{print $3}' || kubectl version -o json 2>/dev/null | grep -o '"gitVersion": "[^"]*"' | head -1 | cut -d'"' -f4)
    log_info "Kubernetes cluster: $k8s_version"

    linkerd check --pre || {
        log_warn "Some pre-flight checks failed — review above and fix before proceeding"
        read -p "Continue anyway? [y/N] " -r
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    }
}

# ─── Install Linkerd CRDs + Control Plane ───
install_linkerd() {
    log_step "Installing Linkerd CRDs..."
    linkerd install --crds | kubectl apply -f -

    log_step "Installing Linkerd control plane..."
    linkerd install \
        --set proxyInit.runAsRoot=false \
        --set proxy.resources.cpu.request=10m \
        --set proxy.resources.memory.request=20Mi \
        --set proxy.resources.memory.limit=250Mi \
        --set identityTrustAnchorsPEM="$(cat "${K8S_DIR}/linkerd/ca.crt" 2>/dev/null || echo '')" \
        | kubectl apply -f -

    log_step "Waiting for Linkerd control plane to be ready..."
    kubectl -n linkerd rollout status deploy/linkerd-destination --timeout=120s
    kubectl -n linkerd rollout status deploy/linkerd-identity --timeout=120s
    kubectl -n linkerd rollout status deploy/linkerd-proxy-injector --timeout=120s

    log_info "Linkerd control plane installed"
}

# ─── Install Linkerd Viz (observability dashboard) ───
install_viz() {
    log_step "Installing Linkerd Viz extension (dashboard + metrics)..."
    linkerd viz install \
        --set prometheus.enabled=false \
        --set grafana.enabled=false \
        | kubectl apply -f -

    kubectl -n linkerd-viz rollout status deploy/web --timeout=120s
    kubectl -n linkerd-viz rollout status deploy/tap --timeout=120s

    log_info "Linkerd Viz installed"
}

# ─── Deploy IVF K8s manifests ───
deploy_ivf() {
    log_step "Creating namespace '${NAMESPACE}' with Linkerd injection..."
    kubectl apply -f "${K8S_DIR}/namespace.yaml"

    log_step "Deploying secrets and configmaps..."
    if [ -f "${K8S_DIR}/secrets.yaml" ]; then
        kubectl apply -f "${K8S_DIR}/secrets.yaml"
    fi
    kubectl apply -f "${K8S_DIR}/configmap.yaml"

    log_step "Deploying IVF services..."
    kubectl apply -f "${K8S_DIR}/db-statefulset.yaml"
    kubectl apply -f "${K8S_DIR}/redis-deployment.yaml"
    kubectl apply -f "${K8S_DIR}/minio-statefulset.yaml"
    kubectl apply -f "${K8S_DIR}/api-deployment.yaml"
    kubectl apply -f "${K8S_DIR}/frontend-deployment.yaml"
    kubectl apply -f "${K8S_DIR}/ingress.yaml"

    log_step "Deploying network policies..."
    kubectl apply -f "${K8S_DIR}/network-policies.yaml"

    log_step "Deploying Linkerd service profiles..."
    kubectl apply -f "${K8S_DIR}/linkerd/service-profiles.yaml"

    log_step "Waiting for rollouts..."
    kubectl -n "${NAMESPACE}" rollout status deploy/ivf-api --timeout=180s
    kubectl -n "${NAMESPACE}" rollout status deploy/ivf-frontend --timeout=120s
    kubectl -n "${NAMESPACE}" rollout status statefulset/ivf-db --timeout=180s

    log_info "IVF system deployed with Linkerd mesh"
}

# ─── Inject mesh into existing namespace ───
inject_namespace() {
    local ns="${1:-$NAMESPACE}"
    log_step "Injecting Linkerd mesh into namespace '${ns}'..."

    kubectl get namespace "${ns}" -o yaml | \
        linkerd inject - | \
        kubectl apply -f -

    # Restart all deployments to pick up sidecar
    kubectl -n "${ns}" rollout restart deploy
    kubectl -n "${ns}" rollout restart statefulset 2>/dev/null || true

    log_info "Mesh injected into namespace '${ns}'"
}

# ─── Check mesh status ───
check_mesh() {
    log_step "Checking Linkerd mesh status..."
    linkerd check

    echo ""
    log_step "Meshed pods:"
    linkerd -n "${NAMESPACE}" stat deploy 2>/dev/null || log_warn "No meshed deployments found"

    echo ""
    log_step "mTLS status:"
    linkerd -n "${NAMESPACE}" edges deploy 2>/dev/null || log_warn "No edges found"
}

# ─── Dashboard ───
open_dashboard() {
    log_info "Opening Linkerd dashboard (Ctrl+C to stop)..."
    linkerd viz dashboard &
}

# ─── Uninstall ───
uninstall() {
    log_warn "Uninstalling Linkerd + IVF K8s resources..."
    read -p "Are you sure? This will remove all meshed services. [y/N] " -r
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 0
    fi

    kubectl delete namespace "${NAMESPACE}" --ignore-not-found
    linkerd viz uninstall | kubectl delete -f - 2>/dev/null || true
    linkerd uninstall | kubectl delete -f - 2>/dev/null || true

    log_info "Linkerd and IVF namespace removed"
}

# ─── Main ───
case "${1:-help}" in
    install)
        install_cli
        preflight_check
        install_linkerd
        install_viz
        deploy_ivf
        check_mesh
        echo ""
        log_info "═══════════════════════════════════════════════════"
        log_info "  Linkerd Service Mesh deployed successfully!"
        log_info ""
        log_info "  All inter-service traffic is now encrypted with"
        log_info "  automatic mTLS (mutual TLS) using SPIFFE/X.509."
        log_info ""
        log_info "  Dashboard: linkerd viz dashboard"
        log_info "  Status:    linkerd -n ${NAMESPACE} stat deploy"
        log_info "  Edges:     linkerd -n ${NAMESPACE} edges deploy"
        log_info "  Tap:       linkerd viz tap deploy/ivf-api -n ${NAMESPACE}"
        log_info "═══════════════════════════════════════════════════"
        ;;
    check)
        check_mesh
        ;;
    inject)
        inject_namespace "${2:-$NAMESPACE}"
        ;;
    dashboard)
        open_dashboard
        ;;
    uninstall)
        uninstall
        ;;
    *)
        echo "Usage: $0 {install|check|inject [namespace]|dashboard|uninstall}"
        echo ""
        echo "Commands:"
        echo "  install              Full installation: CLI + control plane + IVF deploy"
        echo "  check                Check mesh status and mTLS verification"
        echo "  inject [namespace]   Inject mesh sidecar into a namespace"
        echo "  dashboard            Open Linkerd Viz dashboard"
        echo "  uninstall            Remove Linkerd and IVF namespace"
        ;;
esac
