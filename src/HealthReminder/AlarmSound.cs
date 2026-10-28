using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Strongthium.HealthReminder;

/// <summary>
/// Plays a WAV file from memory, once or over and over until it's stopped, with winmm's PlaySound. Playing another
/// replaces the one playing, which is how the volume rises while the alarm rings.
/// </summary>
internal static partial class AlarmSound
{
    private static IntPtr buffer;

    /// <summary>A sound is repeating; one played once doesn't count.</summary>
    public static bool Playing { get; private set; }

    public static void Play(byte[] wav, bool loop)
    {
        try
        {
            // With SND_ASYNC the sound must stay in memory while it plays: a copy that the GC can't move. The old copy
            // is freed only once the new sound has replaced it.
            var next = Marshal.AllocHGlobal(wav.Length);
            Marshal.Copy(wav, 0, next, wav.Length);
            var played = PlaySound(next, IntPtr.Zero, SndAsync | SndNoDefault | SndMemory | (loop ? SndLoop : 0));
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            buffer = next;
            Playing = played && loop;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            Trace.WriteLine("health-reminder: no sound: " + exception.Message);
        }
    }

    public static void Stop()
    {
        PlaySound(IntPtr.Zero, IntPtr.Zero, 0);
        Playing = false;
        if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        buffer = IntPtr.Zero;
    }

    private const uint SndAsync = 0x1, SndNoDefault = 0x2, SndMemory = 0x4, SndLoop = 0x8;

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(IntPtr sound, IntPtr module, uint flags);
}
