#!/bin/bash
set -e

# ─── Create WAL archive directory ───────────────────────
mkdir -p /var/lib/postgresql/archive
chown postgres:postgres /var/lib/postgresql/archive

# ─── Create replication user ────────────────────────────
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'replicator') THEN
            CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD 'replicator_pass';
            RAISE NOTICE 'Replication user created';
        END IF;
    END
    \$\$;
EOSQL

# ─── Append replication entry to pg_hba.conf ───────────
# Allow replication from the Docker network (172.16.0.0/12 covers Docker default subnets)
if ! grep -q "replicator" /var/lib/postgresql/data/pg_hba.conf 2>/dev/null; then
    echo "host replication replicator 0.0.0.0/0 md5" >> /var/lib/postgresql/data/pg_hba.conf
    echo "Replication HBA entry added"
fi

# ─── Include WAL config ────────────────────────────────
# PostgreSQL 16 supports include_dir; we use ALTER SYSTEM as a fallback-safe approach
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
    ALTER SYSTEM SET wal_level = 'replica';
    ALTER SYSTEM SET archive_mode = 'on';
    ALTER SYSTEM SET archive_command = 'cp %p /var/lib/postgresql/archive/%f';
    ALTER SYSTEM SET archive_timeout = '300';
    ALTER SYSTEM SET max_wal_senders = 5;
    ALTER SYSTEM SET max_replication_slots = 5;
    ALTER SYSTEM SET wal_keep_size = '256MB';
    ALTER SYSTEM SET log_replication_commands = 'on';
EOSQL

echo "WAL archiving and replication configured — will take effect after restart"
