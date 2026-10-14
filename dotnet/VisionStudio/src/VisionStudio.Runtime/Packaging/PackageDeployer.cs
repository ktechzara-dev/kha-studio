using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace VisionStudio.Runtime.Packaging;

/// <summary>
/// Unpacks, verifies cryptographic checksums, diffs, and activates .vspkg packages in the runtime.
/// </summary>
public sealed class PackageDeployer
{
    private readonly string _deploymentBaseDir;
    private readonly ILogger? _logger;

    public PackageDeployer(string deploymentBaseDir, ILogger? logger = null)
    {
        _deploymentBaseDir = deploymentBaseDir;
        _logger = logger;
        if (!Directory.Exists(_deploymentBaseDir))
        {
            Directory.CreateDirectory(_deploymentBaseDir);
        }
    }

    /// <summary>
    /// Inspects a package without activating it, verifying integrity and returning manifest and diff.
    /// </summary>
    public (PackageManifest Manifest, PackageDiff Diff) InspectPackage(string packagePath, string? expectedSecret = null)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException($"Package file '{packagePath}' not found.");
        }

        string tempUnpack = Path.Combine(Path.GetTempPath(), "vspkg_inspect_" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(packagePath, tempUnpack, true);
            string manifestPath = Path.Combine(tempUnpack, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidDataException("Invalid .vspkg: missing manifest.json");
            }

            string manifestJson = File.ReadAllText(manifestPath, Encoding.UTF8);
            var manifest = PackageManifest.FromJson(manifestJson)
                ?? throw new InvalidDataException("Failed to parse package manifest.");

            // Verify checksum
            string actualChecksum = PackagePacker.ComputeDirectoryChecksum(tempUnpack);
            if (!string.Equals(manifest.ChecksumSha256, actualChecksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicException($"Checksum mismatch! Manifest: {manifest.ChecksumSha256}, Actual: {actualChecksum}");
            }

            // Verify signature if secret provided
            if (!string.IsNullOrEmpty(expectedSecret) && !string.IsNullOrEmpty(manifest.Signature))
            {
                string expectedSig = PackagePacker.SignChecksum(actualChecksum, expectedSecret);
                if (!string.Equals(expectedSig, manifest.Signature, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CryptographicException("Cryptographic signature verification failed! Package was tampered with.");
                }
            }

            // Diff against currently active version if exists
            var diff = ComputeDiffAgainstActive(tempUnpack, manifest);
            return (manifest, diff);
        }
        finally
        {
            if (Directory.Exists(tempUnpack))
            {
                Directory.Delete(tempUnpack, true);
            }
        }
    }

    /// <summary>
    /// Extracts and activates the package in the runtime directory.
    /// Returns the active deployed directory.
    /// </summary>
    public string DeployPackage(string packagePath, string? expectedSecret = null)
    {
        var (manifest, diff) = InspectPackage(packagePath, expectedSecret);
        _logger?.LogInformation("Deploying package '{Name}' v{Version}: {Summary}",
            manifest.Name, manifest.Version, diff.Summarize());

        string targetDir = Path.Combine(_deploymentBaseDir, "active");
        string backupDir = Path.Combine(_deploymentBaseDir, "previous");

        // Backup existing active if present
        if (Directory.Exists(targetDir))
        {
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, true);
            }
            Directory.Move(targetDir, backupDir);
        }

        Directory.CreateDirectory(targetDir);
        ZipFile.ExtractToDirectory(packagePath, targetDir, true);

        // Also save archive in versions history
        string archiveStore = Path.Combine(_deploymentBaseDir, "packages");
        Directory.CreateDirectory(archiveStore);
        string versionedArchive = Path.Combine(archiveStore, $"{manifest.Name}_{manifest.Version}.vspkg");
        File.Copy(packagePath, versionedArchive, true);

        _logger?.LogInformation("Package successfully deployed to '{TargetDir}'", targetDir);
        return targetDir;
    }

    public PackageManifest? GetActiveManifest()
    {
        string manifestPath = Path.Combine(_deploymentBaseDir, "active", "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        try
        {
            string json = File.ReadAllText(manifestPath, Encoding.UTF8);
            return PackageManifest.FromJson(json);
        }
        catch
        {
            return null;
        }
    }

    private PackageDiff ComputeDiffAgainstActive(string newUnpackDir, PackageManifest newManifest)
    {
        string activeDir = Path.Combine(_deploymentBaseDir, "active");
        var diff = new PackageDiff
        {
            NewVersion = newManifest.Version
        };

        if (!Directory.Exists(activeDir))
        {
            diff.OldVersion = "None";
            diff.AddedStations = newManifest.Stations.Select(s => s.StationId).ToList();
            diff.AddedFiles = Directory.GetFiles(newUnpackDir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(newUnpackDir, f)).ToList();
            return diff;
        }

        var activeManifest = GetActiveManifest();
        diff.OldVersion = activeManifest?.Version ?? "Unknown";

        var oldFiles = Directory.GetFiles(activeDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(activeDir, f)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newFiles = Directory.GetFiles(newUnpackDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(newUnpackDir, f)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        diff.AddedFiles = newFiles.Except(oldFiles).ToList();
        diff.RemovedFiles = oldFiles.Except(newFiles).ToList();

        foreach (var common in newFiles.Intersect(oldFiles))
        {
            byte[] b1 = File.ReadAllBytes(Path.Combine(activeDir, common));
            byte[] b2 = File.ReadAllBytes(Path.Combine(newUnpackDir, common));
            if (!b1.SequenceEqual(b2))
            {
                diff.ModifiedFiles.Add(common);
            }
        }

        if (activeManifest != null)
        {
            var oldStations = activeManifest.Stations.Select(s => s.StationId).ToHashSet();
            var newStations = newManifest.Stations.Select(s => s.StationId).ToHashSet();
            diff.AddedStations = newStations.Except(oldStations).ToList();
            diff.RemovedStations = oldStations.Except(newStations).ToList();
        }

        return diff;
    }
}
