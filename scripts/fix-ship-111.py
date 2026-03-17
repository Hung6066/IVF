#!/usr/bin/env python3
"""Fix vmi3129111 ship script to use docker run with ivf-monitoring."""
script_path = "/usr/local/bin/lynis-ship.sh"

with open(script_path) as f:
    lines = f.readlines()

print(f"Total lines: {len(lines)}")

# Find the upload section
start = None
end = None
for i, line in enumerate(lines):
    stripped = line.strip()
    if "upload" in stripped.lower() and "MinIO" in stripped and start is None:
        start = i
    if start is not None and stripped.startswith("echo") and "uploaded" in stripped:
        end = i + 1
        break

if start is None or end is None:
    print(f"Could not find upload section: start={start} end={end}")
    for i, l in enumerate(lines):
        if "upload" in l.lower() or "mc " in l or "MINIO" in l:
            print(f"  {i}: {repr(l.rstrip())}")
    exit(1)

print(f"Replacing lines {start}-{end}")
print("Old section:")
for l in lines[start:end]:
    print(f"  {repr(l.rstrip())}")

new_section = [
    "# ─── upload lên MinIO (via Docker ivf-monitoring overlay) ───\n",
    'MC_HOST_URL="http://${MINIO_ACCESS}:${MINIO_SECRET}@minio-metrics:9000"\n',
    'docker run --rm --network ivf-monitoring -e "MC_HOST_minio=${MC_HOST_URL}" -v "${JSONFILE}:/tmp/r.json:ro" minio/mc:latest cp /tmp/r.json "minio/${BUCKET}/${OBJECT_PREFIX}/lynis-${TODAY}.json" --quiet\n',
    'docker run --rm --network ivf-monitoring -e "MC_HOST_minio=${MC_HOST_URL}" -v "${JSONFILE}:/tmp/r.json:ro" minio/mc:latest cp /tmp/r.json "minio/${BUCKET}/${OBJECT_PREFIX}/latest.json" --quiet\n',
    'echo "Lynis report uploaded: ${BUCKET}/${OBJECT_PREFIX}/lynis-${TODAY}.json"\n',
]

new_lines = lines[:start] + new_section + lines[end:]

with open(script_path, "w") as f:
    f.writelines(new_lines)

print(f"Done: {len(lines)} -> {len(new_lines)} lines")
print("New upload section:")
for l in new_section:
    print(f"  {repr(l.rstrip())}")
