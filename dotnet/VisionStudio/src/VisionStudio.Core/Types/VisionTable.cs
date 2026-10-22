using System.Globalization;
using System.Text;

namespace VisionStudio.Core.Types;

public enum TableColumnType
{
    Number,
    String,
    Boolean,
    Object
}

public enum TableAggregateType
{
    Sum,
    Mean,
    Min,
    Max,
    Count,
    StdDev
}

public sealed record TableColumn(string Name, TableColumnType Type);

public sealed class TableRow
{
    private readonly VisionTable _owner;
    private readonly object?[] _values;

    public IReadOnlyList<object?> Values => _values;

    internal TableRow(VisionTable owner, object?[] values)
    {
        _owner = owner;
        _values = values;
    }

    public object? this[string columnName]
    {
        get
        {
            int idx = _owner.GetColumnIndex(columnName);
            return _values[idx];
        }
        set
        {
            int idx = _owner.GetColumnIndex(columnName);
            _values[idx] = value;
        }
    }

    public object? this[int columnIndex]
    {
        get => _values[columnIndex];
        set => _values[columnIndex] = value;
    }

    public double GetDouble(string columnName, double defaultValue = 0.0)
    {
        var val = this[columnName];
        if (val == null) return defaultValue;
        if (val is double d) return d;
        if (val is float f) return f;
        if (val is int i) return i;
        if (val is long l) return l;
        if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return defaultValue;
    }

    public string GetString(string columnName, string defaultValue = "")
    {
        return this[columnName]?.ToString() ?? defaultValue;
    }

    public bool GetBool(string columnName, bool defaultValue = false)
    {
        var val = this[columnName];
        if (val == null) return defaultValue;
        if (val is bool b) return b;
        if (bool.TryParse(val.ToString(), out var parsed)) return parsed;
        return defaultValue;
    }
}

/// <summary>
/// First-class typed tabular dataset with columns, rows, querying, sorting, 
/// joining, aggregations, and CSV serialization.
/// </summary>
public sealed class VisionTable
{
    private readonly List<TableColumn> _columns = new();
    private readonly Dictionary<string, int> _columnIndexMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TableRow> _rows = new();

    public IReadOnlyList<TableColumn> Columns => _columns;
    public IReadOnlyList<TableRow> Rows => _rows;
    public int RowCount => _rows.Count;
    public int ColumnCount => _columns.Count;

    public VisionTable()
    {
    }

    public VisionTable(IEnumerable<TableColumn> columns)
    {
        foreach (var col in columns)
        {
            AddColumn(col.Name, col.Type);
        }
    }

    public VisionTable AddColumn(string name, TableColumnType type = TableColumnType.String)
    {
        if (_columnIndexMap.ContainsKey(name))
        {
            throw new ArgumentException($"Column '{name}' already exists in the table.", nameof(name));
        }

        int index = _columns.Count;
        _columns.Add(new TableColumn(name, type));
        _columnIndexMap[name] = index;
        return this;
    }

    public TableRow AddRow(params object?[] values)
    {
        var rowValues = new object?[_columns.Count];
        for (int i = 0; i < Math.Min(values.Length, _columns.Count); i++)
        {
            rowValues[i] = values[i];
        }

        var row = new TableRow(this, rowValues);
        _rows.Add(row);
        return row;
    }

    public void Clear()
    {
        _rows.Clear();
    }

    public TableRow AddRowFromDictionary(IReadOnlyDictionary<string, object?> dict)
    {
        var rowValues = new object?[_columns.Count];
        foreach (var kvp in dict)
        {
            if (_columnIndexMap.TryGetValue(kvp.Key, out int idx))
            {
                rowValues[idx] = kvp.Value;
            }
        }
        var row = new TableRow(this, rowValues);
        _rows.Add(row);
        return row;
    }

    public int GetColumnIndex(string name)
    {
        if (!_columnIndexMap.TryGetValue(name, out int idx))
        {
            throw new KeyNotFoundException($"Column '{name}' was not found in table.");
        }
        return idx;
    }

    public bool HasColumn(string name) => _columnIndexMap.ContainsKey(name);

