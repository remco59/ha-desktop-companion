using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using DesktopCompanion.Core.Abstractions;

namespace DesktopCompanion.Windows.Security;

/// <summary>
/// Stores secrets in the current Windows user's Credential Manager vault.
/// Generic credentials are protected by Windows and are not written to app files.
/// </summary>
public sealed class WindowsCredentialStore : ISecretStore
{
    private const uint GenericCredential = 1;
    private const uint LocalMachinePersistence = 2;
    private const int MaximumBlobSize = 5 * 1024;

    public void Write(string key, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(secret);
        var blob = Encoding.UTF8.GetBytes(secret);
        if (blob.Length > MaximumBlobSize)
            throw new ArgumentOutOfRangeException(nameof(secret), "The credential is larger than Windows Credential Manager supports.");

        var credential = new NativeCredential
        {
            Type = GenericCredential,
            TargetName = key,
            CredentialBlobSize = (uint)blob.Length,
            CredentialBlob = Marshal.AllocHGlobal(blob.Length),
            Persist = LocalMachinePersistence,
            UserName = Environment.UserName
        };

        try
        {
            Marshal.Copy(blob, 0, credential.CredentialBlob, blob.Length);
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not save the Home Assistant credential.");
        }
        finally
        {
            if (credential.CredentialBlob != IntPtr.Zero) Marshal.ZeroFreeHGlobal(credential.CredentialBlob);
            CryptographicOperations.ZeroMemory(blob);
        }
    }

    public string? Read(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!CredRead(key, GenericCredential, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) return null; // ERROR_NOT_FOUND
            throw new Win32Exception(error, "Windows could not read the Home Assistant credential.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlobSize > MaximumBlobSize)
                throw new InvalidOperationException("The stored credential exceeded the supported size.");

            var blob = new byte[checked((int)credential.CredentialBlobSize)];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            try { return Encoding.UTF8.GetString(blob); }
            finally { CryptographicOperations.ZeroMemory(blob); }
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Delete(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (CredDelete(key, GenericCredential, 0)) return;
        var error = Marshal.GetLastWin32Error();
        if (error != 1168) throw new Win32Exception(error, "Windows could not delete the Home Assistant credential.");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string targetName, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string targetName, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);
}
