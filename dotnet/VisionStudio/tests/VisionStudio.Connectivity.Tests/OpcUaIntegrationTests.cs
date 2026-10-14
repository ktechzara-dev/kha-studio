using FluentAssertions;
using VisionStudio.Connectivity.OpcUa;
using VisionStudio.Core.Traceability;
using Xunit;

namespace VisionStudio.Connectivity.Tests;

public class OpcUaIntegrationTests
{
    [Fact]
    public void OpcUaServer_InitializesAddressSpace_AndMapsInspectionResults()
    {
        var server = new OpcUaServerManager();

        server.ReadNodeValue("ns=2;s=Station.Verdict").Should().Be("Pass");
        server.ReadNodeValue("ns=2;s=Station.TotalCount").Should().Be(0L);

        var record = new InspectionRecord
        {
            Verdict = InspectionVerdict.Fail,
            ExecutionTimeMs = 15.5,
            RecipeName = "Vial_10ml",
            Measurements = new() { ["Height"] = 45.2, ["CapTilt"] = 1.2 }
        };

        server.UpdateFromInspection(record);

        server.ReadNodeValue("ns=2;s=Station.Verdict").Should().Be("Fail");
        server.ReadNodeValue("ns=2;s=Station.TotalCount").Should().Be(1L);
        server.ReadNodeValue("ns=2;s=Station.FailCount").Should().Be(1L);
        server.ReadNodeValue("ns=2;s=Station.CycleTimeMs").Should().Be(15.5);
        server.ReadNodeValue("ns=2;s=Station.ActiveRecipe").Should().Be("Vial_10ml");

        // Verify dynamic measurement nodes
        server.ReadNodeValue("ns=2;s=Measurements.Height").Should().Be(45.2);
        server.ReadNodeValue("ns=2;s=Measurements.CapTilt").Should().Be(1.2);
    }

    [Fact]
    public async Task OpcUaClient_ReadsAndWritesNodes_InProcess()
    {
        var server = new OpcUaServerManager();
        await using var client = new OpcUaClientConnector(server);

        // Read node
        string? status = await client.ReadNodeAsync<string>("ns=2;s=Station.Status");
        status.Should().Be("Idle");

        // Write command node
        bool written = await client.WriteNodeAsync("ns=2;s=Commands.Trigger", true);
        written.Should().BeTrue();

        // Verify server received updated value
        server.ReadNodeValue("ns=2;s=Commands.Trigger").Should().Be(true);
    }
}
