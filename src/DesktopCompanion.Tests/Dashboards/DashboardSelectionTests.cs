using Xunit;
using DesktopCompanion.Core.Dashboards;
using DesktopCompanion.Core.Models;

namespace DesktopCompanion.Tests.Dashboards;

public sealed class DashboardSelectionTests
{
    [Fact]
    public void Normalize_supplies_default_when_empty()
    {
        var result = DashboardSelection.Normalize([]);
        Assert.Single(result);
        Assert.True(result[0].IsDefault);
    }

    [Fact]
    public void Normalize_sorts_by_order_then_name()
    {
        var values = new[]
        {
            new DashboardDefinition("two", "Second", "lovelace/2", SortOrder: 2),
            new DashboardDefinition("one", "First", "lovelace/1", SortOrder: 1),
            new DashboardDefinition("zero", "Zero", "lovelace/0", SortOrder: 1, IsDefault: true)
        };

        var result = DashboardSelection.Normalize(values);
        Assert.Equal(["one", "zero", "two"], result.Select(x => x.Id));
        Assert.Equal("zero", result.Single(x => x.IsDefault).Id);
    }

    [Fact]
    public void Select_returns_persisted_dashboard_when_available()
    {
        var values = new[]
        {
            new DashboardDefinition("default", "Home", "", IsDefault: true),
            new DashboardDefinition("rooms", "Rooms", "lovelace/rooms")
        };

        Assert.Equal("rooms", DashboardSelection.Select(values, "rooms").Id);
    }

    [Fact]
    public void Select_falls_back_to_default_when_saved_dashboard_disappears()
    {
        var values = new[]
        {
            new DashboardDefinition("default", "Home", "", IsDefault: true),
            new DashboardDefinition("rooms", "Rooms", "lovelace/rooms")
        };

        Assert.Equal("default", DashboardSelection.Select(values, "removed").Id);
    }
}
