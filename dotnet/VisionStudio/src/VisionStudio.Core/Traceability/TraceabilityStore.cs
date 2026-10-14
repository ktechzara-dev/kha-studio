using System.Text;
using System.Text.Json;

namespace VisionStudio.Core.Traceability;

/// <summary>
/// Industrial persistent Traceability Store with tamper-evident cryptographic hash chain,
/// historical query filtering, CSV export, and disk retention guard.
/// </summary>
public sealed class TraceabilityStore
{
    private readonly string _storeDir;
    private readonly string _recordsFilePath;
    private readonly string _imagesDir;
    private readonly List<InspectionRecord> _records = new();
    private readonly object _lock = new();

    public IReadOnlyList<InspectionRecord> Records
    {
        get
        {
            lock (_lock)
            {
                return _records.ToList();
            }
        }
    }

    public string StoreDirectory => _storeDir;
    public string ImagesDirectory => _imagesDir;

    public TraceabilityStore(string? storeDir = null)
    {
        _storeDir = storeDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisionStudio", "Traceability");
        _recordsFilePath = Path.Combine(_storeDir, "inspections.jsonl");
        _imagesDir = Path.Combine(_storeDir, "images");

        Directory.CreateDirectory(_storeDir);
        Directory.CreateDirectory(_imagesDir);

        LoadRecords();
    }

    private void LoadRecords()
    {
        lock (_lock)
        {
            _records.Clear();
            if (!File.Exists(_recordsFilePath)) return;

            foreach (var line in File.ReadLines(_recordsFilePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize<InspectionRecord>(line);
                    if (record != null)
                    {
                        _records.Add(record);
                    }
                }
                catch
                {
                    // Skip corrupted lines
                }
            }
        }
    }

    /// <summary>
    /// Appends a new inspection record, computing and linking the tamper-evident hash chain.
    /// </summary>
    public InspectionRecord AppendRecord(InspectionRecord record)
    {
        lock (_lock)
        {
            record.SequenceNumber = _records.Count + 1;

            string prevHash = _records.Count > 0 ? _records[^1].RecordHash : "GENESIS";
            record.PreviousRecordHash = prevHash;
            record.RecordHash = record.ComputePayloadHash(prevHash);

            _records.Add(record);

            string line = JsonSerializer.Serialize(record) + Environment.NewLine;
            File.AppendAllText(_recordsFilePath, line, Encoding.UTF8);

            return record;
        }
    }

    /// <summary>
    /// Verifies the cryptographic integrity of the entire hash chain from Genesis to latest.
    /// Returns invalid if any record's verdict, timestamp, measurements, or link was tampered with.
    /// </summary>
    public HashChainValidationResult ValidateHashChain()
    {
        lock (_lock)
        {
            if (_records.Count == 0)
            {
                return new HashChainValidationResult(true, 0, null, "Hash chain is empty.");
            }

            string expectedPrev = "GENESIS";
            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];

                if (r.PreviousRecordHash != expectedPrev)
                {
                    return new HashChainValidationResult(
                        false,
                        _records.Count,
                        i,
                        $"Record #{r.SequenceNumber} previous hash mismatch. Expected '{expectedPrev}', found '{r.PreviousRecordHash}'. Data was modified or reordered.");
                }

                string recomputedHash = r.ComputePayloadHash(expectedPrev);
                if (recomputedHash != r.RecordHash)
                {
                    return new HashChainValidationResult(
                        false,
                        _records.Count,
                        i,
                        $"Record #{r.SequenceNumber} payload hash mismatch. Expected '{recomputedHash}', found '{r.RecordHash}'. Record fields were tampered with.");
                }

                expectedPrev = r.RecordHash;
            }

