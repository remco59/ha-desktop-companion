using Xunit;
using DesktopCompanion.Core.Dashboards;

namespace DesktopCompanion.Tests.Dashboards;

public sealed class DashboardUrlComposerTests
{
    [Theory]
    [InlineData("https://ha.example", "", "https://ha.example/?kiosk")]
    [InlineData("https://ha.example", "lovelace", "https://ha.example/lovelace?kiosk")]
    [InlineData("https://ha.example", "lovelace/rooms", "https://ha.example/lovelace/rooms?kiosk")]
    [InlineData("https://ha.example/ha", "lovelace", "https://ha.example/ha/lovelace?kiosk")]
    [InlineData("https://ha.example/ha/", "/lovelace/rooms/", "https://ha.example/ha/lovelace/rooms?kiosk")]
    public void Compose_preserves_base_path_and_adds_dashboard_path(string instance, string path, string expected)
    {
        var result = DashboardUrlComposer.Compose(new Uri(instance), path);
        Assert.Equal(new Uri(expected), result);
    }

    [Fact]
    public void Compose_preserves_existing_query_parameters()
    {
        var result = DashboardUrlComposer.Compose(new Uri("https://ha.example/proxy/?auth=abc&theme=dark"), "lovelace");
        Assert.Equal("?auth=abc&theme=dark&kiosk", result.Query);
        Assert.Equal("/proxy/lovelace", result.AbsolutePath);
    }

    [Fact]
    public void Compose_does_not_duplicate_kiosk_parameter()
    {
        var result = DashboardUrlComposer.Compose(new Uri("https://ha.example/?kiosk"), "lovelace");
        Assert.Equal("?kiosk", result.Query);
    }

    [Fact]
    public void Compose_respects_explicit_kiosk_opt_out()
    {
        var result = DashboardUrlComposer.Compose(new Uri("https://ha.example/?disable_km"), "lovelace");
        Assert.Equal("?disable_km", result.Query);
    }

    [Fact]
    public void Compose_can_disable_kiosk_mode()
    {
        var result = DashboardUrlComposer.Compose(new Uri("https://ha.example/ha/?theme=dark"), "lovelace", false);
        Assert.Equal("?theme=dark", result.Query);
    }

    [Fact]
    public void Compose_rejects_absolute_dashboard_urls()
    {
        Assert.Throws<ArgumentException>(() =>
            DashboardUrlComposer.Compose(new Uri("https://ha.example"), "https://other.example/"));
    }

    [Fact]
    public void Compose_rejects_non_http_instance_urls()
    {
        Assert.Throws<ArgumentException>(() =>
            DashboardUrlComposer.Compose(new Uri("file:///C:/ha"), "lovelace"));
    }
}