    #region Filtering and Sorting

    public VisionTable Filter(Func<TableRow, bool> predicate)
    {
        var result = new VisionTable(_columns);
        foreach (var row in _rows)
        {
            if (predicate(row))
            {
                result.AddRow(row.Values.ToArray());
            }
        }
        return result;
    }

    public VisionTable SortBy(string columnName, bool ascending = true)
    {
        int colIdx = GetColumnIndex(columnName);
        var sortedRows = ascending
            ? _rows.OrderBy(r => r[colIdx] as IComparable).ToList()
            : _rows.OrderByDescending(r => r[colIdx] as IComparable).ToList();

        var result = new VisionTable(_columns);
        foreach (var row in sortedRows)
        {
            result.AddRow(row.Values.ToArray());
        }
        return result;
    }

    #endregion

    #region Aggregation

    public double Aggregate(string columnName, TableAggregateType aggType)
    {
        if (_rows.Count == 0) return 0;

        var numbers = _rows.Select(r => r.GetDouble(columnName)).ToList();

        return aggType switch
        {
            TableAggregateType.Count => numbers.Count,
            TableAggregateType.Sum => numbers.Sum(),
            TableAggregateType.Mean => numbers.Average(),
            TableAggregateType.Min => numbers.Min(),
            TableAggregateType.Max => numbers.Max(),
            TableAggregateType.StdDev => CalculateStdDev(numbers),
            _ => 0
        };
    }

    private static double CalculateStdDev(List<double> values)
    {
        if (values.Count < 2) return 0;
        double avg = values.Average();
        double sumSq = values.Sum(v => (v - avg) * (v - avg));
        return Math.Sqrt(sumSq / (values.Count - 1));
    }

    #endregion

    #region Joining

    public VisionTable Join(VisionTable other, string keyColumn, string otherKeyColumn, bool leftOuter = false)
    {
        var result = new VisionTable();

        // Add columns from this table
        foreach (var col in _columns)
        {
            result.AddColumn(col.Name, col.Type);
        }

        // Add non-conflicting or suffixed columns from other table
        var otherColNames = new List<string>();
        foreach (var col in other.Columns)
        {
            string outName = result.HasColumn(col.Name) ? $"{col.Name}_right" : col.Name;
            result.AddColumn(outName, col.Type);
            otherColNames.Add(outName);
        }

        int myKeyIdx = GetColumnIndex(keyColumn);
        int otherKeyIdx = other.GetColumnIndex(otherKeyColumn);

        // Build lookup map for other table
        var otherLookup = other.Rows
            .Where(r => r[otherKeyIdx] != null)
            .ToLookup(r => r[otherKeyIdx]!.ToString() ?? "");

        foreach (var myRow in _rows)
        {
            string myKeyStr = myRow[myKeyIdx]?.ToString() ?? "";
            var matches = otherLookup[myKeyStr].ToList();

            if (matches.Count > 0)
            {
                foreach (var match in matches)
                {
                    var merged = myRow.Values.Concat(match.Values).ToArray();
                    result.AddRow(merged);
                }
            }
            else if (leftOuter)
            {
                var emptyRight = new object?[other.ColumnCount];
                var merged = myRow.Values.Concat(emptyRight).ToArray();
                result.AddRow(merged);
            }
        }

        return result;
    }

    #endregion

    #region CSV Serialization

    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", _columns.Select(c => EscapeCsv(c.Name))));

        foreach (var row in _rows)
        {
            sb.AppendLine(string.Join(",", row.Values.Select(v => EscapeCsv(v?.ToString() ?? ""))));
        }

        return sb.ToString();
    }

    public static VisionTable FromCsv(string csv)
    {
        var table = new VisionTable();
        var lines = csv.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return table;

        var headers = ParseCsvLine(lines[0]);
        foreach (var header in headers)
        {
            table.AddColumn(header.Trim(), TableColumnType.String);
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = ParseCsvLine(lines[i]);
            table.AddRow(parts.Cast<object?>().ToArray());
        }

        return table;
    }

    private static string EscapeCsv(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '\"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                {
                    current.Append('\"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    #endregion
}
