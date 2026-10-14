using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VisionStudio.Hmi.Engine;
using VisionStudio.Hmi.Model;
using VisionStudio.Hmi.Web;
using Xunit;

namespace VisionStudio.Hmi.Tests;

public class WebHmiServerTests
{
    [Fact]
    public async Task WebHmiServer_ServesEndpointsAndExecutesActions()
    {
        var engine = new HmiRuntimeEngine();
        // Use a dedicated loopback port for testing
        int testPort = 5892;
        var server = new WebHmiServer(engine, testPort, "127.0.0.1");

        try
        {
            await server.StartAsync();
            Assert.True(server.IsRunning);

            using var http = new HttpClient();
            var baseUrl = server.ServerUrl;

            // 1. Check Root HTML Dashboard
            var rootResp = await http.GetAsync($"{baseUrl}/");
            Assert.Equal(HttpStatusCode.OK, rootResp.StatusCode);
            var html = await rootResp.Content.ReadAsStringAsync();
            Assert.Contains("VisionStudio Web HMI", html);

            // 2. Check /health
            var healthResp = await http.GetAsync($"{baseUrl}/health");
            Assert.Equal(HttpStatusCode.OK, healthResp.StatusCode);
            var healthJson = await healthResp.Content.ReadAsStringAsync();
            Assert.Contains("Healthy", healthJson);

            // 3. Check /api/hmi/layout
            var layoutResp = await http.GetAsync($"{baseUrl}/api/hmi/layout");
            Assert.Equal(HttpStatusCode.OK, layoutResp.StatusCode);
            var layout = await layoutResp.Content.ReadFromJsonAsync<HmiProject>();
            Assert.NotNull(layout);
            Assert.Equal(engine.CurrentProject.Title, layout.Title);

            // 4. Check /api/hmi/snapshot
            var snapResp = await http.GetAsync($"{baseUrl}/api/hmi/snapshot");
            Assert.Equal(HttpStatusCode.OK, snapResp.StatusCode);
            var snap = await snapResp.Content.ReadFromJsonAsync<HmiPageSnapshot>();
            Assert.NotNull(snap);
            Assert.True(snap.Widgets.Count > 0);

            // 5. Send Action via POST
            string? capturedAction = null;
            engine.ActionTriggered += (act, _) => capturedAction = act;

            var actionReq = new HmiActionRequest("btn-test", "TriggerStation", "cam-01");
            var postResp = await http.PostAsJsonAsync($"{baseUrl}/api/hmi/action", actionReq);
            Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
            Assert.Equal("TriggerStation", capturedAction);
        }
        finally
        {
            await server.StopAsync();
            Assert.False(server.IsRunning);
        }
    }
}
