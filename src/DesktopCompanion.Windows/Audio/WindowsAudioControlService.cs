using System.Runtime.InteropServices;
using DesktopCompanion.Core.Abstractions;
using DesktopCompanion.Core.Models;
using DesktopCompanion.Core.Sensors;

namespace DesktopCompanion.Windows.Audio;

/// <summary>
/// Controls the default Windows playback endpoint through the native Core Audio COM API.
/// All COM interfaces are scoped to one call and released immediately afterwards.
/// </summary>
public sealed class WindowsAudioControlService : IAudioControlService
{
    private static readonly Guid EndpointVolumeInterfaceId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private const uint ClsContextInprocServer = 1;

    public Task<AudioEndpointState> GetDefaultOutputStateAsync(CancellationToken cancellationToken = default) =>
        Task.Run(ReadDefaultOutputState, cancellationToken);

    public Task<AudioEndpointState> SetMasterVolumeAsync(
        double volumePercent,
        CancellationToken cancellationToken = default)
    {
        var scalar = AudioVolumeNormalizer.ToScalar(volumePercent);
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var endpoint = OpenDefaultEndpoint();
            var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.Volume.SetMasterVolumeLevelScalar(scalar, ref context));
            return ReadState(endpoint);
        }, cancellationToken);
    }

    public Task<AudioEndpointState> SetMutedAsync(
        bool muted,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var endpoint = OpenDefaultEndpoint();
            var context = Guid.Empty;
            Marshal.ThrowExceptionForHR(endpoint.Volume.SetMute(muted, ref context));
            return ReadState(endpoint);
        }, cancellationToken);

    private static AudioEndpointState ReadDefaultOutputState()
    {
        using var endpoint = OpenDefaultEndpoint();
        return ReadState(endpoint);
    }

    private static AudioEndpointState ReadState(DefaultEndpoint endpoint)
    {
        Marshal.ThrowExceptionForHR(endpoint.Volume.GetMasterVolumeLevelScalar(out var scalar));
        Marshal.ThrowExceptionForHR(endpoint.Volume.GetMute(out var muted));
        return new AudioEndpointState(
            AudioVolumeNormalizer.ToPercentage(scalar),
            muted,
            endpoint.DeviceId);
    }

    private static DefaultEndpoint OpenDefaultEndpoint()
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioEndpointVolume? volume = null;
        IntPtr deviceIdPointer = IntPtr.Zero;

        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            Marshal.ThrowExceptionForHR(
                enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out device));

            Marshal.ThrowExceptionForHR(device.GetId(out deviceIdPointer));
            var deviceId = Marshal.PtrToStringUni(deviceIdPointer) ?? "Default playback device";
            var interfaceId = EndpointVolumeInterfaceId;
            Marshal.ThrowExceptionForHR(
                device.Activate(ref interfaceId, ClsContextInprocServer, IntPtr.Zero, out volume));

            var result = new DefaultEndpoint(deviceId, volume);
            volume = null;
            ReleaseComObject(device);
            device = null;
            ReleaseComObject(enumerator);
            enumerator = null;
            return result;
        }
        finally
        {
            if (deviceIdPointer != IntPtr.Zero)
                Marshal.FreeCoTaskMem(deviceIdPointer);
            ReleaseComObject(volume);
            ReleaseComObject(device);
            ReleaseComObject(enumerator);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.ReleaseComObject(value);
    }

    private sealed class DefaultEndpoint(string deviceId, IAudioEndpointVolume volume) : IDisposable
    {
        public string DeviceId { get; } = deviceId;
        public IAudioEndpointVolume Volume { get; } = volume;

        public void Dispose() => ReleaseComObject(Volume);
    }

    private enum DataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    private enum Role
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(DataFlow dataFlow, uint stateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice endpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr callback);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr callback);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid interfaceId,
            uint classContext,
            IntPtr activationParameters,
            [MarshalAs(UnmanagedType.Interface)] out IAudioEndpointVolume endpointVolume);

        [PreserveSig]
        int OpenPropertyStore(uint storageMode, out IntPtr properties);

        [PreserveSig]
        int GetId(out IntPtr deviceId);

        [PreserveSig]
        int GetState(out uint state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
        [PreserveSig] int VolumeStepUp(ref Guid eventContext);
        [PreserveSig] int VolumeStepDown(ref Guid eventContext);
        [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float minimumDb, out float maximumDb, out float incrementDb);
    }
}
