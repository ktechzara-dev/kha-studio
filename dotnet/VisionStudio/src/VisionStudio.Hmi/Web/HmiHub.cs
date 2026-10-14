using Microsoft.AspNetCore.SignalR;
using VisionStudio.Hmi.Engine;
using VisionStudio.Hmi.Model;

namespace VisionStudio.Hmi.Web;

/// <summary>
/// SignalR real-time duplex communication hub connecting browser tablets and operator panels to the machine engine.
/// </summary>
public sealed class HmiHub : Hub
{
    private readonly IHmiRuntimeEngine _engine;

    public HmiHub(IHmiRuntimeEngine engine)
    {
        _engine = engine;
    }

    public override async Task OnConnectedAsync()
    {
        // Send initial page snapshot on connection
        var snapshot = _engine.GetPageSnapshot();
        await Clients.Caller.SendAsync("ReceivePageSnapshot", snapshot);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Switch active screen page.
    /// </summary>
    public async Task ChangePage(string pageId)
    {
        _engine.CurrentPageId = pageId;
        var snapshot = _engine.GetPageSnapshot(pageId);
        await Clients.Caller.SendAsync("ReceivePageSnapshot", snapshot);
    }

    /// <summary>
    /// Dispatches a widget action from the web touch UI to the engine.
    /// </summary>
    public async Task TriggerAction(string widgetId, string actionName, string payload)
    {
        await _engine.ExecuteActionAsync(widgetId, actionName, payload);
    }

    /// <summary>
    /// Switches the logged-in user role from the Web UI.
    /// </summary>
    public async Task Login(string username, string roleName)
    {
        if (Enum.TryParse<HmiRole>(roleName, true, out var role))
        {
            _engine.CurrentUsername = username;
            _engine.CurrentRole = role;
            _engine.LogMessage("AUTH", $"Web HMI client đăng nhập: [{username}] với quyền [{role}]");
        }

        var snapshot = _engine.GetPageSnapshot();
        await Clients.All.SendAsync("ReceivePageSnapshot", snapshot);
    }
}
