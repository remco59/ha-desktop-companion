namespace DesktopCompanion.Core.Models;

public sealed class HomeAssistantEventReceivedEventArgs : EventArgs
{
    public HomeAssistantEventReceivedEventArgs(HomeAssistantEvent homeAssistantEvent)
    {
        HomeAssistantEvent = homeAssistantEvent;
    }

    public HomeAssistantEvent HomeAssistantEvent { get; }
}
