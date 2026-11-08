namespace DesktopCompanion.Core.Abstractions;

/// <summary>
/// Stores small secrets using an operating-system-protected credential facility.
/// Implementations must never log secret values.
/// </summary>
public interface ISecretStore
{
    void Write(string key, string secret);
    string? Read(string key);
    void Delete(string key);
}
