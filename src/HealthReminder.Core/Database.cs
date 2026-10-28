using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Strongthium.HealthReminder;

/// <summary>
/// The reminders, their schedule and what was done about each, in one SQLite file. One connection, used from one
/// thread at a time: every call takes the lock.
/// </summary>
public sealed class Database : IDisposable
{
    // 3: the grocery list and the cigarettes; 4: the menu and the kitchen; 5: exercise; 6: medication on hand and
    // refills, and the kitchen's stock. Tables of their own that the
    // CREATE adds.
    private const int SchemaVersion = 6;
    private readonly SqliteConnection connection;
    private readonly Lock gate = new();

    /// <summary>Where the database and the photos are kept: %LocalAppData%\Strongthium\health-reminder.</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "Strongthium", "health-reminder");

    public string Directory { get; }
    public string ImageDirectory => Path.Combine(Directory, "images");
    /// <summary>The videos recorded as evidence, a folder a day. Deleting a reminder leaves its videos.</summary>
    public string VideoDirectory => Path.Combine(Directory, "videos");

    public Database(string directory) : this(directory, Path.Combine(directory, "health-reminder.db")) { }

    /// <summary>A database in memory, for tests; photos go to a temporary directory.</summary>
    internal Database() : this(Path.Combine(Path.GetTempPath(), "health-reminder-" + Guid.NewGuid().ToString("N")), ":memory:") { }

