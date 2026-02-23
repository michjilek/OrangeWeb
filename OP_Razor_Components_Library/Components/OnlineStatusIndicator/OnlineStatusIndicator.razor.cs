using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OP_Shared_Library.Notify;
using OP_Shared_Library.Notify.Interface;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace OP_Razor_Components_Library.Components.OnlineStatusIndicator;
public partial class OnlineStatusIndicator
{
    #region Injected
    [Inject]
    public INotifier _notifier { get; set; }

    [Inject]
    public IJSRuntime _jsRuntime { get; set; }
    #endregion

    #region Parameters
    [Parameter] public bool IsOnline { get; set; }

    [Parameter] public EventCallback<bool> IsOnlineChanged { get; set; }
    #endregion

    #region Constants
    const string InitializeInvokeName = "OnlineStatus.Initialize";
    const string DisposeInvokeName = "OnlineStatus.Dispose";
    const string GetOnlineStatusName = "OnlineStatus.JSGetOnlineStatus";
    #endregion

    #region Ctor
    protected override async Task OnInitializedAsync()
    {
        // When this object initialize, invoke method "Initialize" in javascript (online-status.js),
        // which set handler, which always send (to OnStatusChanged method) information
        // about online/offline state from "window"

        var interop = DotNetObjectReference.Create(this);

        // Set online/offline status changed handler
        await _jsRuntime.InvokeVoidAsync(InitializeInvokeName, interop);

        // Give me information about online/offline state once for first app start
        await _jsRuntime.InvokeVoidAsync(GetOnlineStatusName, interop);
    }
    #endregion

    #region Public Methods
    public async ValueTask DisposeAsync()
    {
        await _jsRuntime.InvokeVoidAsync(DisposeInvokeName);
    }
    #endregion

    #region [JSInvokable]
    // Get online status, when changed
    [JSInvokable("OnlineStatus.StatusChanged")]
    public async Task OnStatusChanged(bool isOnline)
    {
        if (IsOnline != isOnline)
        {
            // Save state
            IsOnline = isOnline;

            // Inform others via parameter
            await TellToOtherViaEventParameter(IsOnline);
        }

        // Inform others via Notifier
        var message = new NotifyMessage(NotifyMessageType.onlineState, IsOnline);
        _notifier.Notify(message);
    }

    [JSInvokable("OnlineStatus.GetOnlineStatus")]
    public async Task GetOnlineStatus(bool isOnline)
    {
        IsOnline = isOnline;

        await TellToOtherViaEventParameter(IsOnline);
    }
    #endregion

    #region Private Methods
    // Raise event to inform others
    private async Task TellToOtherViaEventParameter(bool isOnline)
    {
        await IsOnlineChanged.InvokeAsync(isOnline);
    }
    #endregion
}