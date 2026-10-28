namespace Strongthium.HealthReminder;

/// <summary>The sounds a reminder can make. They are made here, not shipped as files; <see cref="Custom"/> is a WAV
/// file of the user's.</summary>
public enum ChimeSound { Chime, Bell, Rising, Beeps, AlarmClock, Siren, Custom, None }

/// <summary>Makes the reminder sounds as 16-bit mono WAV files, at a volume from 0 to 1.</summary>
public static class Sounds
{
    public const int Rate = 22050;

    public static string Label(this ChimeSound sound) => sound switch
    {
        ChimeSound.Chime => "Chime (two soft bells)",
        ChimeSound.Bell => "Bell",
        ChimeSound.Rising => "Rising notes",
        ChimeSound.Beeps => "Beeps",
        ChimeSound.AlarmClock => "Alarm clock (loud)",
        ChimeSound.Siren => "Siren (very loud)",
        ChimeSound.Custom => "My own sound file (.wav)",
        _ => "No sound",
    };

    /// <summary>One round of the sound, pause included, so that it can be looped.</summary>
    public static byte[] Make(ChimeSound sound, double volume)
    {
        var samples = new double[Rate * 2];
        switch (sound)
        {
            case ChimeSound.Bell:
                Bell(samples, 1046.5, 0, 1.8, 1.0);
                break;
            case ChimeSound.Rising:
                Bell(samples, 523.25, 0, 0.9, 0.7);
                Bell(samples, 659.25, 0.25, 0.9, 0.7);
                Bell(samples, 783.99, 0.5, 1.2, 0.8);
                break;
            case ChimeSound.Beeps:
                for (var i = 0; i < 4; i++) Tone(samples, 1000, i * 0.2, 0.12, 0.9, square: true);
                break;
            case ChimeSound.AlarmClock:
                // A bell hammered 20 times a second, twice.
                for (var burst = 0; burst < 2; burst++)
                    for (var strike = 0; strike < 14; strike++)
                        Bell(samples, strike % 2 == 0 ? 2400 : 2600, burst * 0.9 + strike * 0.05, 0.06, 1.0);
                break;
            case ChimeSound.Siren:
                for (var i = 0; i < samples.Length; i++)
                {
                    var t = (double)i / Rate;
                    // 600 Hz up to 1400 Hz and back, once a second; the phase follows the integral of the frequency.
                    var phase = 2 * Math.PI * (1000 * t - 400 / (2 * Math.PI) * Math.Cos(2 * Math.PI * t));
                    samples[i] = 0.8 * Math.Sign(Math.Sin(phase)) * 0.6 + 0.4 * Math.Sin(phase);
                }
                break;
            case ChimeSound.None:
                break;
            default:
                Bell(samples, 880, 0, 1.2, 0.8);
                Bell(samples, 659.25, 0.3, 1.4, 0.8);
                break;
        }
        return Wav(samples, volume);
    }

    private static void Bell(double[] samples, double frequency, double start, double length, double level)
    {
        var from = (int)(start * Rate);
        for (var i = 0; i < length * Rate && from + i < samples.Length; i++)
        {
            var t = (double)i / Rate;
            var envelope = Math.Min(1, t * 400) * Math.Exp(-t * 4.5 / Math.Max(length, 0.2));
            samples[from + i] += level * envelope * (Math.Sin(2 * Math.PI * frequency * t) + 0.35 * Math.Sin(4 * Math.PI * frequency * t) + 0.1 * Math.Sin(6.8 * Math.PI * frequency * t)) / 1.45;
        }
    }

    private static void Tone(double[] samples, double frequency, double start, double length, double level, bool square)
    {
        var from = (int)(start * Rate);
        for (var i = 0; i < length * Rate && from + i < samples.Length; i++)
        {
            var t = (double)i / Rate;
            var envelope = Math.Min(1, Math.Min(t, length - t) * 200);
            var wave = Math.Sin(2 * Math.PI * frequency * t);
            samples[from + i] += level * envelope * (square ? 0.7 * Math.Sign(wave) + 0.3 * wave : wave);
        }
    }

    private static byte[] Wav(double[] samples, double volume)
    {
        var gain = Math.Clamp(volume, 0, 1) * short.MaxValue * 0.9;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        Header(writer, samples.Length * 2);
        foreach (var sample in samples) writer.Write((short)Math.Clamp(sample * gain, short.MinValue, short.MaxValue));
        writer.Flush();
        return stream.ToArray();
    }

    private static void Header(BinaryWriter writer, int dataBytes)
    {
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);       // PCM
        writer.Write((short)1);       // mono
        writer.Write(Rate);
        writer.Write(Rate * 2);       // bytes a second
        writer.Write((short)2);       // bytes a sample
        writer.Write((short)16);      // bits a sample
        writer.Write("data"u8);
        writer.Write(dataBytes);
    }

    /// <summary>A WAV file of the user's at a volume: 16-bit PCM is scaled; any other kind is played as it is.
    /// Null when it isn't a WAV file.</summary>
    public static byte[]? Scale(byte[] wav, double volume)
    {
        if (wav.Length < 44 || !wav.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !wav.AsSpan(8, 4).SequenceEqual("WAVE"u8)) return null;
        var copy = (byte[])wav.Clone();
        var pcm16 = false;
        for (var at = 12; at + 8 <= copy.Length;)
        {
            var id = copy.AsSpan(at, 4);
            var size = BitConverter.ToInt32(copy, at + 4);
            if (size < 0 || at + 8 + size > copy.Length) size = copy.Length - at - 8;
            if (id.SequenceEqual("fmt "u8) && size >= 16)
                pcm16 = BitConverter.ToInt16(copy, at + 8) == 1 && BitConverter.ToInt16(copy, at + 22) == 16;
            else if (id.SequenceEqual("data"u8) && pcm16)
            {
                var gain = Math.Clamp(volume, 0, 1);
                for (var i = at + 8; i + 1 < at + 8 + size; i += 2)
                    BitConverter.TryWriteBytes(copy.AsSpan(i, 2), (short)Math.Clamp(BitConverter.ToInt16(copy, i) * gain, short.MinValue, short.MaxValue));
            }
            at += 8 + size + (size & 1);
        }
        return copy;
    }
}
