using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RadialMenu;

/// <summary>Virtual-key codes we use.</summary>
internal static class VK
{
    public const ushort Shift = 0x10, Ctrl = 0x11, Alt = 0x12, Win = 0x5B, Tab = 0x09, Esc = 0x1B,
        Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28, PrintScreen = 0x2C,
        A = 0x41, C = 0x43, D = 0x44, E = 0x45, I = 0x49, L = 0x4C, R = 0x52, T = 0x54, V = 0x56, W = 0x57,
        X = 0x58, Y = 0x59, Z = 0x5A, F4 = 0x73, F5 = 0x74, F11 = 0x7A, Period = 0xBE,
        VolMute = 0xAD, MediaNext = 0xB0, MediaPrev = 0xB1, MediaStop = 0xB2, MediaPlay = 0xB3;
}

internal static class InputSender
{
    /// <summary>Marks events we injected so our own hook ignores them.</summary>
    public static readonly UIntPtr Magic = new(0x52444D31);

    const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_EXTENDEDKEY = 1, KEYEVENTF_KEYUP = 2;
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;

    static bool IsExtended(ushort vk) => (vk >= 0x21 && vk <= 0x28) || vk == 0x2D || vk == 0x2E;

    static Native.INPUT Key(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = { ki = { wVk = vk, dwFlags = (IsExtended(vk) ? KEYEVENTF_EXTENDEDKEY : 0) | (up ? KEYEVENTF_KEYUP : 0), dwExtraInfo = Magic } }
    };

    /// <summary>Press all keys in order, then release in reverse (a chord like Ctrl+Shift+T).</summary>
    public static void Chord(params ushort[] vks)
    {
        var list = new List<Native.INPUT>();
        foreach (var k in vks) list.Add(Key(k, false));
        for (int i = vks.Length - 1; i >= 0; i--) list.Add(Key(vks[i], true));
        Native.SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf<Native.INPUT>());
    }

    public static void RightClick()
    {
        var down = new Native.INPUT { type = INPUT_MOUSE, u = { mi = { dwFlags = MOUSEEVENTF_RIGHTDOWN, dwExtraInfo = Magic } } };
        var up = new Native.INPUT { type = INPUT_MOUSE, u = { mi = { dwFlags = MOUSEEVENTF_RIGHTUP, dwExtraInfo = Magic } } };
        Native.SendInput(2, new[] { down, up }, Marshal.SizeOf<Native.INPUT>());
    }
}