            return new HashChainValidationResult(true, _records.Count, null, "Hash chain integrity verified. All records are authentic and tamper-free.");
        }
    }

    /// <summary>
    /// Queries historical inspection records using the specified filter criteria.
    /// </summary>
    public IReadOnlyList<InspectionRecord> QueryRecords(TraceFilter filter)
    {
        lock (_lock)
        {
            var query = _records.AsEnumerable();

            if (filter.StartTime.HasValue)
            {
                query = query.Where(r => r.Timestamp >= filter.StartTime.Value);
            }

            if (filter.EndTime.HasValue)
            {
                query = query.Where(r => r.Timestamp <= filter.EndTime.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.LotNumber))
            {
                query = query.Where(r => r.LotNumber.Contains(filter.LotNumber, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            {
                query = query.Where(r => r.SerialNumber.Contains(filter.SerialNumber, StringComparison.OrdinalIgnoreCase));
            }

            if (filter.Verdict.HasValue)
            {
                query = query.Where(r => r.Verdict == filter.Verdict.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.RecipeId))
            {
                query = query.Where(r => string.Equals(r.RecipeId, filter.RecipeId, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(r.RecipeName, filter.RecipeId, StringComparison.OrdinalIgnoreCase));
            }

            return query.OrderByDescending(r => r.Timestamp).Take(filter.Limit).ToList();
        }
    }

    /// <summary>
    /// Disk retention guard: enforces maximum storage usage and purges oldest historical images/records.
    /// </summary>
    public int EnforceRetentionPolicy(long maxStorageBytes = 500_000_000, int maxDays = 90)
    {
        lock (_lock)
        {
            int purged = 0;
            var cutoff = DateTimeOffset.UtcNow.AddDays(-maxDays);

            // 1. Purge expired images by age
            if (Directory.Exists(_imagesDir))
            {
                var files = Directory.GetFiles(_imagesDir);
                foreach (var f in files)
                {
                    var fileInfo = new FileInfo(f);
                    if (fileInfo.CreationTimeUtc < cutoff.UtcDateTime)
                    {
                        try { fileInfo.Delete(); purged++; } catch { }
                    }
                }
            }

            // 2. Check total storage size
            long totalBytes = CalculateDirectorySize(_storeDir);
            if (totalBytes > maxStorageBytes && Directory.Exists(_imagesDir))
            {
                var imageFiles = Directory.GetFiles(_imagesDir)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.CreationTimeUtc)
                    .ToList();

                foreach (var fi in imageFiles)
                {
                    if (totalBytes <= maxStorageBytes) break;
                    long sz = fi.Length;
                    try
                    {
                        fi.Delete();
                        totalBytes -= sz;
                        purged++;
                    }
                    catch { }
                }
            }

            return purged;
        }
    }

    /// <summary>
    /// Exports a collection of inspection records to a standardized CSV format.
    /// </summary>
    public void ExportToCsv(string filePath, IEnumerable<InspectionRecord> records)
    {
        var sb = new StringBuilder();
        // CSV Header
        sb.AppendLine("SequenceNumber,Id,TimestampUtc,LotNumber,SerialNumber,PartType,RecipeName,PipelineVersion,Verdict,ExecutionTimeMs,FailureReasons,RecordHash,ImagePath");

        foreach (var r in records)
        {
            string reasons = string.Join(";", r.FailureReasons).Replace("\"", "\"\"");
            sb.Append(r.SequenceNumber).Append(',')
              .Append(r.Id).Append(',')
              .Append(r.Timestamp.ToString("o")).Append(',')
              .Append($"\"{r.LotNumber}\",")
              .Append($"\"{r.SerialNumber}\",")
              .Append($"\"{r.PartType}\",")
              .Append($"\"{r.RecipeName}\",")
              .Append($"\"{r.PipelineVersion}\",")
              .Append(r.Verdict).Append(',')
              .Append(r.ExecutionTimeMs.ToString("F2")).Append(',')
              .Append($"\"{reasons}\",")
              .Append(r.RecordHash).Append(',')
              .Append($"\"{r.ImagePath}\"")
              .AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static long CalculateDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f).Length)
                .Sum();
        }
        catch
        {
            return 0;
        }
    }
}
