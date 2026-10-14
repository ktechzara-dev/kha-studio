using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenCvSharp;

namespace VisionStudio.DeepLearning.Datasets;

/// <summary>
/// Manages industrial vision datasets with import, stratified splitting, duplicate detection,
/// class balance tracking, and immutable SHA-256 dataset versioning.
/// </summary>
public sealed class DatasetManager
{
    private readonly List<DatasetItem> _items = new();
    private static readonly string[] ValidExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

    public IReadOnlyList<DatasetItem> Items => _items;
    public int Count => _items.Count;

    /// <summary>
    /// Imports images from a root folder. If organized as sub-folders, subfolder names are used as class labels.
    /// </summary>
    public int ImportFolder(string folderPath, bool subfoldersAsClasses = true)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        int added = 0;
        var dirInfo = new DirectoryInfo(folderPath);

        if (subfoldersAsClasses && dirInfo.GetDirectories().Length > 0)
        {
            foreach (var subDir in dirInfo.GetDirectories())
            {
                string label = subDir.Name;
                foreach (var file in subDir.EnumerateFiles())
                {
                    if (ValidExtensions.Contains(file.Extension.ToLowerInvariant()))
                    {
                        AddItem(file.FullName, label);
                        added++;
                    }
                }
            }
        }
        else
        {
            foreach (var file in dirInfo.EnumerateFiles())
            {
                if (ValidExtensions.Contains(file.Extension.ToLowerInvariant()))
                {
                    AddItem(file.FullName, "Unlabeled");
                    added++;
                }
            }
        }

        return added;
    }

    public int ImportFromFolder(string folderPath, bool subfoldersAsClasses = true) => ImportFolder(folderPath, subfoldersAsClasses);

    public IReadOnlyList<string> GetClasses() => _items.Select(i => i.PrimaryLabel).Distinct().ToList();

    public DatasetItem AddItem(string filePath, string label = "Unlabeled", DatasetSplit split = DatasetSplit.Train)
    {
        string hash = ComputeFileSha256(filePath);
        int w = 0, h = 0;

        try
        {
            using var mat = Cv2.ImRead(filePath, ImreadModes.Unchanged);
            if (!mat.Empty())
            {
                w = mat.Width;
                h = mat.Height;
            }
        }
        catch
        {
            // Fallback for image dimensions
        }

        var item = new DatasetItem
        {
            FilePath = filePath,
            PrimaryLabel = label,
            Split = split,
            Sha256Checksum = hash,
            Width = w,
            Height = h
        };

        _items.Add(item);
        return item;
    }

    public void RemoveItem(string id)
    {
        _items.RemoveAll(i => i.Id == id);
    }

    public void Clear()
    {
        _items.Clear();
    }

    /// <summary>
    /// Performs stratified splitting into Train / Validation / Test sets,
    /// maintaining identical class balance ratios across all partitions.
    /// </summary>
    public void StratifiedSplit(double trainRatio = 0.70, double valRatio = 0.15, double testRatio = 0.15, int seed = 42)
    {
        if (_items.Count == 0) return;

        double sum = trainRatio + valRatio + testRatio;
        trainRatio /= sum;
        valRatio /= sum;
        testRatio /= sum;

        var rng = new Random(seed);
        var groups = _items.GroupBy(i => i.PrimaryLabel);

        foreach (var group in groups)
        {
            var shuffled = group.OrderBy(_ => rng.Next()).ToList();
            int total = shuffled.Count;
            int trainCount = (int)Math.Round(total * trainRatio);
            int valCount = (int)Math.Round(total * valRatio);

            // Ensure at least 1 item in train if available
            if (trainCount == 0 && total > 0) trainCount = 1;

            for (int i = 0; i < total; i++)
            {
                if (i < trainCount)
                {
                    shuffled[i].Split = DatasetSplit.Train;
                }
                else if (i < trainCount + valCount)
                {
                    shuffled[i].Split = DatasetSplit.Validation;
                }
                else
                {
                    shuffled[i].Split = DatasetSplit.Test;
                }
            }
        }
    }

    public void SplitStratified(double trainRatio = 0.70, double valRatio = 0.15, double testRatio = 0.15, int seed = 42)
        => StratifiedSplit(trainRatio, valRatio, testRatio, seed);

    /// <summary>
    /// Detects duplicate images based on exact file SHA-256 checksums.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<DatasetItem>> FindDuplicates()
    {
        return _items
            .GroupBy(i => i.Sha256Checksum)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1)
            .Select(g => (IReadOnlyList<DatasetItem>)g.ToList())
            .ToList();
    }

    /// <summary>
    /// Computes class distribution statistics and percentages across splits.
    /// </summary>
    public IReadOnlyList<ClassDistribution> GetClassDistribution()
    {
        int totalAll = _items.Count;
        if (totalAll == 0) return Array.Empty<ClassDistribution>();

        return _items
            .GroupBy(i => i.PrimaryLabel)
            .Select(g => new ClassDistribution(
                Label: g.Key,
                TotalCount: g.Count(),
                TrainCount: g.Count(i => i.Split == DatasetSplit.Train),
                ValCount: g.Count(i => i.Split == DatasetSplit.Validation),
                TestCount: g.Count(i => i.Split == DatasetSplit.Test),
                Percentage: Math.Round((double)g.Count() / totalAll * 100.0, 1)))
            .OrderByDescending(c => c.TotalCount)
            .ToList();
    }

    /// <summary>
    /// Computes a deterministic version hash representing this dataset state.
    /// Any change in files, labels, or splits alters this hash.
    /// </summary>
    public string ComputeDatasetVersionHash()
    {
        if (_items.Count == 0) return "empty";

        var sb = new StringBuilder();
        foreach (var item in _items.OrderBy(i => i.Id))
        {
            sb.Append($"{item.Sha256Checksum}:{item.PrimaryLabel}:{item.Split};");
        }

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    private static string ComputeFileSha256(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}
