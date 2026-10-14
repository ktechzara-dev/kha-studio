using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using VisionStudio.Runtime.Packaging;

namespace VisionStudio.Runtime.Tests;

public class PackagingTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "vspkg_tests_" + Guid.NewGuid().ToString("N"));

    public PackagingTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, true); } catch { }
        }
    }

    [Fact]
    public void PackagePacker_CreatesValidZipWithManifestAndChecksum()
    {
        string srcDir = Path.Combine(_tempRoot, "src_project");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "pipeline.json"), "{\"nodes\": []}");
        File.WriteAllText(Path.Combine(srcDir, "recipe.json"), "{\"name\": \"default\"}");

        var manifest = new PackageManifest
        {
            Name = "AutomotiveInspection",
            Version = "1.2.0",
            Stations = new List<StationMappingEntry>
            {
                new() { StationId = "Head_01", PipelineFile = "pipeline.json" }
            }
        };

        string outPkg = Path.Combine(_tempRoot, "project.vspkg");
        PackagePacker.CreatePackage(srcDir, outPkg, manifest, "secret_key_123");

        File.Exists(outPkg).Should().BeTrue();

        // Inspect package
        var deployer = new PackageDeployer(Path.Combine(_tempRoot, "runtime"));
        var (inspectedManifest, diff) = deployer.InspectPackage(outPkg, "secret_key_123");

        inspectedManifest.Name.Should().Be("AutomotiveInspection");
        inspectedManifest.Version.Should().Be("1.2.0");
        inspectedManifest.ChecksumSha256.Should().NotBeNullOrEmpty();
        inspectedManifest.Signature.Should().NotBeNullOrEmpty();
        diff.AddedFiles.Should().Contain("pipeline.json");
    }

    [Fact]
    public void PackageDeployer_ThrowsOnTamperedPackage()
    {
        string srcDir = Path.Combine(_tempRoot, "src_project");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "pipeline.json"), "{\"nodes\": []}");

        var manifest = new PackageManifest { Name = "TamperTest", Version = "1.0.0" };
        string outPkg = Path.Combine(_tempRoot, "tampered.vspkg");
        PackagePacker.CreatePackage(srcDir, outPkg, manifest);

        // Tamper with the zip archive by modifying file content
        using (var archive = ZipFile.Open(outPkg, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("pipeline.json");
            entry?.Delete();
            var newEntry = archive.CreateEntry("pipeline.json");
            using var writer = new StreamWriter(newEntry.Open());
            writer.Write("{\"tampered\": true}");
        }

        var deployer = new PackageDeployer(Path.Combine(_tempRoot, "runtime"));
        var act = () => deployer.InspectPackage(outPkg);
        act.Should().Throw<CryptographicException>()
            .WithMessage("*Checksum mismatch*");
    }

    [Fact]
    public void PackageDeployer_DeploysPackageAndReportsDiff()
    {
        string runtimeDir = Path.Combine(_tempRoot, "runtime");
        var deployer = new PackageDeployer(runtimeDir);

        // Version 1
        string v1Dir = Path.Combine(_tempRoot, "v1");
        Directory.CreateDirectory(v1Dir);
        File.WriteAllText(Path.Combine(v1Dir, "pipeline.json"), "{\"v\": 1}");
        var m1 = new PackageManifest { Name = "P1", Version = "1.0.0" };
        string pkg1 = Path.Combine(_tempRoot, "p1.vspkg");
        PackagePacker.CreatePackage(v1Dir, pkg1, m1);

        string active1 = deployer.DeployPackage(pkg1);
        File.Exists(Path.Combine(active1, "pipeline.json")).Should().BeTrue();
        deployer.GetActiveManifest()?.Version.Should().Be("1.0.0");

        // Version 2 (with additional file)
        string v2Dir = Path.Combine(_tempRoot, "v2");
        Directory.CreateDirectory(v2Dir);
        File.WriteAllText(Path.Combine(v2Dir, "pipeline.json"), "{\"v\": 2}");
        File.WriteAllText(Path.Combine(v2Dir, "model.onnx"), "dummy_onnx");
        var m2 = new PackageManifest { Name = "P1", Version = "2.0.0" };
        string pkg2 = Path.Combine(_tempRoot, "p2.vspkg");
        PackagePacker.CreatePackage(v2Dir, pkg2, m2);

        var (inspectM2, diff) = deployer.InspectPackage(pkg2);
        diff.OldVersion.Should().Be("1.0.0");
        diff.NewVersion.Should().Be("2.0.0");
        diff.AddedFiles.Should().Contain("model.onnx");
        diff.ModifiedFiles.Should().Contain("pipeline.json");

        string active2 = deployer.DeployPackage(pkg2);
        deployer.GetActiveManifest()?.Version.Should().Be("2.0.0");
        File.Exists(Path.Combine(active2, "model.onnx")).Should().BeTrue();
    }
}
