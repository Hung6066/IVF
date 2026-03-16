#!/bin/bash
# ═══════════════════════════════════════════════════════════════════
# lynis-ship.sh — Parse Lynis .dat report và upload JSON lên MinIO
# Chạy sau mỗi lần lynis audit system
# ═══════════════════════════════════════════════════════════════════
set -euo pipefail

REPORT_DIR="/var/log/lynis/reports"
HOSTNAME="vmi3129111"
TODAY="$(date +%Y-%m-%d)"
DATFILE="${REPORT_DIR}/lynis-${TODAY}.dat"
JSONFILE="${REPORT_DIR}/lynis-${TODAY}.json"

MINIO_ACCESS="ivf-admin-6dcf47ed0cd5cc8a"
MINIO_SECRET="ZvaBRL2rgSphAByqsm1jeMMAXNXiidFjc+yVO4PIfP0="
BUCKET="ivf-documents"
OBJECT_PREFIX="system/lynis/${HOSTNAME}"

# ─── thoát nếu không có file report ───
if [[ ! -f "${DATFILE}" ]]; then
    echo "Lynis report not found: ${DATFILE}" >&2
    exit 0
fi

# ─── parse .dat thành JSON ───
parse_lynis_dat() {
    local dat_file="$1"

    # Lấy các giá trị từ .dat file (key=value format)
    get_val() { grep -m1 "^${1}=" "${dat_file}" 2>/dev/null | cut -d= -f2- | tr -d '[:space:]' || echo ""; }
    get_list() { grep "^${1}\[\]=" "${dat_file}" 2>/dev/null | cut -d= -f2- | sed 's/^"\(.*\)"$/\1/' || true; }

    local score; score=$(get_val "lynis_version" || echo "0")
    local hardening; hardening=$(get_val "hardening_index")
    local tests_done; tests_done=$(get_val "tests_executed")
    # tests_executed may be pipe-delimited test IDs — count them
    if [[ "${tests_done}" =~ [A-Z] ]]; then
        tests_done=$(echo "${tests_done}" | tr '|' '\n' | grep -c '[A-Z]' || echo 0)
    fi
    local lynis_version; lynis_version=$(get_val "lynis_version")

    # Warnings và suggestions
    local warnings warnings_json
    warnings=$(get_list "warning")
    warnings_json="["
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        warnings_json+="\"$(echo "$line" | sed 's/"/\\"/g')\","
    done <<< "$warnings"
    warnings_json="${warnings_json%,}]"

    local suggestions sugg_json
    suggestions=$(get_list "suggestion")
    sugg_json="["
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        sugg_json+="\"$(echo "$line" | sed 's/"/\\"/g')\","
    done <<< "$suggestions"
    sugg_json="${sugg_json%,}]"

    # Vulnerable packages
    local vuln_pkgs vuln_json
    vuln_pkgs=$(get_list "vulnerable_package")
    vuln_json="["
    while IFS= read -r line; do
        [[ -z "$line" ]] && continue
        vuln_json+="\"$(echo "$line" | sed 's/"/\\"/g')\","
    done <<< "$vuln_pkgs"
    vuln_json="${vuln_json%,}]"

    local firewall; firewall=$(get_val "firewall_active")
    local malware_scanner; malware_scanner=$(get_val "malware_scanner")
    local compiler_installed; compiler_installed=$(get_val "compiler_installed")
    local kernel_version; kernel_version=$(uname -r)
    local os_name; os_name=$(lsb_release -d 2>/dev/null | cut -f2 || cat /etc/os-release 2>/dev/null | grep "PRETTY_NAME" | cut -d= -f2- | tr -d '"' || echo "Unknown")

    cat <<JSON
{
  "hostname": "${HOSTNAME}",
  "report_date": "${TODAY}",
  "generated_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "lynis_version": "${lynis_version}",
  "os": "${os_name}",
  "kernel": "${kernel_version}",
  "hardening_index": ${hardening:-0},
  "tests_executed": ${tests_done:-0},
  "firewall_active": "${firewall:-no}",
  "malware_scanner": "${malware_scanner:-}",
  "compiler_installed": "${compiler_installed:-no}",
  "warnings": ${warnings_json},
  "suggestions": ${sugg_json},
  "vulnerable_packages": ${vuln_json},
  "warning_count": $(echo "${warnings}" | grep -c '[^[:space:]]' || echo 0),
  "suggestion_count": $(echo "${suggestions}" | grep -c '[^[:space:]]' || echo 0),
  "source_file": "${DATFILE}"
}
JSON
}

parse_lynis_dat "${DATFILE}" > "${JSONFILE}"
chmod 600 "${JSONFILE}"

# ─── upload lên MinIO (via Docker ivf-monitoring overlay) ───
MC_HOST_URL="http://${MINIO_ACCESS}:${MINIO_SECRET}@minio-metrics:9000"
docker run --rm --network ivf-monitoring \
  -e "MC_HOST_minio=${MC_HOST_URL}" \
  -v "${JSONFILE}:/tmp/r.json:ro" \
  minio/mc:latest cp /tmp/r.json "minio/${BUCKET}/${OBJECT_PREFIX}/lynis-${TODAY}.json" --quiet
docker run --rm --network ivf-monitoring \
  -e "MC_HOST_minio=${MC_HOST_URL}" \
  -v "${JSONFILE}:/tmp/r.json:ro" \
  minio/mc:latest cp /tmp/r.json "minio/${BUCKET}/${OBJECT_PREFIX}/latest.json" --quiet

echo "Lynis report uploaded: ${BUCKET}/${OBJECT_PREFIX}/lynis-${TODAY}.json"
