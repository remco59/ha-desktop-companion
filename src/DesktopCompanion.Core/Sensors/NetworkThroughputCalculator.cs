using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Core.Sensors;

/// <summary>Calculates network byte rates from cumulative adapter counters.</summary>
public static class NetworkThroughputCalculator
{
    public static NetworkThroughput? Calculate(
        NetworkCounterSnapshot previous,
        NetworkCounterSnapshot current)
    {
        if (previous.BytesReceived < 0 || previous.BytesSent < 0 ||
            current.BytesReceived < previous.BytesReceived ||
            current.BytesSent < previous.BytesSent ||
            current.RecordedAt <= previous.RecordedAt)
            return null;

        var elapsedSeconds = (current.RecordedAt - previous.RecordedAt).TotalSeconds;
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0)
            return null;

        var receive = (current.BytesReceived - previous.BytesReceived) / elapsedSeconds;
        var send = (current.BytesSent - previous.BytesSent) / elapsedSeconds;
        if (!double.IsFinite(receive) || !double.IsFinite(send) || receive < 0 || send < 0)
            return null;

        return new NetworkThroughput(receive, send);
    }
}
