namespace VisionStudio.Runtime.Packaging;

public sealed class PackageDiff
{
    public string OldVersion { get; set; } = "None";
    public string NewVersion { get; set; } = string.Empty;
    public List<string> AddedFiles { get; set; } = new();
    public List<string> RemovedFiles { get; set; } = new();
    public List<string> ModifiedFiles { get; set; } = new();
    public List<string> AddedStations { get; set; } = new();
    public List<string> RemovedStations { get; set; } = new();
    public List<string> ModelChanges { get; set; } = new();

    public bool HasBreakingChanges => RemovedStations.Count > 0 || RemovedFiles.Count > 0;
    public int TotalChanges => AddedFiles.Count + RemovedFiles.Count + ModifiedFiles.Count;

    public string Summarize()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Package Upgrade Diff] {OldVersion} -> {NewVersion}");
        if (AddedStations.Count > 0) sb.AppendLine($"  + Stations Added: {string.Join(", ", AddedStations)}");
        if (RemovedStations.Count > 0) sb.AppendLine($"  - Stations Removed: {string.Join(", ", RemovedStations)}");
        if (AddedFiles.Count > 0) sb.AppendLine($"  + Files Added: {AddedFiles.Count} files");
        if (ModifiedFiles.Count > 0) sb.AppendLine($"  ~ Files Modified: {ModifiedFiles.Count} files");
        if (RemovedFiles.Count > 0) sb.AppendLine($"  - Files Removed: {RemovedFiles.Count} files");
        if (ModelChanges.Count > 0) sb.AppendLine($"  * Models: {string.Join("; ", ModelChanges)}");
        return sb.ToString().TrimEnd();
    }
}
