using System;
using System.Runtime.InteropServices;

namespace RadialMenu;

/// <summary>Master volume of the default playback device via Core Audio COM.</summary>
internal static class AudioController
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr p);
        int UnregisterControlChangeNotify(IntPtr p);
        int GetChannelCount(out uint c);
        int SetMasterVolumeLevel(float level, ref Guid ctx);
        int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint ch, float level, ref Guid ctx);
        int SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
        int GetChannelVolumeLevel(uint ch, out float level);
        int GetChannelVolumeLevelScalar(uint ch, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    static IAudioEndpointVolume? Endpoint()
    {
        try
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            en.GetDefaultAudioEndpoint(0 /*render*/, 1 /*multimedia*/, out var dev);
            var iid = typeof(IAudioEndpointVolume).GUID;
            dev.Activate(ref iid, 23 /*CLSCTX_ALL*/, IntPtr.Zero, out var o);
            return (IAudioEndpointVolume)o;
        }
        catch { return null; }
    }

    public static int Volume
    {
        get { var e = Endpoint(); if (e == null) return -1; e.GetMasterVolumeLevelScalar(out var v); return (int)Math.Round(v * 100); }
    }

    public static bool Muted
    {
        get { var e = Endpoint(); if (e == null) return false; e.GetMute(out var m); return m; }
    }

    public static void Change(int deltaPercent)
    {
        var e = Endpoint(); if (e == null) return;
        var g = Guid.Empty;
        e.GetMasterVolumeLevelScalar(out var v);
        v = Math.Clamp(v + deltaPercent / 100f, 0f, 1f);
        e.SetMasterVolumeLevelScalar(v, ref g);
        if (v > 0) e.SetMute(false, ref g);
    }

    public static void ToggleMute()
    {
        var e = Endpoint(); if (e == null) return;
        var g = Guid.Empty;
        e.GetMute(out var m);
        e.SetMute(!m, ref g);
    }
}
