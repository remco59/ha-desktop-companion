namespace DesktopCompanion.Core.Models;

public sealed class HomeAssistantConnectionStateChangedEventArgs : EventArgs
{
    public HomeAssistantConnectionStateChangedEventArgs(
        HomeAssistantConnectionStatus status,
        DateTimeOffset changedAt,
        string? message,
        int reconnectAttempt)
    {
        Status = status;
        ChangedAt = changedAt;
        Message = message;
        ReconnectAttempt = reconnectAttempt;
    }

    public HomeAssistantConnectionStatus Status { get; }

    public DateTimeOffset ChangedAt { get; }

    public string? Message { get; }

    public int ReconnectAttempt { get; }
}
