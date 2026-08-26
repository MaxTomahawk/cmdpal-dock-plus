using System.Runtime.InteropServices;

namespace CmdPalDockPlus.Windows.SystemStatus;

public readonly record struct VolumeSnapshot(int Percent, bool IsMuted)
{
    public int ClampedPercent => Math.Clamp(Percent, 0, 100);
}

public sealed class VolumeService : IDisposable
{
    private readonly object _gate = new();
    private readonly IAudioEndpointVolume _endpoint;
    private readonly EndpointVolumeCallback _callback;
    private VolumeSnapshot _snapshot;
    private bool _disposed;

    public VolumeService()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        try
        {
            ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device));
            try
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                ThrowIfFailed(device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out var endpointObject));
                _endpoint = (IAudioEndpointVolume)endpointObject;
            }
            finally
            {
                ReleaseComObject(device);
            }
        }
        finally
        {
            ReleaseComObject(enumerator);
        }

        _callback = new EndpointVolumeCallback(RefreshFromEndpoint);
        ThrowIfFailed(_endpoint.RegisterControlChangeNotify(_callback));
        _snapshot = ReadSnapshot();
    }

    public event EventHandler? Changed;

    public VolumeSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public void ToggleMute()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var current = Snapshot;
        var context = Guid.Empty;
        ThrowIfFailed(_endpoint.SetMute(!current.IsMuted, ref context));
    }

    public void ChangePercent(int delta)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var target = Math.Clamp(Snapshot.ClampedPercent + delta, 0, 100) / 100f;
        var context = Guid.Empty;
        ThrowIfFailed(_endpoint.SetMasterVolumeLevelScalar(target, ref context));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ = _endpoint.UnregisterControlChangeNotify(_callback);
        ReleaseComObject(_endpoint);
        GC.SuppressFinalize(this);
    }

    private void RefreshFromEndpoint()
    {
        if (_disposed) return;
        try
        {
            var next = ReadSnapshot();
            var changed = false;
            lock (_gate)
            {
                if (_snapshot != next)
                {
                    _snapshot = next;
                    changed = true;
                }
            }

            if (changed)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (COMException)
        {
            // Endpoint replacement/device teardown is non-fatal. A future runtime
            // restart will bind the current default endpoint again.
        }
    }

    private VolumeSnapshot ReadSnapshot()
    {
        ThrowIfFailed(_endpoint.GetMasterVolumeLevelScalar(out var scalar));
        ThrowIfFailed(_endpoint.GetMute(out var muted));
        return new VolumeSnapshot((int)Math.Round(Math.Clamp(scalar, 0f, 1f) * 100f), muted);
    }

    private static void ThrowIfFailed(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }

    private static void ReleaseComObject(object value)
    {
        if (Marshal.IsComObject(value))
        {
            _ = Marshal.FinalReleaseComObject(value);
        }
    }

    private sealed class EndpointVolumeCallback(Action changed) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr notifyData)
        {
            changed();
            return 0;
        }
    }

    private enum EDataFlow
    {
        Render,
        Capture,
        All,
    }

    private enum ERole
    {
        Console,
        Multimedia,
        Communications,
    }

    [Flags]
    private enum ClsCtx : uint
    {
        InprocServer = 0x1,
        InprocHandler = 0x2,
        LocalServer = 0x4,
        RemoteServer = 0x10,
        All = InprocServer | InprocHandler | LocalServer | RemoteServer,
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumeratorComObject;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(
            ref Guid iid,
            ClsCtx clsCtx,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);

        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
        [PreserveSig] int VolumeStepUp(ref Guid eventContext);
        [PreserveSig] int VolumeStepDown(ref Guid eventContext);
        [PreserveSig] int QueryHardwareSupport(out uint mask);
        [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
    }

    [ComImport]
    [Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig]
        int OnNotify(IntPtr notifyData);
    }
}
