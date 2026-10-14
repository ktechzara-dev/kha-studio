using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using VisionStudio.Core.Traceability;

namespace VisionStudio.Runtime.Tests;

public class RuntimeApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RuntimeApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkAndHealthyStatus()
    {
        var response = await _client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"status\":\"Healthy\"");
    }

    [Fact]
    public async Task GetReady_ReturnsOk()
    {
        var response = await _client.GetAsync("/ready");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"ready\":true");
    }

    [Fact]
    public async Task GetMetrics_ReturnsPrometheusText()
    {
        var response = await _client.GetAsync("/metrics");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("visionstudio_uptime_seconds");
        text.Should().Contain("visionstudio_stations_count");
    }

    [Fact]
    public async Task GetStatus_ReturnsRuntimeState()
    {
        var response = await _client.GetAsync("/api/v1/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"status\":\"Running\"");
    }

    [Fact]
    public async Task GetStations_ReturnsStationsArray()
    {
        var response = await _client.GetAsync("/api/v1/stations");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("Station_01");
    }

    [Fact]
    public async Task PostTrigger_ExecutesInspectionAndReturnsRecord()
    {
        var triggerResponse = await _client.PostAsJsonAsync("/api/v1/stations/Station_01/trigger", new { });
        triggerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var record = await triggerResponse.Content.ReadFromJsonAsync<InspectionRecord>();
        record.Should().NotBeNull();
        record!.Verdict.Should().Be(InspectionVerdict.Pass);

        // Fetch last result endpoint
        var resultResponse = await _client.GetAsync("/api/v1/stations/Station_01/result");
        resultResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Fetch last image endpoint
        var imgResponse = await _client.GetAsync("/api/v1/stations/Station_01/image");
        imgResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        imgResponse.Content.Headers.ContentType?.MediaType.Should().Be("image/jpeg");
    }
}
