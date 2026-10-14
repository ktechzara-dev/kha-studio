using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace VisionStudio.Runtime.Packaging;

/// <summary>
/// Packages vision pipelines, models, calibrations, recipes, and manifests into a signed .vspkg bundle.
/// </summary>
public sealed class PackagePacker
{
    /// <summary>
    /// Creates a .vspkg zip archive from a source directory containing project assets.
    /// </summary>
    public static string CreatePackage(string sourceDir, string outputPackagePath, PackageManifest manifest, string? signingSecret = null)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory '{sourceDir}' not found.");
        }

        var outDir = Path.GetDirectoryName(outputPackagePath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        if (File.Exists(outputPackagePath))
        {
            File.Delete(outputPackagePath);
        }

        // Temporary build folder for archive assembly
        string tempBuildDir = Path.Combine(Path.GetTempPath(), "vspkg_build_" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(sourceDir, tempBuildDir);

            // Compute hash of existing payload files
            string payloadChecksum = ComputeDirectoryChecksum(tempBuildDir);
            manifest.ChecksumSha256 = payloadChecksum;

            if (!string.IsNullOrEmpty(signingSecret))
            {
                manifest.Signature = SignChecksum(payloadChecksum, signingSecret);
            }

            // Write manifest.json
            string manifestPath = Path.Combine(tempBuildDir, "manifest.json");
            File.WriteAllText(manifestPath, manifest.ToJson(), Encoding.UTF8);

            // Compress to .vspkg
            ZipFile.CreateFromDirectory(tempBuildDir, outputPackagePath, CompressionLevel.Optimal, false);
            return outputPackagePath;
        }
        finally
        {
            if (Directory.Exists(tempBuildDir))
            {
                Directory.Delete(tempBuildDir, true);
            }
        }
    }

    /// <summary>
    /// Computes deterministic SHA-256 hash across all files in a directory sorted by path.
    /// </summary>
    public static string ComputeDirectoryChecksum(string dirPath)
    {
        var files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        using var sha256 = SHA256.Create();
        foreach (var file in files)
        {
            string rel = Path.GetRelativePath(dirPath, file).Replace('\\', '/');
            byte[] relBytes = Encoding.UTF8.GetBytes(rel);
            sha256.TransformBlock(relBytes, 0, relBytes.Length, null, 0);

            byte[] content = File.ReadAllBytes(file);
            sha256.TransformBlock(content, 0, content.Length, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
    }

    public static string SignChecksum(string checksum, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(checksum));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }
}
