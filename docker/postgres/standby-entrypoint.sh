#!/bin/bash
set -e

# ─── Standby Initialization Script ─────────────────────
# This script runs when the standby container starts for the first time.
# It uses pg_basebackup to clone data from the primary, then configures
# the instance as a streaming replication standby.

PGDATA="/var/lib/postgresql/data"
PRIMARY_HOST="${PRIMARY_HOST:-db}"
PRIMARY_PORT="${PRIMARY_PORT:-5432}"
REPLICATOR_USER="${REPLICATOR_USER:-replicator}"
REPLICATOR_PASSWORD="${REPLICATOR_PASSWORD:-replicator_pass}"

# Only initialize if PGDATA is empty (first run)
if [ -z "$(ls -A "$PGDATA" 2>/dev/null)" ]; then
    echo "Standby: PGDATA is empty — cloning from primary ($PRIMARY_HOST:$PRIMARY_PORT)..."

    # Wait for primary to be ready
    until PGPASSWORD="$REPLICATOR_PASSWORD" pg_isready -h "$PRIMARY_HOST" -p "$PRIMARY_PORT" -U "$REPLICATOR_USER" 2>/dev/null; do
        echo "Waiting for primary to be ready..."
        sleep 2
    done

    # Clone primary using pg_basebackup
    PGPASSWORD="$REPLICATOR_PASSWORD" pg_basebackup \
        -h "$PRIMARY_HOST" \
        -p "$PRIMARY_PORT" \
        -U "$REPLICATOR_USER" \
        -D "$PGDATA" \
        -Fp -Xs -P --checkpoint=fast

    # Create standby signal file
    touch "$PGDATA/standby.signal"

    # Configure primary connection
    cat >> "$PGDATA/postgresql.auto.conf" <<EOF

# Streaming replication configuration (auto-generated)
primary_conninfo = 'host=$PRIMARY_HOST port=$PRIMARY_PORT user=$REPLICATOR_USER password=$REPLICATOR_PASSWORD application_name=ivf-standby'
primary_slot_name = 'standby_slot'
hot_standby = on
EOF

    echo "Standby initialized successfully from primary"
else
    echo "Standby: PGDATA already exists — starting as standby"
    # Ensure standby.signal exists
    if [ ! -f "$PGDATA/standby.signal" ]; then
        touch "$PGDATA/standby.signal"
    fi
fi

# Start PostgreSQL (exec replaces shell with postgres process)
exec docker-entrypoint.sh postgres
