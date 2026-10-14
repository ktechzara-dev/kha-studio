using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VisionStudio.Core.Traceability;

public enum InspectionVerdict
{
    Pass = 0,
    Fail = 1,
    Warning = 2
}

/// <summary>
/// Immutable record of an individual machine vision inspection event with full traceability.
/// Includes a cryptographic SHA-256 hash chain link to detect data tampering.
/// </summary>
public sealed class InspectionRecord
{
    public long SequenceNumber { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string LotNumber { get; set; } = "UNKNOWN_LOT";
    public string SerialNumber { get; set; } = "UNKNOWN_SN";
    public string PartType { get; set; } = "Part";
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string PipelineVersion { get; set; } = "1.0.0";
    public InspectionVerdict Verdict { get; set; } = InspectionVerdict.Pass;
    public double ExecutionTimeMs { get; set; }
    public Dictionary<string, double> Measurements { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> FailureReasons { get; set; } = new();
    public string ImagePath { get; set; } = string.Empty;
    public string ThumbnailBase64 { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the immediately preceding record, forming a tamper-evident blockchain/hash chain.
    /// </summary>
    public string PreviousRecordHash { get; set; } = "GENESIS";

    /// <summary>
    /// Computed cryptographic hash seal of this record's payload combined with PreviousRecordHash.
    /// </summary>
    public string RecordHash { get; set; } = string.Empty;

    public string ComputePayloadHash(string previousHash)
    {
        // Build deterministic canonical string
        var sb = new StringBuilder();
        sb.Append(previousHash).Append('|')
          .Append(SequenceNumber).Append('|')
          .Append(Id).Append('|')
          .Append(Timestamp.ToUnixTimeMilliseconds()).Append('|')
          .Append(LotNumber).Append('|')
          .Append(SerialNumber).Append('|')
          .Append(RecipeId).Append('|')
          .Append((int)Verdict).Append('|')
          .Append(ExecutionTimeMs.ToString("F3")).Append('|');

        foreach (var kv in Measurements.OrderBy(m => m.Key))
        {
            sb.Append(kv.Key).Append(':').Append(kv.Value.ToString("F4")).Append(';');
        }

        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>
/// Filter criteria for querying historical inspection records.
/// </summary>
public sealed class TraceFilter
{
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public InspectionVerdict? Verdict { get; set; }
    public string? RecipeId { get; set; }
    public int Limit { get; set; } = 1000;
}

public sealed record HashChainValidationResult(
    bool IsValid,
    int TotalRecords,
    int? TamperedIndex,
    string? Message);
