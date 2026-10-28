namespace Strongthium.HealthReminder.Tests;

public sealed class SoundsTests
{
    private static short Peak(byte[] wav)
    {
        short peak = 0;
        for (var i = 44; i + 1 < wav.Length; i += 2) peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(wav, i)));
        return peak;
    }

    [Theory]
    [InlineData(ChimeSound.Chime)]
    [InlineData(ChimeSound.Bell)]
    [InlineData(ChimeSound.Rising)]
    [InlineData(ChimeSound.Beeps)]
    [InlineData(ChimeSound.AlarmClock)]
    [InlineData(ChimeSound.Siren)]
    public void EachSoundIsTwoSecondsOfSixteenBitWav(ChimeSound sound)
    {
        var wav = Sounds.Make(sound, 1);
        Assert.Equal("RIFF"u8.ToArray(), wav[..4]);
        Assert.Equal("WAVE"u8.ToArray(), wav[8..12]);
        Assert.Equal(16, BitConverter.ToInt16(wav, 34));
        Assert.Equal(44 + Sounds.Rate * 2 * 2, wav.Length);
        Assert.InRange(Peak(wav), 10_000, short.MaxValue);
    }

    [Fact]
    public void VolumeScalesTheSound()
    {
        var full = Peak(Sounds.Make(ChimeSound.Beeps, 1));
        var half = Peak(Sounds.Make(ChimeSound.Beeps, 0.5));
        Assert.InRange(half, full * 0.45, full * 0.55);
        Assert.Equal(0, Peak(Sounds.Make(ChimeSound.None, 1)));
    }

    [Fact]
    public void ScalesAUsersSixteenBitWavAndRefusesWhatIsntWav()
    {
        var wav = Sounds.Make(ChimeSound.Bell, 1);
        var scaled = Sounds.Scale(wav, 0.25);
        Assert.NotNull(scaled);
        Assert.Equal(wav.Length, scaled.Length);
        Assert.InRange(Peak(scaled), Peak(wav) * 0.2, Peak(wav) * 0.3);
        Assert.Equal(wav[..44], scaled[..44]);

        Assert.Null(Sounds.Scale("ID3 not a wav file at all, but long enough to have a header....."u8.ToArray(), 1));
        Assert.Null(Sounds.Scale([1, 2, 3], 1));
    }
}
