namespace VisionStudio.DeepLearning.Datasets;

public enum DatasetSplit
{
    Train = 0,
    Validation = 1,
    Test = 2
}

public sealed record DatasetAnnotation(
    string Label,
    double X = 0,
    double Y = 0,
    double Width = 0,
    double Height = 0,
    IReadOnlyList<(double X, double Y)>? Polygon = null);

public sealed class DatasetItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public DatasetSplit Split { get; set; } = DatasetSplit.Train;
    public string PrimaryLabel { get; set; } = "Unlabeled";
    public List<DatasetAnnotation> Annotations { get; set; } = new();
    public string Sha256Checksum { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }

    public override string ToString() => $"{FileName} [{PrimaryLabel}, {Split}]";
}

public sealed record ClassDistribution(
    string Label,
    int TotalCount,
    int TrainCount,
    int ValCount,
    int TestCount,
    double Percentage);
