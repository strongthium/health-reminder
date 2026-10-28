namespace Strongthium.HealthReminder.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public void SavesAndReadsBackAReminder()
    {
        using var database = new Database();
        var reminder = new Reminder
        {
            Kind = ReminderKind.Pill,
            Name = "  Examplopril 10 mg ",
            Unit = "capsule",
            Notes = "with food",
            Times = [new ReminderTime(new TimeOnly(20, 0), 1), new ReminderTime(new TimeOnly(8, 0), 2.5), new ReminderTime(new TimeOnly(8, 0), 1)],
            Days = DaySet.Monday | DaySet.Friday,
            Created = new DateTime(2026, 10, 27, 9, 30, 0),
            Appearance = new PillAppearance(["BLUE", "WHITE"], "CAPSULE", 18, "EX;20", 1),
            Form = "CAPSULE",
            SetId = "aaaa-1",
            Ndc = "12345-201",
            RxCui = "311353",
            LabelTitle = "EXAMPLOPRIL CAPSULE [EXAMPLE PHARMA]",
            Photo = "aaaa-1-pill.jpg",
            PhotoSource = "the photo on this label",
        };
        var id = database.Save(reminder);
        Assert.True(id > 0);

        var read = Assert.Single(database.Reminders());
        Assert.Equal("Examplopril 10 mg", read.Name);
        Assert.Equal([new ReminderTime(new TimeOnly(8, 0), 2.5), new ReminderTime(new TimeOnly(20, 0), 1)], read.Times);
        Assert.Equal(DaySet.Monday | DaySet.Friday, read.Days);
        Assert.Equal(reminder.Created, read.Created);
        Assert.Equal(["BLUE", "WHITE"], read.Appearance.Colors);
        Assert.Equal(reminder.Appearance with { Colors = read.Appearance.Colors }, read.Appearance);
        Assert.Equal(("CAPSULE", "aaaa-1", "12345-201", "311353", "aaaa-1-pill.jpg", "the photo on this label"),
            (read.Form, read.SetId, read.Ndc, read.RxCui, read.Photo, read.PhotoSource));

        read.Name = "Renamed";
        Assert.Equal(id, database.Save(read));
        Assert.Equal("Renamed", Assert.Single(database.Reminders()).Name);
    }

    [Fact]
    public void KeepsAnInjectionsSitesAndACheckReadingUnit()
    {
        using var database = new Database();
        database.Save(new Reminder { Kind = ReminderKind.Injection, Name = "Insulin", Unit = "unit", Sites = ["Thigh, left", "Thigh, right"], Times = [new(new TimeOnly(8, 0), 12)], Din = "02024233 ", RxNumber = "4471182" });
        database.Save(new Reminder { Kind = ReminderKind.BloodSugar, Name = "Sugar", ValueUnit = "mmol/L", Times = [new(new TimeOnly(7, 0), 1)] });
        var all = database.Reminders();
        Assert.Equal(["Thigh, left", "Thigh, right"], all.Single(r => r.Kind == ReminderKind.Injection).Sites);
        Assert.Equal(("02024233", "4471182"), (all.Single(r => r.Kind == ReminderKind.Injection).Din, all.Single(r => r.Kind == ReminderKind.Injection).RxNumber));
        Assert.Equal("mmol/L", all.Single(r => r.Kind == ReminderKind.BloodSugar).ValueUnit);
    }

    [Fact]
    public void DeletingAReminderDeletesItsLogAndPhoto()
    {
        using var database = new Database();
        File.WriteAllBytes(Path.Combine(database.ImageDirectory, "p.jpg"), [1, 2, 3]);
        var reminder = new Reminder { Name = "A", Times = [new(new TimeOnly(8, 0), 1)], Photo = "p.jpg" };
        database.Save(reminder);
        var due = new DateTime(2026, 10, 27, 8, 0, 0);
        database.Log(new Dose(reminder, due), new DoseRecord(DoseStatus.Taken, due, 1, "", ""));
        Assert.Single(database.LoggedSince(due.Date));

        database.Delete(reminder);
        Assert.Empty(database.Reminders());
        Assert.Empty(database.LoggedSince(due.Date));
        Assert.False(File.Exists(Path.Combine(database.ImageDirectory, "p.jpg")));
    }

    [Fact]
    public void KeepsWhetherToRecordAndTheVideoOfEachDose()
    {
        using var database = new Database();
        var reminder = new Reminder { Name = "Metformin", RecordVideo = true, Times = [new(new TimeOnly(8, 0), 2)] };
        database.Save(reminder);
        Assert.True(Assert.Single(database.Reminders()).RecordVideo);

        var due = new DateTime(2026, 10, 27, 8, 0, 0);
        new Schedule(database).Done(new Dose(reminder, due), due.AddMinutes(2), video: @"videos\2026-10-27\080200-metformin.mp4", videoSha256: "ab12");
        var record = Assert.Single(database.History(due.Date)).Record;
        Assert.Equal((@"videos\2026-10-27\080200-metformin.mp4", "ab12", 2.0), (record.Video, record.VideoSha256, record.Amount));
    }

    [Fact]
    public void KeepsWhichRemindersTheEatingPlanMade()
    {
        using var database = new Database();
        var plan = Planner.Make(new BodyProfile(Sex.Female, 165, 60, 40, Activity.Light, Pregnancy.None, new TimeOnly(7, 0), new TimeOnly(23, 0)));
        foreach (var reminder in Planner.Reminders(plan, new DateTime(2026, 10, 27))) database.Save(reminder);
        database.Save(new Reminder { Name = "Metformin", Times = [new(new TimeOnly(8, 0), 1)] });
        Assert.Equal(6, database.Reminders().Count(r => r.Planned));
        Assert.False(database.Reminders().Single(r => r.Name == "Metformin").Planned);
    }

    [Fact]
    public void UpgradesADatabaseWithoutTheDinAndPrescriptionNumber()
    {
        var directory = Path.Combine(Path.GetTempPath(), "health-reminder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var old = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + Path.Combine(directory, "health-reminder.db") + ";Pooling=False"))
            {
                old.Open();
                using var command = old.CreateCommand();
                command.CommandText = """
                    CREATE TABLE reminder (id INTEGER PRIMARY KEY, kind TEXT NOT NULL, name TEXT NOT NULL, unit TEXT NOT NULL DEFAULT '',
                        notes TEXT NOT NULL DEFAULT '', times TEXT NOT NULL DEFAULT '', days INTEGER NOT NULL DEFAULT 127, active INTEGER NOT NULL DEFAULT 1,
                        created TEXT NOT NULL, planned INTEGER NOT NULL DEFAULT 0, colors TEXT NOT NULL DEFAULT '', shape TEXT NOT NULL DEFAULT '',
                        size_mm REAL NOT NULL DEFAULT 0, imprint TEXT NOT NULL DEFAULT '', score INTEGER NOT NULL DEFAULT 1, form TEXT NOT NULL DEFAULT '',
                        set_id TEXT NOT NULL DEFAULT '', ndc TEXT NOT NULL DEFAULT '', rxcui TEXT NOT NULL DEFAULT '', label_title TEXT NOT NULL DEFAULT '',
                        photo TEXT NOT NULL DEFAULT '', photo_source TEXT NOT NULL DEFAULT '', sites TEXT NOT NULL DEFAULT '', value_unit TEXT NOT NULL DEFAULT '',
                        record_video INTEGER NOT NULL DEFAULT 0);
                    INSERT INTO reminder (kind, name, times, created) VALUES ('Pill', 'Adalat XL 30 mg', '08:00*1', '2026-10-27T08:00:00');
                    PRAGMA user_version = 1;
                    """;
                command.ExecuteNonQuery();
            }
            using var database = new Database(directory);
            var reminder = Assert.Single(database.Reminders());
            Assert.Equal(("Adalat XL 30 mg", "", ""), (reminder.Name, reminder.Din, reminder.RxNumber));
            reminder.Din = "02155907";
            database.Save(reminder);
            Assert.Equal("02155907", Assert.Single(database.Reminders()).Din);
            // ... and gains the grocery list.
            database.Save(new GroceryItem { Name = "Milk", Quantity = "2 L" });
            Assert.Equal("Milk", Assert.Single(database.Groceries()).Name);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void UpgradesAVersion4GroceryTableWithoutItsKitchenColumns()
    {
        var directory = Path.Combine(Path.GetTempPath(), "health-reminder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using (var old = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + Path.Combine(directory, "health-reminder.db") + ";Pooling=False"))
            {
                old.Open();
                using var command = old.CreateCommand();
                command.CommandText = """
                    CREATE TABLE grocery (id INTEGER PRIMARY KEY, name TEXT NOT NULL, quantity TEXT NOT NULL DEFAULT '', price TEXT, bought TEXT,
                        added TEXT NOT NULL, cleared INTEGER NOT NULL DEFAULT 0);
                    INSERT INTO grocery (name, quantity, bought, added, cleared) VALUES ('Milk', '2 L', '2026-10-26T10:00:00', '2026-10-26T10:00:00', 1);
                    PRAGMA user_version = 4;
                    """;
                command.ExecuteNonQuery();
            }
            using var database = new Database(directory);
            var milk = Assert.Single(database.InKitchen(new DateTime(2026, 10, 1)));
            milk.Used = new DateTime(2026, 10, 27);
            database.Save(milk);
            Assert.Empty(database.InKitchen(new DateTime(2026, 10, 1)));
            Assert.Empty(database.ExerciseLog(new DateOnly(2026, 1, 1)));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(directory, true); } catch (IOException) { }
        }
    }

    [Fact]
    public void KeepsSettings()
    {
        using var database = new Database();
        Assert.Null(database.Setting("snooze_minutes"));
        database.SetSetting("snooze_minutes", "15");
        database.SetSetting("snooze_minutes", "30");
        Assert.Equal("30", database.Setting("snooze_minutes"));
    }

    [Fact]
    public void ReadsTimesWithAndWithoutAmounts() =>
        Assert.Equal([new ReminderTime(new TimeOnly(8, 0), 1), new ReminderTime(new TimeOnly(9, 30), 0.5)], Database.ParseTimes("09:30*0.5;08:00"));

    [Theory]
    [InlineData(DaySet.Every, "every day")]
    [InlineData(DaySet.Monday | DaySet.Tuesday | DaySet.Wednesday | DaySet.Thursday | DaySet.Friday, "weekdays")]
    [InlineData(DaySet.Monday | DaySet.Wednesday | DaySet.Friday, "Mon, Wed, Fri")]
    public void DescribesTheDays(DaySet days, string text) => Assert.Equal(text, days.Describe());

    [Fact]
    public void GroupsKindsByWhatMattersMost()
    {
        Assert.Equal(AlertGroup.Medication, ReminderKind.Injection.Group());
        Assert.Equal(AlertGroup.Medication, ReminderKind.Other.Group());
        Assert.Equal(AlertGroup.Check, ReminderKind.BloodSugar.Group());
        Assert.Equal(AlertGroup.EatDrink, ReminderKind.Drink.Group());
        Assert.Equal(AlertGroup.Check, new[] { ReminderKind.Meal, ReminderKind.Weight }.Group());
        Assert.Equal(AlertGroup.Medication, new[] { ReminderKind.Drink, ReminderKind.BloodSugar, ReminderKind.Pill }.Group());
    }

    [Theory]
    [InlineData(1, "tablet", "1 tablet")]
    [InlineData(2, "tablet", "2 tablets")]
    [InlineData(0.5, "tablet", "½ tablet")]
    [InlineData(1.5, "tablet", "1½ tablets")]
    [InlineData(12, "unit", "12 units")]
    [InlineData(250, "mL", "250 mL")]
    [InlineData(2, "patch", "2 patches")]
    [InlineData(2, "puff", "2 puffs")]
    [InlineData(550, "kcal", "550 kcal")]
    public void WritesAmounts(double amount, string unit, string text) => Assert.Equal(text, Amounts.Format(amount, unit));
}