    private Database(string directory, string file)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(ImageDirectory);
        connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file }.ToString());
        connection.Open();
        Migrate();
    }

    private void Migrate()
    {
        Execute("PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;");
        var version = Convert.ToInt32(Scalar("PRAGMA user_version"), CultureInfo.InvariantCulture);
        if (version >= SchemaVersion) return;
        Execute("""
            CREATE TABLE IF NOT EXISTS reminder (
                id           INTEGER PRIMARY KEY,
                kind         TEXT NOT NULL,             -- ReminderKind
                name         TEXT NOT NULL,
                unit         TEXT NOT NULL DEFAULT '',
                notes        TEXT NOT NULL DEFAULT '',
                times        TEXT NOT NULL DEFAULT '',  -- "08:00*2;20:00*1": time * amount
                days         INTEGER NOT NULL DEFAULT 127,
                active       INTEGER NOT NULL DEFAULT 1,
                created      TEXT NOT NULL,
                planned      INTEGER NOT NULL DEFAULT 0,
                colors       TEXT NOT NULL DEFAULT '',  -- "WHITE;BLUE"
                shape        TEXT NOT NULL DEFAULT '',
                size_mm      REAL NOT NULL DEFAULT 0,
                imprint      TEXT NOT NULL DEFAULT '',
                score        INTEGER NOT NULL DEFAULT 1,
                form         TEXT NOT NULL DEFAULT '',
                set_id       TEXT NOT NULL DEFAULT '',
                ndc          TEXT NOT NULL DEFAULT '',
                rxcui        TEXT NOT NULL DEFAULT '',
                label_title  TEXT NOT NULL DEFAULT '',
                photo        TEXT NOT NULL DEFAULT '',
                photo_source TEXT NOT NULL DEFAULT '',
                sites        TEXT NOT NULL DEFAULT '',  -- "Abdomen, left;Abdomen, right"
                value_unit   TEXT NOT NULL DEFAULT '',
                record_video INTEGER NOT NULL DEFAULT 0,
                din          TEXT NOT NULL DEFAULT '',  -- Canada's Drug Identification Number
                rx_number    TEXT NOT NULL DEFAULT '',  -- the pharmacy's prescription number
                stock_count  REAL,                      -- how many on hand when counted (null: not counted)
                stock_at     TEXT                       -- when it was counted
            );
            CREATE TABLE IF NOT EXISTS dose_log (
                reminder_id INTEGER NOT NULL REFERENCES reminder(id) ON DELETE CASCADE,
                due         TEXT NOT NULL,              -- local time, "yyyy-MM-ddTHH:mm"
                status      TEXT NOT NULL,              -- Taken, Skipped
                at          TEXT NOT NULL,
                amount      REAL NOT NULL DEFAULT 0,
                value       TEXT NOT NULL DEFAULT '',   -- a check's reading: "6.4", "128/82"
                site        TEXT NOT NULL DEFAULT '',   -- an injection's site
                video       TEXT NOT NULL DEFAULT '',   -- the video of it, under videos/
                video_sha256 TEXT NOT NULL DEFAULT '',  -- the video's SHA-256 when it was made
                PRIMARY KEY (reminder_id, due)
            );
            CREATE INDEX IF NOT EXISTS dose_log_due ON dose_log (due);
            CREATE TABLE IF NOT EXISTS setting (
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS grocery (
                id       INTEGER PRIMARY KEY,
                name     TEXT NOT NULL,
                quantity TEXT NOT NULL DEFAULT '',  -- as typed: "2 L", "1 gallon", "500 g", "12"
                price    TEXT,                      -- paid, a decimal: "3.49"
                bought   TEXT,
                added    TEXT NOT NULL,
                cleared  INTEGER NOT NULL DEFAULT 0, -- bought on an earlier trip
                used     TEXT,                       -- when it was used up
                use_by   TEXT                        -- when it's best used by, if it was said (from a photo)
            );
            CREATE TABLE IF NOT EXISTS smoke (
                id       INTEGER PRIMARY KEY,
                started  TEXT NOT NULL,
                finished TEXT,                      -- when it was timed and put out
                brand    TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS smoke_started ON smoke (started);
            CREATE TABLE IF NOT EXISTS menu (
                weekday INTEGER NOT NULL,           -- 0 Sunday to 6 Saturday
                meal    TEXT NOT NULL,              -- the meal reminder's name
                recipe  TEXT NOT NULL,              -- a recipe's id, or "decide" to choose on the day
                decided TEXT NOT NULL,
                PRIMARY KEY (weekday, meal)
            );
            CREATE TABLE IF NOT EXISTS meal_choice (
                day     TEXT NOT NULL,              -- "yyyy-MM-dd": what was picked for one day, over the menu
                meal    TEXT NOT NULL,
                recipe  TEXT NOT NULL,
                decided TEXT NOT NULL,
                PRIMARY KEY (day, meal)
            );
            CREATE TABLE IF NOT EXISTS kitchen_done (
                key  TEXT PRIMARY KEY,              -- KitchenEvent.Key
                done TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS refill (
                reminder_id INTEGER NOT NULL REFERENCES reminder(id) ON DELETE CASCADE,
                at          TEXT NOT NULL,
                amount      REAL NOT NULL              -- in the reminder's unit
            );
            CREATE TABLE IF NOT EXISTS pantry (
                name    TEXT PRIMARY KEY COLLATE NOCASE,
                amount  REAL NOT NULL,                 -- in unit; below zero when more was used than was counted
                unit    TEXT NOT NULL DEFAULT '',      -- the catalog's ("g", "egg"), or from the amount typed ("g", "mL", "")
                use_by  TEXT,
                counted TEXT NOT NULL,                 -- when it was last counted (bought, photographed or checked)
                unsure  INTEGER NOT NULL DEFAULT 0     -- the amount is a guess: from a photo, or used past zero
            );
            CREATE TABLE IF NOT EXISTS exercise_log (
                day     TEXT PRIMARY KEY,           -- "yyyy-MM-dd"
                title   TEXT NOT NULL,
                place   TEXT NOT NULL,              -- Outside, Inside
                minutes INTEGER NOT NULL,
                kcal    INTEGER NOT NULL DEFAULT 0,
                done    TEXT NOT NULL
            );
            """);
        // 1 to 2: the DIN and the prescription number.
        if (version == 1) Execute("ALTER TABLE reminder ADD COLUMN din TEXT NOT NULL DEFAULT ''; ALTER TABLE reminder ADD COLUMN rx_number TEXT NOT NULL DEFAULT '';");
        // 3 and up: what's used up in the kitchen, and use-by days read from photos. By column, not by version: a build
        // marked 4 had the grocery table without them.
        AddColumn("grocery", "used", "TEXT");
        AddColumn("grocery", "use_by", "TEXT");
        // 6: medication on hand.
        AddColumn("reminder", "stock_count", "REAL");
        AddColumn("reminder", "stock_at", "TEXT");
        Execute($"PRAGMA user_version = {SchemaVersion}");
    }

    public List<Reminder> Reminders()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM reminder ORDER BY active DESC, name COLLATE NOCASE";
            using var reader = command.ExecuteReader();
            var list = new List<Reminder>();
            while (reader.Read()) list.Add(Read(reader));
            return list;
        }
    }

    private static Reminder Read(SqliteDataReader r)
    {
        string Text(string column) => r.GetString(r.GetOrdinal(column));
        return new Reminder
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Kind = Enum.TryParse<ReminderKind>(Text("kind"), out var kind) ? kind : ReminderKind.Other,
            Name = Text("name"),
            Unit = Text("unit"),
            Notes = Text("notes"),
            Times = ParseTimes(Text("times")),
            Days = (DaySet)r.GetInt32(r.GetOrdinal("days")),
            Active = r.GetBoolean(r.GetOrdinal("active")),
            Created = DateTime.Parse(Text("created"), CultureInfo.InvariantCulture),
            Planned = r.GetBoolean(r.GetOrdinal("planned")),
            Appearance = new PillAppearance(
                Text("colors").Split(';', StringSplitOptions.RemoveEmptyEntries),
                Text("shape"),
                r.GetDouble(r.GetOrdinal("size_mm")),
                Text("imprint"),
                r.GetInt32(r.GetOrdinal("score"))),
            Form = Text("form"),
            SetId = Text("set_id"),
            Ndc = Text("ndc"),
            RxCui = Text("rxcui"),
            LabelTitle = Text("label_title"),
            Photo = Text("photo"),
            PhotoSource = Text("photo_source"),
            Sites = Text("sites").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
            ValueUnit = Text("value_unit"),
            RecordVideo = r.GetBoolean(r.GetOrdinal("record_video")),
            Din = Text("din"),
            RxNumber = Text("rx_number"),
            StockCount = r.IsDBNull(r.GetOrdinal("stock_count")) ? null : r.GetDouble(r.GetOrdinal("stock_count")),
            StockAt = r.IsDBNull(r.GetOrdinal("stock_at")) ? null : DateTime.Parse(Text("stock_at"), CultureInfo.InvariantCulture),
        };
    }

    /// <summary>Adds the reminder (Id 0) or updates it; returns its id.</summary>
    public long Save(Reminder m)
    {
        m.Times = m.Times.GroupBy(t => t.Time).Select(g => g.First()).OrderBy(t => t.Time).ToList();
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = m.Id == 0
                ? """
                  INSERT INTO reminder (kind, name, unit, notes, times, days, active, created, planned, colors, shape, size_mm, imprint, score,
                                        form, set_id, ndc, rxcui, label_title, photo, photo_source, sites, value_unit, record_video, din, rx_number, stock_count, stock_at)
                  VALUES ($kind, $name, $unit, $notes, $times, $days, $active, $created, $planned, $colors, $shape, $size, $imprint, $score,
                          $form, $setId, $ndc, $rxcui, $title, $photo, $photoSource, $sites, $valueUnit, $recordVideo, $din, $rxNumber, $stockCount, $stockAt)
                  RETURNING id
                  """
                : """
                  UPDATE reminder SET kind = $kind, name = $name, unit = $unit, notes = $notes, times = $times, days = $days, active = $active,
                      planned = $planned, colors = $colors, shape = $shape, size_mm = $size, imprint = $imprint, score = $score, form = $form,
                      set_id = $setId, ndc = $ndc, rxcui = $rxcui, label_title = $title, photo = $photo, photo_source = $photoSource,
                      sites = $sites, value_unit = $valueUnit, record_video = $recordVideo, din = $din, rx_number = $rxNumber,
                      stock_count = $stockCount, stock_at = $stockAt
                  WHERE id = $id
                  RETURNING id
                  """;
            var p = command.Parameters;
            p.AddWithValue("$id", m.Id);
            p.AddWithValue("$kind", m.Kind.ToString());
            p.AddWithValue("$name", m.Name.Trim());
            p.AddWithValue("$unit", m.Unit.Trim());
            p.AddWithValue("$notes", m.Notes.Trim());
            p.AddWithValue("$times", FormatTimes(m.Times));
            p.AddWithValue("$days", (int)m.Days);
            p.AddWithValue("$active", m.Active);
            p.AddWithValue("$created", m.Created.ToString("s", CultureInfo.InvariantCulture));
            p.AddWithValue("$planned", m.Planned);
            p.AddWithValue("$colors", string.Join(';', m.Appearance.Colors));
            p.AddWithValue("$shape", m.Appearance.Shape);
            p.AddWithValue("$size", m.Appearance.SizeMm);
            p.AddWithValue("$imprint", m.Appearance.Imprint);
            p.AddWithValue("$score", m.Appearance.Score);
            p.AddWithValue("$form", m.Form);
            p.AddWithValue("$setId", m.SetId);
            p.AddWithValue("$ndc", m.Ndc);
            p.AddWithValue("$rxcui", m.RxCui);
            p.AddWithValue("$title", m.LabelTitle);
            p.AddWithValue("$photo", m.Photo);
            p.AddWithValue("$photoSource", m.PhotoSource);
            p.AddWithValue("$sites", string.Join(';', m.Sites));
            p.AddWithValue("$valueUnit", m.ValueUnit);
            p.AddWithValue("$recordVideo", m.RecordVideo);
            p.AddWithValue("$din", m.Din.Trim());
            p.AddWithValue("$rxNumber", m.RxNumber.Trim());
            p.AddWithValue("$stockCount", m.StockCount is { } count ? (object)count : DBNull.Value);
            p.AddWithValue("$stockAt", m.StockAt is { } at ? at.ToString("s", CultureInfo.InvariantCulture) : DBNull.Value);
            m.Id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return m.Id;
        }
    }

    public void Delete(Reminder m)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM reminder WHERE id = $id";
            command.Parameters.AddWithValue("$id", m.Id);
            command.ExecuteNonQuery();
        }
        if (m.Photo.Length > 0 && !Reminders().Any(other => other.Photo == m.Photo))
            File.Delete(Path.Combine(ImageDirectory, m.Photo));
    }

    public void Log(Dose dose, DoseRecord record)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR REPLACE INTO dose_log (reminder_id, due, status, at, amount, value, site, video, video_sha256)
                VALUES ($id, $due, $status, $at, $amount, $value, $site, $video, $videoSha256)
                """;
            command.Parameters.AddWithValue("$video", record.Video);
            command.Parameters.AddWithValue("$videoSha256", record.VideoSha256);
            command.Parameters.AddWithValue("$id", dose.Reminder.Id);
            command.Parameters.AddWithValue("$due", FormatDue(dose.Due));
            command.Parameters.AddWithValue("$status", record.Status.ToString());
            command.Parameters.AddWithValue("$at", record.At.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$amount", record.Amount);
            command.Parameters.AddWithValue("$value", record.Value.Trim());
            command.Parameters.AddWithValue("$site", record.Site);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>What was done about each dose due from <paramref name="from"/> on, by <see cref="Dose.Key"/>.</summary>
    public Dictionary<string, DoseRecord> LoggedSince(DateTime from)
    {
        var logged = new Dictionary<string, DoseRecord>();
        foreach (var (id, due, record) in Logs("WHERE due >= $from", from))
            logged[$"{id}@{FormatDue(due)}"] = record;
        return logged;
    }

    /// <summary>Everything done from <paramref name="from"/> on, newest first, with the reminder it was for.</summary>
    public List<HistoryEntry> History(DateTime from)
    {
        var reminders = Reminders().ToDictionary(r => r.Id);
        return Logs("WHERE due >= $from ORDER BY due DESC", from)
            .Where(l => reminders.ContainsKey(l.Id))
            .Select(l => new HistoryEntry(l.Id, reminders[l.Id].Name, reminders[l.Id].Kind, reminders[l.Id].Unit, l.Due, l.Record))
            .ToList();
    }

    /// <summary>The injection site used last for a reminder, or "" if none was recorded.</summary>
    public string LastSite(long reminderId)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT site FROM dose_log WHERE reminder_id = $id AND site <> '' ORDER BY at DESC LIMIT 1";
            command.Parameters.AddWithValue("$id", reminderId);
            return command.ExecuteScalar() as string ?? "";
        }
    }

    private List<(long Id, DateTime Due, DoseRecord Record)> Logs(string where, DateTime from)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT reminder_id, due, status, at, amount, value, site, video, video_sha256 FROM dose_log " + where;
            command.Parameters.AddWithValue("$from", FormatDue(from));
            using var reader = command.ExecuteReader();
            var logs = new List<(long, DateTime, DoseRecord)>();
            while (reader.Read())
                logs.Add((reader.GetInt64(0),
                    DateTime.ParseExact(reader.GetString(1), "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
                    new DoseRecord(Enum.Parse<DoseStatus>(reader.GetString(2)), DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                        reader.GetDouble(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8))));
            return logs;
        }
    }

    /// <summary>The grocery list: what's still to buy, oldest first, then what's been bought on this trip.</summary>
    public List<GroceryItem> Groceries() =>
        ReadGroceries("SELECT * FROM grocery WHERE cleared = 0 ORDER BY bought IS NOT NULL, coalesce(bought, added), id");

    /// <summary>The last time something of that name was bought with a price, or null.</summary>
    public GroceryItem? LastBought(string name) =>
        ReadGroceries("SELECT * FROM grocery WHERE name = $name COLLATE NOCASE AND bought IS NOT NULL AND price IS NOT NULL ORDER BY bought DESC LIMIT 1", ("$name", name.Trim()))
            .FirstOrDefault();

    /// <summary>What was bought from <paramref name="since"/> on, done shopping or not, newest first.</summary>
    public List<GroceryItem> BoughtSince(DateTime since) =>
        ReadGroceries("SELECT * FROM grocery WHERE bought >= $since ORDER BY bought DESC", ("$since", since.ToString("s", CultureInfo.InvariantCulture)));

    /// <summary>What's in the kitchen: bought (or seen in a photo) from <paramref name="since"/> on, and not used up.</summary>
    public List<GroceryItem> InKitchen(DateTime since) =>
        ReadGroceries("SELECT * FROM grocery WHERE bought >= $since AND used IS NULL ORDER BY bought DESC", ("$since", since.ToString("s", CultureInfo.InvariantCulture)));

    /// <summary>Everything that has been on the list, for typing ahead.</summary>
    public List<string> GroceryNames()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM grocery GROUP BY name COLLATE NOCASE ORDER BY count(*) DESC, name COLLATE NOCASE";
            using var reader = command.ExecuteReader();
            var names = new List<string>();
            while (reader.Read()) names.Add(reader.GetString(0));
            return names;
        }
    }

    private List<GroceryItem> ReadGroceries(string sql, params (string Name, object Value)[] parameters)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            using var r = command.ExecuteReader();
            var list = new List<GroceryItem>();
            DateTime? Time(string column) => r[column] is string s ? DateTime.Parse(s, CultureInfo.InvariantCulture) : null;
            while (r.Read())
                list.Add(new GroceryItem
                {
                    Id = (long)r["id"],
                    Name = (string)r["name"],
                    Quantity = (string)r["quantity"],
                    Price = r["price"] is string price ? decimal.Parse(price, CultureInfo.InvariantCulture) : null,
                    Bought = Time("bought"),
                    Added = Time("added") ?? DateTime.MinValue,
                    Cleared = (long)r["cleared"] != 0,
                    Used = Time("used"),
                    UseBy = Time("use_by"),
                });
            return list;
        }
    }

    public long Save(GroceryItem item)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = item.Id == 0
                ? "INSERT INTO grocery (name, quantity, price, bought, added, cleared, used, use_by) VALUES ($name, $quantity, $price, $bought, $added, $cleared, $used, $useBy) RETURNING id"
                : "UPDATE grocery SET name = $name, quantity = $quantity, price = $price, bought = $bought, cleared = $cleared, used = $used, use_by = $useBy WHERE id = $id RETURNING id";
            var p = command.Parameters;
            p.AddWithValue("$id", item.Id);
            p.AddWithValue("$name", item.Name.Trim());
            p.AddWithValue("$quantity", item.Quantity.Trim());
            p.AddWithValue("$price", item.Price is { } price ? price.ToString(CultureInfo.InvariantCulture) : DBNull.Value);
            p.AddWithValue("$bought", item.Bought is { } bought ? bought.ToString("s", CultureInfo.InvariantCulture) : DBNull.Value);
            p.AddWithValue("$added", (item.Added == default ? DateTime.Now : item.Added).ToString("s", CultureInfo.InvariantCulture));
            p.AddWithValue("$cleared", item.Cleared);
            p.AddWithValue("$used", item.Used is { } used ? used.ToString("s", CultureInfo.InvariantCulture) : DBNull.Value);
            p.AddWithValue("$useBy", item.UseBy is { } useBy ? useBy.ToString("s", CultureInfo.InvariantCulture) : DBNull.Value);
            item.Id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return item.Id;
        }
    }

    public void Delete(GroceryItem item)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM grocery WHERE id = $id";
            command.Parameters.AddWithValue("$id", item.Id);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>Done shopping: what was bought leaves the list, and is kept for its price.</summary>
    public void ClearBought()
    {
        lock (gate) Execute("UPDATE grocery SET cleared = 1 WHERE bought IS NOT NULL AND cleared = 0");
    }

    /// <summary>The cigarettes lit from <paramref name="from"/> on, oldest first.</summary>
    public List<Smoke> Smokes(DateTime from)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, started, finished, brand FROM smoke WHERE started >= $from ORDER BY started";
            command.Parameters.AddWithValue("$from", from.ToString("s", CultureInfo.InvariantCulture));
            using var r = command.ExecuteReader();
            var list = new List<Smoke>();
            while (r.Read())
                list.Add(new Smoke
                {
                    Id = r.GetInt64(0),
                    Started = DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture),
                    Finished = r.IsDBNull(2) ? null : DateTime.Parse(r.GetString(2), CultureInfo.InvariantCulture),
                    Brand = r.GetString(3),
                });
            return list;
        }
    }

    public long Save(Smoke smoke)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = smoke.Id == 0
                ? "INSERT INTO smoke (started, finished, brand) VALUES ($started, $finished, $brand) RETURNING id"
                : "UPDATE smoke SET started = $started, finished = $finished, brand = $brand WHERE id = $id RETURNING id";
            command.Parameters.AddWithValue("$id", smoke.Id);
            command.Parameters.AddWithValue("$started", smoke.Started.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$finished", smoke.Finished is { } f ? f.ToString("s", CultureInfo.InvariantCulture) : DBNull.Value);
            command.Parameters.AddWithValue("$brand", smoke.Brand.Trim());
            smoke.Id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return smoke.Id;
        }
    }

    public void Delete(Smoke smoke)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM smoke WHERE id = $id";
            command.Parameters.AddWithValue("$id", smoke.Id);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>The week's menu: for a weekday and a meal, a recipe's id (or "decide"), and when it was set.</summary>
    public Dictionary<(DayOfWeek Day, string Meal), (string Recipe, DateTime Decided)> Menu()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT weekday, meal, recipe, decided FROM menu";
            using var r = command.ExecuteReader();
            var menu = new Dictionary<(DayOfWeek, string), (string, DateTime)>();
            while (r.Read()) menu[((DayOfWeek)r.GetInt32(0), r.GetString(1))] = (r.GetString(2), DateTime.Parse(r.GetString(3), CultureInfo.InvariantCulture));
            return menu;
        }
    }

    /// <summary>Sets a weekday's meal to a recipe, "decide", or nothing (null: just an idea).</summary>
    public void SetMenu(DayOfWeek day, string meal, string? recipe)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = recipe is null
                ? "DELETE FROM menu WHERE weekday = $day AND meal = $meal"
                : "INSERT OR REPLACE INTO menu (weekday, meal, recipe, decided) VALUES ($day, $meal, $recipe, $decided)";
            command.Parameters.AddWithValue("$day", (int)day);
            command.Parameters.AddWithValue("$meal", meal);
            command.Parameters.AddWithValue("$recipe", recipe ?? "");
            command.Parameters.AddWithValue("$decided", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    /// <summary>What was picked for a meal on a day, over the week's menu, and when; null if nothing was.</summary>
    public (string Recipe, DateTime Decided)? Choice(DateOnly day, string meal)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT recipe, decided FROM meal_choice WHERE day = $day AND meal = $meal";
            command.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$meal", meal);
            using var r = command.ExecuteReader();
            return r.Read() ? (r.GetString(0), DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture)) : null;
        }
    }

    /// <summary>Picks a recipe for a meal on one day. Choices over two weeks old are let go.</summary>
    public void SetChoice(DateOnly day, string meal, string recipe)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR REPLACE INTO meal_choice (day, meal, recipe, decided) VALUES ($day, $meal, $recipe, $decided);
                DELETE FROM meal_choice WHERE day < $old;
                """;
            command.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$meal", meal);
            command.Parameters.AddWithValue("$recipe", recipe);
            command.Parameters.AddWithValue("$decided", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$old", day.AddDays(-14).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    /// <summary>The kitchen alerts done (their keys), from <paramref name="from"/> on.</summary>
    public HashSet<string> KitchenDone(DateTime from)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT key FROM kitchen_done WHERE key >= $from";
            command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
            using var r = command.ExecuteReader();
            var done = new HashSet<string>();
            while (r.Read()) done.Add(r.GetString(0));
            return done;
        }
    }

    public void SetKitchenDone(string key)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO kitchen_done (key, done) VALUES ($key, $done); DELETE FROM kitchen_done WHERE done < $old";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$done", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$old", DateTime.Now.AddDays(-30).ToString("s", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    // ---- Medication on hand -----------------------------------------------------------------------------------

    /// <summary>The refills received for a reminder, newest first.</summary>
    public List<(DateTime At, double Amount)> Refills(long reminderId)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT at, amount FROM refill WHERE reminder_id = $id ORDER BY at DESC";
            command.Parameters.AddWithValue("$id", reminderId);
            using var r = command.ExecuteReader();
            var refills = new List<(DateTime, double)>();
            while (r.Read()) refills.Add((DateTime.Parse(r.GetString(0), CultureInfo.InvariantCulture), r.GetDouble(1)));
            return refills;
        }
    }

    public void AddRefill(long reminderId, double amount, DateTime at)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO refill (reminder_id, at, amount) VALUES ($id, $at, $amount)";
            command.Parameters.AddWithValue("$id", reminderId);
            command.Parameters.AddWithValue("$at", at.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$amount", amount);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>How much has been marked taken since <paramref name="since"/>: only doses marked taken count, by when
    /// they were marked.</summary>
    public double TakenSince(long reminderId, DateTime since)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT coalesce(sum(amount), 0) FROM dose_log WHERE reminder_id = $id AND status = 'Taken' AND at >= $since";
            command.Parameters.AddWithValue("$id", reminderId);
            command.Parameters.AddWithValue("$since", since.ToString("s", CultureInfo.InvariantCulture));
            return Convert.ToDouble(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>How much of a medication is on hand: what was counted, plus refills since, less doses taken since; null
    /// when it isn't counted.</summary>
    public Supply? Supply(Reminder reminder)
    {
        if (reminder.StockCount is not { } count || reminder.StockAt is not { } at || reminder.Id == 0) return null;
        var refilled = Refills(reminder.Id).Where(r => r.At >= at).Sum(r => r.Amount);
        return new Supply(count + refilled - TakenSince(reminder.Id, at), reminder.DailyUse);
    }

    // ---- The kitchen's stock ------------------------------------------------------------------------------------

    public List<PantryItem> Pantry()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name, amount, unit, use_by, counted, unsure FROM pantry ORDER BY name COLLATE NOCASE";
            using var r = command.ExecuteReader();
            var items = new List<PantryItem>();
            while (r.Read())
                items.Add(new(r.GetString(0), r.GetDouble(1), r.GetString(2), r.IsDBNull(3) ? null : DateTime.Parse(r.GetString(3), CultureInfo.InvariantCulture),
                    DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture), r.GetInt64(5) != 0));
            return items;
        }
    }

    public void Save(PantryItem item)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO pantry (name, amount, unit, use_by, counted, unsure) VALUES ($name, $amount, $unit, $useBy, $counted, $unsure)";
            command.Parameters.AddWithValue("$name", item.Name.Trim());
            command.Parameters.AddWithValue("$amount", item.Amount);
            command.Parameters.AddWithValue("$unit", item.Unit);
            command.Parameters.AddWithValue("$useBy", item.UseBy is { } by ? by.ToString("s", CultureInfo.InvariantCulture) : DBNull.Value);
            command.Parameters.AddWithValue("$counted", item.Counted.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$unsure", item.Unsure);
            command.ExecuteNonQuery();
        }
    }

    public void DeletePantry(string name)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM pantry WHERE name = $name";
            command.Parameters.AddWithValue("$name", name.Trim());
            command.ExecuteNonQuery();
        }
    }

    /// <summary>The exercise done from <paramref name="from"/> on, a day at a time, oldest first.</summary>
    public List<ExerciseDone> ExerciseLog(DateOnly from)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT day, title, place, minutes, kcal, done FROM exercise_log WHERE day >= $from ORDER BY day";
            command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            using var r = command.ExecuteReader();
            var log = new List<ExerciseDone>();
            while (r.Read())
                log.Add(new(DateOnly.ParseExact(r.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture), r.GetString(1),
                    Enum.Parse<Place>(r.GetString(2)), r.GetInt32(3), r.GetInt32(4), DateTime.Parse(r.GetString(5), CultureInfo.InvariantCulture)));
            return log;
        }
    }

    /// <summary>The day's exercise done; a second time the same day replaces the first.</summary>
    public void LogExercise(ExerciseDone done)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO exercise_log (day, title, place, minutes, kcal, done) VALUES ($day, $title, $place, $minutes, $kcal, $done)";
            command.Parameters.AddWithValue("$day", done.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$title", done.Title);
            command.Parameters.AddWithValue("$place", done.Place.ToString());
            command.Parameters.AddWithValue("$minutes", done.Minutes);
            command.Parameters.AddWithValue("$kcal", done.Kcal);
            command.Parameters.AddWithValue("$done", done.Done.ToString("s", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
    }

    public string? Setting(string key)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM setting WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() as string;
        }
    }

    public void SetSetting(string key, string value)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO setting (key, value) VALUES ($key, $value)";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>"08:00*2;20:00*1" → the times and amounts. A time without "*amount" has the amount 1.</summary>
    public static List<ReminderTime> ParseTimes(string text) =>
        text.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Split('*'))
            .Select(p => new ReminderTime(TimeOnly.ParseExact(p[0], "HH:mm", CultureInfo.InvariantCulture),
                p.Length > 1 ? double.Parse(p[1], CultureInfo.InvariantCulture) : 1))
            .OrderBy(t => t.Time)
            .ToList();

    public static string FormatTimes(IEnumerable<ReminderTime> times) =>
        string.Join(';', times.Select(t => t.Time.ToString("HH:mm", CultureInfo.InvariantCulture) + "*" + t.Amount.ToString(CultureInfo.InvariantCulture)));

    private static string FormatDue(DateTime due) => due.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private void AddColumn(string table, string column, string type)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM pragma_table_info('{table}') WHERE name = '{column}'";
        if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0) Execute($"ALTER TABLE {table} ADD COLUMN {column} {type}");
    }

    private void Execute(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void Dispose() => connection.Dispose();
}
