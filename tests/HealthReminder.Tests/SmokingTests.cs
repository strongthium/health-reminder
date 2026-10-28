namespace Strongthium.HealthReminder.Tests;

public sealed class SmokingTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 18, 0, 0);

    private static Smoke Lit(DateTime at, double minutes = 0) => new() { Started = at, Finished = minutes > 0 ? at.AddMinutes(minutes) : at };

    [Fact]
    public void CountsTheDayAndTheWeek()
    {
        var smokes = new List<Smoke>();
        // 10 a day for the 7 days before today, starting at 7:30, and 4 so far today.
        for (var day = 1; day <= 7; day++)
            for (var i = 0; i < 10; i++)
                smokes.Add(Lit(Now.Date.AddDays(-day).AddHours(7.5 + i * 1.5), 6));
        for (var i = 0; i < 4; i++) smokes.Add(Lit(Now.Date.AddHours(8 + i), 5));

        var stats = Smoking.Stats(smokes, Now);
        Assert.Equal(4, stats.Today);
        Assert.Equal(10, stats.PerDay);
        Assert.Equal(new TimeOnly(7, 30), stats.FirstOfDay);
        Assert.Equal(TimeSpan.FromHours(7), stats.SinceLast);
        Assert.NotNull(stats.Duration);
        Assert.InRange(stats.Duration.Value.TotalMinutes, 5.9, 6);
    }

    [Fact]
    public void ADayOrTwoOfLoggingIsntAveragedOverAWeek()
    {
        var smokes = new List<Smoke> { Lit(Now.Date.AddDays(-1).AddHours(9)), Lit(Now.Date.AddDays(-1).AddHours(12)), Lit(Now.Date.AddHours(9)) };
        Assert.Equal(2, Smoking.Stats(smokes, Now).PerDay);
        Assert.Equal(1, Smoking.Stats([Lit(Now.Date.AddHours(9))], Now).PerDay);
    }

    [Fact]
    public void OnlyBelievableTimesCount()
    {
        Assert.Null(Lit(Now).Duration);                 // not timed
        Assert.Null(Lit(Now, 45).Duration);             // left going on the button
        Assert.Equal(TimeSpan.FromMinutes(7), Lit(Now, 7).Duration);
        Assert.Null(new Smoke { Started = Now }.Duration);
    }

    [Theory]
    [InlineData(20, 10, "21 mg a day", "4 mg")]
    [InlineData(20, 60, "21 mg a day", "2 mg")]
    [InlineData(8, 45, "14 mg a day", "2 mg")]
    public void NicotineReplacementMatchesHowMuchIsSmoked(double perDay, int minutesAfterWaking, string patch, string gum)
    {
        var options = Smoking.Replacement(perDay, TimeSpan.FromMinutes(minutesAfterWaking));
        Assert.Equal(patch, options.Single(o => o.Name == "Patch").Dose);
        Assert.Equal(gum, options.Single(o => o.Name == "Gum or lozenge").Dose);
    }

    [Fact]
    public void AFewADayGetNoPatch() =>
        Assert.DoesNotContain(Smoking.Replacement(3, null), o => o.Name == "Patch");

    [Fact]
    public void TheGoalComesDownEachWeek()
    {
        var start = new DateOnly(2026, 10, 1);
        Assert.Equal(15, Smoking.Target(15, start, 2, start.AddDays(6)));
        Assert.Equal(13, Smoking.Target(15, start, 2, start.AddDays(7)));
        Assert.Equal(0, Smoking.Target(15, start, 2, start.AddDays(70)));
    }

    [Fact]
    public void RecalibratingGoesOneStepAWeek()
    {
        var today = new DateOnly(2026, 10, 27);
        var first = Recalibration.Next(new Dictionary<string, DateOnly>(), today);
        Assert.NotNull(first);
        Assert.Equal((Recalibration.Steps[0], today), (first.Value.Step, first.Value.From));

        // Started 3 days ago: the next is due 4 days from now.
        var started = new Dictionary<string, DateOnly> { [Recalibration.Steps[0].Id] = today.AddDays(-3) };
        var second = Recalibration.Next(started, today);
        Assert.Equal((Recalibration.Steps[1], today.AddDays(4)), (second!.Value.Step, second.Value.From));

        // Long ago: due now, not in the past.
        started[Recalibration.Steps[0].Id] = today.AddDays(-30);
        Assert.Equal(today, Recalibration.Next(started, today)!.Value.From);

        var all = Recalibration.Steps.ToDictionary(s => s.Id, _ => today);
        Assert.Null(Recalibration.Next(all, today));
    }

    [Fact]
    public void EveryStepSaysHowWellItsBacked()
    {
        Assert.Equal(Recalibration.Steps.Count, Recalibration.Steps.Select(s => s.Id).Distinct().Count());
        Assert.All(Recalibration.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.Studies)));
        Assert.Contains("beta-carotene", Recalibration.Caution);
    }

    [Fact]
    public void KeepsTheLog()
    {
        using var database = new Database();
        var smoke = new Smoke { Started = Now, Brand = "Example" };
        database.Save(smoke);
        smoke.Finished = Now.AddMinutes(6);
        database.Save(smoke);
        var saved = Assert.Single(database.Smokes(Now.Date));
        Assert.Equal((Now, Now.AddMinutes(6), "Example"), (saved.Started, saved.Finished, saved.Brand));
        Assert.Empty(database.Smokes(Now.AddMinutes(1)));
        database.Delete(saved);
        Assert.Empty(database.Smokes(Now.Date));
    }
}
