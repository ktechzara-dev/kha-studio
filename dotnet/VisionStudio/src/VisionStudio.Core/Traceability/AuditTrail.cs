using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VisionStudio.Core.Traceability;

public sealed class AuditEvent
{
    public long Sequence { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string User { get; set; } = "Operator";
    public string Action { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string PreviousHash { get; set; } = "GENESIS";
    public string EventHash { get; set; } = string.Empty;

    public string ComputeHash(string prevHash)
    {
        string raw = $"{prevHash}|{Sequence}|{Timestamp.ToUnixTimeMilliseconds()}|{User}|{Action}|{Target}|{Details}";
        using var sha256 = SHA256.Create();
        byte[] b = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(b).ToLowerInvariant();
    }
}

/// <summary>
/// Tamper-evident audit trail for system parameter modifications, recipe activations, and user actions.
/// </summary>
public sealed class AuditTrail
{
    private readonly string _auditFilePath;
    private readonly List<AuditEvent> _events = new();
    private readonly object _lock = new();

    public IReadOnlyList<AuditEvent> Events
    {
        get
        {
            lock (_lock) return _events.ToList();
        }
    }

    public AuditTrail(string? filePath = null)
    {
        _auditFilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VisionStudio", "Traceability", "audit_log.jsonl");

        var dir = Path.GetDirectoryName(_auditFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        LoadEvents();
    }

    private void LoadEvents()
    {
        lock (_lock)
        {
            _events.Clear();
            if (!File.Exists(_auditFilePath)) return;

            foreach (var line in File.ReadLines(_auditFilePath))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var ev = JsonSerializer.Deserialize<AuditEvent>(line);
                    if (ev != null) _events.Add(ev);
                }
                catch { }
            }
        }
    }

    public AuditEvent LogAction(string action, string target, string details, string user = "Operator")
    {
        lock (_lock)
        {
            var ev = new AuditEvent
            {
                Sequence = _events.Count + 1,
                Timestamp = DateTimeOffset.UtcNow,
                User = user,
                Action = action,
                Target = target,
                Details = details
            };

            string prev = _events.Count > 0 ? _events[^1].EventHash : "GENESIS";
            ev.PreviousHash = prev;
            ev.EventHash = ev.ComputeHash(prev);

            _events.Add(ev);
            File.AppendAllText(_auditFilePath, JsonSerializer.Serialize(ev) + Environment.NewLine, Encoding.UTF8);

            return ev;
        }
    }

    public bool VerifyIntegrity()
    {
        lock (_lock)
        {
            string expected = "GENESIS";
            foreach (var ev in _events)
            {
                if (ev.PreviousHash != expected) return false;
                if (ev.ComputeHash(expected) != ev.EventHash) return false;
                expected = ev.EventHash;
            }
            return true;
        }
    }
}
