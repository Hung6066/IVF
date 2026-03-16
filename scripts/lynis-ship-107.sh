#!/bin/bash
set -euo pipefail

REPORT_DIR="/var/log/lynis/reports"
HOSTNAME_="vmi3129107"
TODAY="$(date +%Y-%m-%d)"
DATFILE="${REPORT_DIR}/lynis-${TODAY}.dat"
JSONFILE="${REPORT_DIR}/lynis-${TODAY}.json"

MINIO_ACCESS="ivf-admin-6dcf47ed0cd5cc8a"
MINIO_SECRET='ZvaBRL2rgSphAByqsm1jeMMAXNXiidFjc+yVO4PIfP0='
BUCKET="ivf-documents"
OBJECT_PREFIX="system/lynis/${HOSTNAME_}"
OVERLAY_NETWORK="ivf-monitoring"

if [[ ! -f "${DATFILE}" ]]; then
    echo "Lynis report not found: ${DATFILE}" >&2
    exit 0
fi

get_val() { grep -m1 "^${1}=" "${DATFILE}" 2>/dev/null | cut -d= -f2- | tr -d '[:space:]' || echo ""; }
get_list() { grep "^${1}\[\]=" "${DATFILE}" 2>/dev/null | cut -d= -f2- | sed 's/^"\(.*\)"$/\1/' || true; }

hardening=$(get_val hardening_index)
tests_done=$(get_val tests_executed)
# tests_executed may be pipe-delimited test IDs — count them
if [[ "${tests_done}" =~ [A-Z] ]]; then
    tests_done=$(echo "${tests_done}" | tr '|' '\n' | grep -c '[A-Z]' || echo 0)
fi
lynis_ver=$(get_val lynis_version)
firewall=$(get_val firewall_active)
malware=$(get_val malware_scanner)
compiler=$(get_val compiler_installed)

warnings=$(get_list warning)
wj="["
while IFS= read -r wl; do
    [[ -z "$wl" ]] && continue
    wj+="\"$(echo "$wl" | sed 's/"/\\"/g')\","
done <<< "$warnings"
wj="${wj%,}]"

suggestions=$(get_list suggestion)
sj="["
while IFS= read -r sl; do
    [[ -z "$sl" ]] && continue
    sj+="\"$(echo "$sl" | sed 's/"/\\"/g')\","
done <<< "$suggestions"
sj="${sj%,}]"

vpkgs=$(get_list vulnerable_package)
vj="["
while IFS= read -r vl; do
    [[ -z "$vl" ]] && continue
    vj+="\"$(echo "$vl" | sed 's/"/\\"/g')\","
done <<< "$vpkgs"
vj="${vj%,}]"

wcount=$(echo "${warnings}" | grep -c '[^[:space:]]' || true)
scount=$(echo "${suggestions}" | grep -c '[^[:space:]]' || true)
os_name=$(lsb_release -d 2>/dev/null | cut -f2 || cat /etc/os-release 2>/dev/null | grep PRETTY_NAME | cut -d= -f2- | tr -d '"' || echo "Ubuntu")
kernel_ver=$(uname -r)

cat > "${JSONFILE}" << JEOF
{
  "hostname": "${HOSTNAME_}",
  "report_date": "${TODAY}",
  "generated_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "lynis_version": "${lynis_ver}",
  "os": "${os_name}",
  "kernel": "${kernel_ver}",
  "hardening_index": ${hardening:-0},
  "tests_executed": ${tests_done:-0},
  "firewall_active": "${firewall:-no}",
  "malware_scanner": "${malware:-}",
  "compiler_installed": "${compiler:-no}",
  "warnings": ${wj},
  "suggestions": ${sj},
  "vulnerable_packages": ${vj},
  "warning_count": ${wcount:-0},
  "suggestion_count": ${scount:-0},
  "source_file": "${DATFILE}"
}
JEOF
chmod 600 "${JSONFILE}"

echo "[lynis-ship] Uploading to MinIO via Docker overlay network..."
# MC_HOST_minio env var: mc reads credentials from this URL directly (no URL encoding needed)
MC_HOST_URL="http://${MINIO_ACCESS}:${MINIO_SECRET}@minio-metrics:9000"

# Use MC_HOST_minio env var — minio/mc:latest entrypoint is mc directly (no /bin/sh)
docker run --rm \
  --network "${OVERLAY_NETWORK}" \
  -e "MC_HOST_minio=${MC_HOST_URL}" \
  -v "${JSONFILE}:/tmp/report.json:ro" \
  minio/mc:latest \
  cp /tmp/report.json "minio/${BUCKET}/${OBJECT_PREFIX}/lynis-${TODAY}.json" --quiet
docker run --rm \
  --network "${OVERLAY_NETWORK}" \
  -e "MC_HOST_minio=${MC_HOST_URL}" \
  -v "${JSONFILE}:/tmp/report.json:ro" \
  minio/mc:latest \
  cp /tmp/report.json "minio/${BUCKET}/${OBJECT_PREFIX}/latest.json" --quiet

echo "Lynis report uploaded: ${BUCKET}/${OBJECT_PREFIX}/lynis-${TODAY}.json"
