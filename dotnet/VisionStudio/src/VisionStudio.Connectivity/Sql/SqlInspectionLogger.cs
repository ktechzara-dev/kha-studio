using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Connectivity.Sql;

public sealed class SqlTraceFilter
{
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public InspectionVerdict? Verdict { get; set; }
    public string? RecipeName { get; set; }
    public int Limit { get; set; } = 500;
}

/// <summary>
/// High-throughput SQL inspection database logger with automatic schema migrations,
/// parameterized indexing, bulk transactions, and data retention purging.
/// </summary>
public sealed class SqlInspectionLogger : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger? _logger;
    private SqliteConnection? _connection;

    public SqlInspectionLogger(string dbFilePath, ILogger? logger = null)
    {
        _logger = logger;
        var dir = Path.GetDirectoryName(dbFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        const string migrationSql = @"
            CREATE TABLE IF NOT EXISTS InspectionHistory (
                Id TEXT PRIMARY KEY,
                SequenceNumber INTEGER,
                Timestamp TEXT,
                LotNumber TEXT,
                SerialNumber TEXT,
                PartType TEXT,
                RecipeName TEXT,
                PipelineVersion TEXT,
                Verdict TEXT,
                ExecutionTimeMs REAL,
                MeasurementsJson TEXT,
                ImagePath TEXT,
                RecordHash TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_timestamp ON InspectionHistory(Timestamp);
            CREATE INDEX IF NOT EXISTS idx_lot_serial ON InspectionHistory(LotNumber, SerialNumber);
            CREATE INDEX IF NOT EXISTS idx_verdict ON InspectionHistory(Verdict);
        ";

        using var cmd = new SqliteCommand(migrationSql, conn);
        cmd.ExecuteNonQuery();
        _logger?.LogInformation("SQLite inspection database initialized with migration indexes.");
    }

    public async Task LogInspectionAsync(InspectionRecord record, CancellationToken ct = default)
    {
        await LogBatchAsync(new[] { record }, ct);
    }

    public async Task LogBatchAsync(IEnumerable<InspectionRecord> records, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var tx = conn.BeginTransaction();
        const string insertSql = @"
            INSERT INTO InspectionHistory (
                Id, SequenceNumber, Timestamp, LotNumber, SerialNumber, PartType,
                RecipeName, PipelineVersion, Verdict, ExecutionTimeMs, MeasurementsJson, ImagePath, RecordHash
            ) VALUES (
                @Id, @Seq, @Timestamp, @Lot, @Serial, @PartType,
                @Recipe, @Version, @Verdict, @TimeMs, @Measurements, @ImagePath, @Hash
            );
        ";

        using var cmd = new SqliteCommand(insertSql, conn, tx);
        var pId = cmd.Parameters.Add("@Id", SqliteType.Text);
        var pSeq = cmd.Parameters.Add("@Seq", SqliteType.Integer);
        var pTs = cmd.Parameters.Add("@Timestamp", SqliteType.Text);
        var pLot = cmd.Parameters.Add("@Lot", SqliteType.Text);
        var pSn = cmd.Parameters.Add("@Serial", SqliteType.Text);
        var pPart = cmd.Parameters.Add("@PartType", SqliteType.Text);
        var pRec = cmd.Parameters.Add("@Recipe", SqliteType.Text);
        var pVer = cmd.Parameters.Add("@Version", SqliteType.Text);
        var pVerdict = cmd.Parameters.Add("@Verdict", SqliteType.Text);
        var pTime = cmd.Parameters.Add("@TimeMs", SqliteType.Real);
        var pMeas = cmd.Parameters.Add("@Measurements", SqliteType.Text);
        var pImg = cmd.Parameters.Add("@ImagePath", SqliteType.Text);
        var pHash = cmd.Parameters.Add("@Hash", SqliteType.Text);

        foreach (var r in records)
        {
            pId.Value = r.Id;
            pSeq.Value = r.SequenceNumber;
            pTs.Value = r.Timestamp.ToString("o");
            pLot.Value = r.LotNumber;
            pSn.Value = r.SerialNumber;
            pPart.Value = r.PartType;
            pRec.Value = r.RecipeName;
            pVer.Value = r.PipelineVersion;
            pVerdict.Value = r.Verdict.ToString();
            pTime.Value = r.ExecutionTimeMs;
            pMeas.Value = JsonSerializer.Serialize(r.Measurements);
            pImg.Value = r.ImagePath;
            pHash.Value = r.RecordHash;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<InspectionRecord>> QueryAsync(SqlTraceFilter filter, CancellationToken ct = default)
    {
        var records = new List<InspectionRecord>();
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var query = "SELECT * FROM InspectionHistory WHERE 1=1";
        using var cmd = new SqliteCommand();
        cmd.Connection = conn;

        if (filter.StartTime.HasValue)
        {
            query += " AND Timestamp >= @Start";
            cmd.Parameters.AddWithValue("@Start", filter.StartTime.Value.ToString("o"));
        }
        if (filter.EndTime.HasValue)
        {
            query += " AND Timestamp <= @End";
            cmd.Parameters.AddWithValue("@End", filter.EndTime.Value.ToString("o"));
        }
        if (!string.IsNullOrWhiteSpace(filter.LotNumber))
        {
            query += " AND LotNumber LIKE @Lot";
            cmd.Parameters.AddWithValue("@Lot", $"%{filter.LotNumber}%");
        }
        if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
        {
            query += " AND SerialNumber LIKE @Serial";
            cmd.Parameters.AddWithValue("@Serial", $"%{filter.SerialNumber}%");
        }
        if (filter.Verdict.HasValue)
        {
            query += " AND Verdict = @Verdict";
            cmd.Parameters.AddWithValue("@Verdict", filter.Verdict.Value.ToString());
        }

        query += " ORDER BY SequenceNumber DESC LIMIT @Limit";
        cmd.Parameters.AddWithValue("@Limit", filter.Limit);
        cmd.CommandText = query;

        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var measJson = reader.GetString(reader.GetOrdinal("MeasurementsJson"));
            var meas = string.IsNullOrEmpty(measJson)
                ? new Dictionary<string, double>()
                : JsonSerializer.Deserialize<Dictionary<string, double>>(measJson) ?? new();

            Enum.TryParse<InspectionVerdict>(reader.GetString(reader.GetOrdinal("Verdict")), out var verdict);

            var r = new InspectionRecord
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                SequenceNumber = reader.GetInt64(reader.GetOrdinal("SequenceNumber")),
                Timestamp = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("Timestamp"))),
                LotNumber = reader.GetString(reader.GetOrdinal("LotNumber")),
                SerialNumber = reader.GetString(reader.GetOrdinal("SerialNumber")),
                PartType = reader.GetString(reader.GetOrdinal("PartType")),
                RecipeName = reader.GetString(reader.GetOrdinal("RecipeName")),
                PipelineVersion = reader.GetString(reader.GetOrdinal("PipelineVersion")),
                Verdict = verdict,
                ExecutionTimeMs = reader.GetDouble(reader.GetOrdinal("ExecutionTimeMs")),
                Measurements = meas,
                ImagePath = reader.GetString(reader.GetOrdinal("ImagePath")),
                RecordHash = reader.GetString(reader.GetOrdinal("RecordHash"))
            };
            records.Add(r);
        }

        return records;
    }

    public async Task<int> PurgeOldRecordsAsync(int retentionDays, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays).ToString("o");
        using var cmd = new SqliteCommand("DELETE FROM InspectionHistory WHERE Timestamp < @Cutoff", conn);
        cmd.Parameters.AddWithValue("@Cutoff", cutoff);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        _logger?.LogInformation("Purged {Count} historical records older than {Days} days.", rows, retentionDays);
        return rows;
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        _connection = null;
        return ValueTask.CompletedTask;
    }
}
