namespace DesktopCompanion.Core.Models;

/// <summary>A locally discovered Windows Start Menu application shortcut.</summary>
public sealed record InstalledApplication(string Id, string Name);
