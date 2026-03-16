#!/usr/bin/env python3
"""Parse Lynis .dat report → JSON, then upload to MinIO via MC_HOST env var."""
import os, json, subprocess, sys
from datetime import datetime, timezone

REPORT_DIR = "/reports"
HOSTNAME = "vmi3129107"

today = datetime.now().strftime("%Y-%m-%d")
dat_file = f"{REPORT_DIR}/lynis-{today}.dat"
json_file = f"{REPORT_DIR}/lynis-{today}.json"

if not os.path.exists(dat_file):
    print(f"Report not found: {dat_file}", file=sys.stderr)
    sys.exit(0)

print(f"Parsing {dat_file}...")

def get_val(key):
    with open(dat_file) as f:
        for line in f:
            if line.startswith(f"{key}="):
                return line.split("=", 1)[1].strip()
    return ""

def get_list(key):
    items = []
    with open(dat_file) as f:
        for line in f:
            if line.startswith(f"{key}[]="):
                val = line.split("[]=", 1)[1].strip().strip('"')
                if val:
                    items.append(val)
    return items

# Read OS info
os_name = "Ubuntu"
try:
    with open("/reports/../etc/os-release") as f:
        for line in f:
            if line.startswith("PRETTY_NAME="):
                os_name = line.split("=", 1)[1].strip().strip('"')
                break
except Exception:
    pass

kernel = os.popen("uname -r").read().strip()

warnings = get_list("warning")
suggestions = get_list("suggestion")
vuln_pkgs = get_list("vulnerable_package")

def safe_int(val, default=0):
    """Parse value as int; if it's a pipe-delimited list, return count."""
    if not val:
        return default
    try:
        return int(val)
    except ValueError:
        # Lynis sometimes stores list of test IDs in tests_executed
        return len([x for x in val.split("|") if x.strip()])

data = {
    "hostname": HOSTNAME,
    "report_date": today,
    "generated_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "lynis_version": get_val("lynis_version"),
    "os": os_name,
    "kernel": kernel,
    "hardening_index": safe_int(get_val("hardening_index")),
    "tests_executed": safe_int(get_val("tests_executed")),
    "firewall_active": get_val("firewall_active") or "no",
    "malware_scanner": get_val("malware_scanner") or "",
    "compiler_installed": get_val("compiler_installed") or "no",
    "warnings": warnings,
    "suggestions": suggestions,
    "vulnerable_packages": vuln_pkgs,
    "warning_count": len(warnings),
    "suggestion_count": len(suggestions),
    "source_file": dat_file,
}

with open(json_file, "w") as f:
    json.dump(data, f, indent=2)

os.chmod(json_file, 0o600)
print(f"Generated {json_file} (hardening_index={data['hardening_index']}, tests={data['tests_executed']}, warnings={data['warning_count']}, suggestions={data['suggestion_count']})")
print("PARSE_DONE")
