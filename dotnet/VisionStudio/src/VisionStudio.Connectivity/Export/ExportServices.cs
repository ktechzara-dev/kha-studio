using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Connectivity.Export;

/// <summary>
/// Dispatches real-time inspection results and defect alerts to factory MES, ERP, or cloud endpoints via REST Webhooks.
/// </summary>
public sealed class WebhookExporter
{
    private readonly HttpClient _httpClient;

    public WebhookExporter(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<bool> SendWebhookAsync(
        string endpointUrl,
        InspectionRecord record,
        string? apiKey = null,
        int timeoutSeconds = 5,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        string json = JsonSerializer.Serialize(record);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Formats and exports inspection results into customizable JSON, XML, or delimited text files.
/// </summary>
public static class FileTemplateExporter
{
    public static void ExportToJson(string filePath, InspectionRecord record)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    public static void ExportToCustomCsv(string filePath, IEnumerable<InspectionRecord> records, string delimiter = ",")
    {
        var sb = new StringBuilder();
        sb.Append("Seq").Append(delimiter)
          .Append("Timestamp").Append(delimiter)
          .Append("Lot").Append(delimiter)
          .Append("Serial").Append(delimiter)
          .Append("Verdict").Append(delimiter)
          .Append("ExecutionMs")
          .AppendLine();

        foreach (var r in records)
        {
            sb.Append(r.SequenceNumber).Append(delimiter)
              .Append(r.Timestamp.ToString("o")).Append(delimiter)
              .Append(r.LotNumber).Append(delimiter)
              .Append(r.SerialNumber).Append(delimiter)
              .Append(r.Verdict).Append(delimiter)
              .Append(r.ExecutionTimeMs.ToString("F2"))
              .AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
}
