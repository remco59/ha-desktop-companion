using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopCompanion.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnNavigationClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string page })
        {
            return;
        }

        PageTitle.Text = page;
        var isOverview = page == "Overview";
        OverviewContent.Visibility = isOverview ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderContent.Visibility = isOverview ? Visibility.Collapsed : Visibility.Visible;

        PageSubtitle.Text = page switch
        {
            "Overview" => "Your PC and Home Assistant, in one place.",
            "Home Assistant" => "Connect your smart home and control entities.",
            "Sensors" => "Discover and configure Windows telemetry.",
            "Actions" => "Build actions that work across Windows and Home Assistant.",
            "Dashboards" => "Create your full and tray dashboards.",
            "Applications & Media" => "Manage your applications and media controls.",
            "Notifications" => "Choose which events appear on your desktop.",
            "Settings" => "Configure Desktop Companion.",
            _ => "Desktop Companion"
        };

        PlaceholderTitle.Text = page;
        PlaceholderDescription.Text = $"{page} is part of the implementation roadmap. The navigation shell is in place; this feature will be built as a complete vertical slice.";
    }
}
