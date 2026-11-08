namespace DesktopCompanion.Core.Models;

public enum HomeAssistantConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    AuthenticationRequired,
    Faulted
}
