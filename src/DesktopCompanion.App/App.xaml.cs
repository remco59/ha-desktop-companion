using DesktopCompanion.Windows.Tray;
using Microsoft.UI.Xaml;

namespace DesktopCompanion.App;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private TrayPopupWindow? _trayPopupWindow;
    private TrayIconHost? _trayIconHost;
    private bool _isExiting;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_mainWindow is not null)
        {
            ShowDashboard();
            return;
        }

        _mainWindow = new MainWindow();
        _trayPopupWindow = new TrayPopupWindow();

        _trayPopupWindow.OpenDashboardRequested += (_, _) => ShowDashboard();
        _trayPopupWindow.SettingsRequested += (_, _) => ShowSettings();
        _trayPopupWindow.ExitRequested += (_, _) => ExitApplication();

        _trayIconHost = new TrayIconHost();
        _trayIconHost.Activated += (_, _) => ToggleTrayPopup();
        _trayIconHost.ContextRequested += (_, _) => ToggleTrayPopup();

        _mainWindow.Activate();
    }

    private void ToggleTrayPopup()
    {
        if (_isExiting || _trayPopupWindow is null)
        {
            return;
        }

        _trayPopupWindow.ToggleNearCursor();
    }

    private void ShowDashboard()
    {
        if (_isExiting || _mainWindow is null)
        {
            return;
        }

        _trayPopupWindow?.AppWindow.Hide();
        _mainWindow.ShowFromTray();
    }

    private void ShowSettings()
    {
        if (_isExiting || _mainWindow is null)
        {
            return;
        }

        _trayPopupWindow?.AppWindow.Hide();
        _mainWindow.ShowSettingsFromTray();
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _trayIconHost?.Dispose();
        _trayIconHost = null;

        _trayPopupWindow?.Close();
        _trayPopupWindow = null;

        _mainWindow?.AllowClose();
        _mainWindow?.Close();
        _mainWindow = null;

        Exit();
    }
}
