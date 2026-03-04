using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace IVF.Infrastructure.Services;

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly string _mainConnectionString;
    private readonly ILogger<TenantProvisioningService> _logger;

    // Tables that implement ITenantEntity — these are provisioned per-tenant
    // Names must match the actual PostgreSQL table names (snake_case via EF Core ToTable())
    private static readonly string[] TenantTables =
    [
        "users", "patients", "doctors", "couples", "treatment_cycles",
        "queue_tickets", "invoices", "appointments", "notifications",
        "form_templates", "form_responses", "service_catalogs"
    ];

    public TenantProvisioningService(
        IConfiguration configuration,
        ILogger<TenantProvisioningService> logger)
    {
        _mainConnectionString = configuration.GetConnectionString("DefaultConnection")!;
        _logger = logger;
    }

    public async Task<TenantProvisioningResult> ProvisionAsync(
        Guid tenantId,
        string tenantSlug,
        DataIsolationStrategy strategy,
        CancellationToken ct = default)
    {
        return strategy switch
        {
            DataIsolationStrategy.SeparateSchema => await ProvisionSchemaAsync(tenantId, tenantSlug, ct),
            DataIsolationStrategy.SeparateDatabase => await ProvisionDatabaseAsync(tenantId, tenantSlug, ct),
            DataIsolationStrategy.SharedDatabase => new TenantProvisioningResult(true, null, null, null),
            _ => new TenantProvisioningResult(false, null, null, $"Unknown strategy: {strategy}")
        };
    }

    public async Task DeprovisionAsync(
        Guid tenantId,
        DataIsolationStrategy previousStrategy,
        string? schemaName,
        string? connectionString,
        CancellationToken ct = default)
    {
        switch (previousStrategy)
        {
            case DataIsolationStrategy.SeparateSchema when !string.IsNullOrEmpty(schemaName):
                await MigrateDataFromSchemaAsync(tenantId, schemaName, ct);
                await DropSchemaAsync(schemaName, ct);
                break;

            case DataIsolationStrategy.SeparateDatabase when !string.IsNullOrEmpty(connectionString):
                await MigrateDataFromDatabaseAsync(tenantId, connectionString, ct);
                break;
        }
    }

    public async Task<bool> ResourceExistsAsync(
        DataIsolationStrategy strategy,
        string resourceName,
        CancellationToken ct = default)
    {
        await using var conn = CreateMainConnection();
        await conn.OpenAsync(ct);

        if (strategy == DataIsolationStrategy.SeparateSchema)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM information_schema.schemata WHERE schema_name = @name)";
            cmd.Parameters.AddWithValue("name", resourceName);
            return (bool)(await cmd.ExecuteScalarAsync(ct))!;
        }

        if (strategy == DataIsolationStrategy.SeparateDatabase)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)";
            cmd.Parameters.AddWithValue("name", resourceName);
            return (bool)(await cmd.ExecuteScalarAsync(ct))!;
        }

        return false;
    }

    // ── Schema Provisioning ──────────────────────────────────────────────

    private async Task<TenantProvisioningResult> ProvisionSchemaAsync(
        Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        var schemaName = $"tenant_{tenantSlug.Replace('-', '_')}";

        try
        {
            await using var conn = CreateMainConnection();
            await conn.OpenAsync(ct);

            // 1. Create schema
            await ExecuteNonQueryAsync(conn,
                $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"", ct);

            _logger.LogInformation("Created schema {Schema} for tenant {TenantId}", schemaName, tenantId);

            // 2. Create tables in the new schema (copy structure from public schema)
            foreach (var table in TenantTables)
            {
                var tableExists = await TableExistsInSchemaAsync(conn, schemaName, table, ct);
                if (!tableExists)
                {
                    await ExecuteNonQueryAsync(conn,
                        $"CREATE TABLE \"{schemaName}\".\"{table}\" (LIKE \"public\".\"{table}\" INCLUDING ALL)", ct);

                    _logger.LogInformation("Created table {Schema}.{Table}", schemaName, table);
                }
            }

            // 3. Migrate existing data from public schema to tenant schema
            await MigrateDataToSchemaAsync(conn, tenantId, schemaName, ct);

            _logger.LogInformation("Schema provisioning completed for tenant {TenantId}, schema={Schema}",
                tenantId, schemaName);

            return new TenantProvisioningResult(true, schemaName, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision schema for tenant {TenantId}", tenantId);
            return new TenantProvisioningResult(false, null, null, ex.Message);
        }
    }

    private async Task MigrateDataToSchemaAsync(
        NpgsqlConnection conn, Guid tenantId, string schemaName, CancellationToken ct)
    {
        foreach (var table in TenantTables)
        {
            // Check if there's data to migrate
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM \"public\".\"{table}\" WHERE \"TenantId\" = @tenantId";
            countCmd.Parameters.AddWithValue("tenantId", tenantId);
            var count = (long)(await countCmd.ExecuteScalarAsync(ct))!;

            if (count == 0) continue;

            // Insert data into tenant schema table
            await ExecuteNonQueryAsync(conn,
                $@"INSERT INTO ""{schemaName}"".""{table}""
                   SELECT * FROM ""public"".""{table}""
                   WHERE ""TenantId"" = '{tenantId}'
                   ON CONFLICT DO NOTHING", ct);

            // Delete from public schema
            await ExecuteNonQueryAsync(conn,
                $@"DELETE FROM ""public"".""{table}""
                   WHERE ""TenantId"" = '{tenantId}'", ct);

            _logger.LogInformation("Migrated {Count} rows for {Table} to schema {Schema}",
                count, table, schemaName);
        }
    }

    private async Task MigrateDataFromSchemaAsync(
        Guid tenantId, string schemaName, CancellationToken ct)
    {
        await using var conn = CreateMainConnection();
        await conn.OpenAsync(ct);

        // Reverse order to handle FK constraints (children first is OK for INSERT, but for moving back we need parent first)
        foreach (var table in TenantTables)
        {
            var tableExists = await TableExistsInSchemaAsync(conn, schemaName, table, ct);
            if (!tableExists) continue;

            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM \"{schemaName}\".\"{table}\"";
            var count = (long)(await countCmd.ExecuteScalarAsync(ct))!;

            if (count == 0) continue;

            await ExecuteNonQueryAsync(conn,
                $@"INSERT INTO ""public"".""{table}""
                   SELECT * FROM ""{schemaName}"".""{table}""
                   ON CONFLICT DO NOTHING", ct);

            _logger.LogInformation("Migrated {Count} rows from {Schema}.{Table} back to public",
                count, schemaName, table);
        }
    }

    private async Task DropSchemaAsync(string schemaName, CancellationToken ct)
    {
        await using var conn = CreateMainConnection();
        await conn.OpenAsync(ct);

        await ExecuteNonQueryAsync(conn,
            $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE", ct);

        _logger.LogInformation("Dropped schema {Schema}", schemaName);
    }

    // ── Database Provisioning ────────────────────────────────────────────

    private async Task<TenantProvisioningResult> ProvisionDatabaseAsync(
        Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        var dbName = $"ivf_{tenantSlug.Replace('-', '_')}";

        try
        {
            // 1. Create the database (must be outside transaction)
            await using (var conn = CreateMainConnection())
            {
                await conn.OpenAsync(ct);

                var exists = await DatabaseExistsAsync(conn, dbName, ct);
                if (!exists)
                {
                    // CREATE DATABASE cannot run inside a transaction
                    await ExecuteNonQueryAsync(conn, $"CREATE DATABASE \"{dbName}\"", ct);
                    _logger.LogInformation("Created database {Database} for tenant {TenantId}", dbName, tenantId);
                }
            }

            // 2. Build a connection string for the new database
            var builder = new NpgsqlConnectionStringBuilder(_mainConnectionString)
            {
                Database = dbName
            };
            var tenantConnStr = builder.ConnectionString;

            // 3. Create tables in the new database (copy structure from main DB)
            await using (var mainConn = CreateMainConnection())
            await using (var tenantConn = new NpgsqlConnection(tenantConnStr))
            {
                await mainConn.OpenAsync(ct);
                await tenantConn.OpenAsync(ct);

                foreach (var table in TenantTables)
                {
                    var tableExists = await TableExistsInSchemaAsync(tenantConn, "public", table, ct);
                    if (!tableExists)
                    {
                        // Get the CREATE TABLE DDL from the main database
                        var ddl = await GetTableDdlAsync(mainConn, table, ct);
                        if (ddl != null)
                        {
                            await ExecuteNonQueryAsync(tenantConn, ddl, ct);
                            _logger.LogInformation("Created table {Table} in database {Database}", table, dbName);
                        }
                    }
                }
            }

            // 4. Migrate data (use connection strings, not open connections, to avoid password stripping)
            await MigrateDataToDatabaseAsync(_mainConnectionString, tenantConnStr, tenantId, ct);

            _logger.LogInformation("Database provisioning completed for tenant {TenantId}, db={Database}",
                tenantId, dbName);

            return new TenantProvisioningResult(true, null, tenantConnStr, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision database for tenant {TenantId}", tenantId);
            return new TenantProvisioningResult(false, null, null, ex.Message);
        }
    }

    private async Task MigrateDataToDatabaseAsync(
        string mainConnStr, string tenantConnStr, Guid tenantId, CancellationToken ct)
    {
        foreach (var table in TenantTables)
        {
            long count;
            // Check if there's data to migrate
            await using (var mainConn = new NpgsqlConnection(mainConnStr))
            {
                await mainConn.OpenAsync(ct);
                await using var countCmd = mainConn.CreateCommand();
                countCmd.CommandText = $"SELECT COUNT(*) FROM \"public\".\"{table}\" WHERE \"TenantId\" = @tenantId";
                countCmd.Parameters.AddWithValue("tenantId", tenantId);
                count = (long)(await countCmd.ExecuteScalarAsync(ct))!;
            }

            if (count == 0) continue;

            List<string> columns;
            await using (var colConn = new NpgsqlConnection(mainConnStr))
            {
                await colConn.OpenAsync(ct);
                columns = await GetTableColumnsAsync(colConn, table, ct);
            }
            var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));

            // Export from main
            string data;
            await using (var exportConn = new NpgsqlConnection(mainConnStr))
            {
                await exportConn.OpenAsync(ct);
                await using var reader = await exportConn.BeginTextExportAsync(
                    $"COPY (SELECT {columnList} FROM \"public\".\"{table}\" WHERE \"TenantId\" = '{tenantId}') TO STDOUT", ct);
                data = await reader.ReadToEndAsync();
            }

            if (!string.IsNullOrEmpty(data))
            {
                await using var importConn = new NpgsqlConnection(tenantConnStr);
                await importConn.OpenAsync(ct);
                await using var writer = await importConn.BeginTextImportAsync(
                    $"COPY \"public\".\"{table}\" ({columnList}) FROM STDIN", ct);
                await writer.WriteAsync(data);
            }

            // Delete from main
            await using (var delConn = new NpgsqlConnection(mainConnStr))
            {
                await delConn.OpenAsync(ct);
                await ExecuteNonQueryAsync(delConn,
                    $"DELETE FROM \"public\".\"{table}\" WHERE \"TenantId\" = '{tenantId}'", ct);
            }

            _logger.LogInformation("Migrated {Count} rows for table {Table} to database", count, table);
        }
    }

    private async Task MigrateDataFromDatabaseAsync(
        Guid tenantId, string connectionString, CancellationToken ct)
    {
        foreach (var table in TenantTables)
        {
            long count;
            await using (var tenantConn = new NpgsqlConnection(connectionString))
            {
                await tenantConn.OpenAsync(ct);
                var tableExists = await TableExistsInSchemaAsync(tenantConn, "public", table, ct);
                if (!tableExists) continue;

                await using var countCmd = tenantConn.CreateCommand();
                countCmd.CommandText = $"SELECT COUNT(*) FROM \"public\".\"{table}\"";
                count = (long)(await countCmd.ExecuteScalarAsync(ct))!;
            }

            if (count == 0) continue;

            List<string> columns;
            await using (var tenantConn = new NpgsqlConnection(connectionString))
            {
                await tenantConn.OpenAsync(ct);
                columns = await GetTableColumnsAsync(tenantConn, table, ct);
            }
            var columnList = string.Join(", ", columns.Select(c => $"\"{c}\""));

            // Export from tenant DB
            string data;
            await using (var tenantConn = new NpgsqlConnection(connectionString))
            {
                await tenantConn.OpenAsync(ct);
                await using var reader = await tenantConn.BeginTextExportAsync(
                    $"COPY (SELECT {columnList} FROM \"public\".\"{table}\") TO STDOUT", ct);
                data = await reader.ReadToEndAsync();
            }

            if (!string.IsNullOrEmpty(data))
            {
                await using var mainConn = new NpgsqlConnection(_mainConnectionString);
                await mainConn.OpenAsync(ct);
                await using var writer = await mainConn.BeginTextImportAsync(
                    $"COPY \"public\".\"{table}\" ({columnList}) FROM STDIN", ct);
                await writer.WriteAsync(data);
            }

            _logger.LogInformation("Migrated {Count} rows from tenant DB back to main for table {Table}",
                count, table);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private NpgsqlConnection CreateMainConnection()
    {
        return new NpgsqlConnection(_mainConnectionString);
    }

    private static async Task ExecuteNonQueryAsync(
        NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> TableExistsInSchemaAsync(
        NpgsqlConnection conn, string schema, string table, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT EXISTS(
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = @schema AND table_name = @table)";
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task<bool> DatabaseExistsAsync(
        NpgsqlConnection conn, string dbName, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM pg_database WHERE datname = @name)";
        cmd.Parameters.AddWithValue("name", dbName);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private static async Task<List<string>> GetTableColumnsAsync(
        NpgsqlConnection conn, string table, CancellationToken ct)
    {
        var columns = new List<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT column_name FROM information_schema.columns
                           WHERE table_schema = 'public' AND table_name = @table
                           ORDER BY ordinal_position";
        cmd.Parameters.AddWithValue("table", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private static async Task<string?> GetTableDdlAsync(
        NpgsqlConnection conn, string table, CancellationToken ct)
    {
        // Build CREATE TABLE from information_schema
        var columns = new List<string>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT column_name, data_type, character_maximum_length,
                       is_nullable, column_default, udt_name
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = @table
                ORDER BY ordinal_position";
            cmd.Parameters.AddWithValue("table", table);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var colName = reader.GetString(0);
                var udtName = reader.GetString(5);
                var dataType = MapUdtToSqlType(udtName, reader.IsDBNull(2) ? null : reader.GetInt32(2));
                var nullable = reader.GetString(3) == "YES" ? "" : " NOT NULL";
                var defaultVal = reader.IsDBNull(4) ? "" : $" DEFAULT {reader.GetString(4)}";

                columns.Add($"\"{colName}\" {dataType}{nullable}{defaultVal}");
            }
        }

        if (columns.Count == 0) return null;

        // Add primary key (reader is now closed, safe to reuse connection)
        var pkColumns = await GetPrimaryKeyColumnsAsync(conn, table, ct);
        var pkClause = pkColumns.Count > 0
            ? $", PRIMARY KEY ({string.Join(", ", pkColumns.Select(c => $"\"{c}\""))})"
            : "";

        return $"CREATE TABLE IF NOT EXISTS \"public\".\"{table}\" ({string.Join(", ", columns)}{pkClause})";
    }

    private static async Task<List<string>> GetPrimaryKeyColumnsAsync(
        NpgsqlConnection conn, string table, CancellationToken ct)
    {
        var columns = new List<string>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu
                ON tc.constraint_name = kcu.constraint_name
                AND tc.table_schema = kcu.table_schema
            WHERE tc.constraint_type = 'PRIMARY KEY'
                AND tc.table_schema = 'public'
                AND tc.table_name = @table
            ORDER BY kcu.ordinal_position";
        cmd.Parameters.AddWithValue("table", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(reader.GetString(0));
        }
        return columns;
    }

    private static string MapUdtToSqlType(string udtName, int? maxLength)
    {
        return udtName switch
        {
            "uuid" => "uuid",
            "text" => "text",
            "varchar" => maxLength.HasValue ? $"varchar({maxLength})" : "varchar",
            "int4" => "integer",
            "int8" => "bigint",
            "int2" => "smallint",
            "float8" => "double precision",
            "float4" => "real",
            "numeric" => "numeric",
            "bool" => "boolean",
            "timestamp" => "timestamp without time zone",
            "timestamptz" => "timestamp with time zone",
            "date" => "date",
            "time" => "time without time zone",
            "timetz" => "time with time zone",
            "jsonb" => "jsonb",
            "json" => "json",
            "bytea" => "bytea",
            "interval" => "interval",
            _ => udtName // enum types, custom types
        };
    }
}
