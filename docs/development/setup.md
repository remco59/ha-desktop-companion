# Development setup

## Requirements

- Windows 11 x64 development machine.
- Visual Studio 2026 with the .NET desktop development and Windows App SDK tooling, or VS Code with C# Dev Kit and the Windows App SDK prerequisites.
- .NET 10 SDK.
- Git.
- Python 3.12+ for Home Assistant integration checks.
- A Home Assistant development instance for integration work.

## Build the Windows solution

From the repository root:

```powershell
dotnet restore .\src\DesktopCompanion.sln
dotnet build .\src\DesktopCompanion.sln -c Debug
```

The WinUI application includes the native shell, tray experience, Home Assistant connection lifecycle, and embedded dashboard. The dashboard uses WebView2 and Home Assistant's own frontend; Windows sensor publishing and most native Windows controls are still on the roadmap. See [the Product Plan](../product-plan.md) for the current implementation status and next steps.

## Validate the Home Assistant integration

```powershell
python -m compileall .\integrations\homeassistant\custom_components\desktop_companion
```

Install the integration by copying the `desktop_companion` directory into the Home Assistant `custom_components` directory. The current scaffold only provides an initial config flow; sensor publishing is intentionally not enabled until the pairing protocol is implemented.

## Development principles

- Keep Core free of UI, Windows, and Home Assistant transport dependencies.
- Add Windows API code only in the Windows project.
- Add Home Assistant API and MQTT code only in the HomeAssistant project.
- Keep view models thin and use application services for behaviour.
- Never commit real access tokens, pairing secrets, local databases, or personal Home Assistant configuration.
