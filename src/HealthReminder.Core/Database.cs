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
    // refills, and the kitchen's stock; 8: the places in the kitchen; 9: brands and packs; 10: Claude's ideas picked for
    // a meal; 11: food eaten, logged by hand; 12: the stock by id, so that two bags of a thing can be kept apart; 13: what's
    // been in the kitchen, kept to buy again, and the brand to buy on the list. Tables of their own that the CREATE adds.
    private const int SchemaVersion = 13;
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
                use_by   TEXT,                       -- when it's best used by, if it was said (from a photo)
                note     TEXT                        -- the brand to buy, if one was said
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
                id      INTEGER PRIMARY KEY,           -- two bags of one thing are two, of the same name
                name    TEXT NOT NULL COLLATE NOCASE,
                amount  REAL NOT NULL,                 -- in unit; below zero when more was used than was counted
                unit    TEXT NOT NULL DEFAULT '',      -- the catalog's ("g", "egg"), or from the amount typed ("g", "mL", "")
                use_by  TEXT,
                counted TEXT NOT NULL,                 -- when it was last counted (bought, photographed or checked)
                unsure  INTEGER NOT NULL DEFAULT 0     -- the amount is a guess: from a photo, or used past zero
            );
            CREATE TABLE IF NOT EXISTS place (
                id   INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                kind TEXT NOT NULL,                    -- Kept: Freezer, Fridge, Pantry, Cupboard, Counter
                sort INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS exercise_log (
                day     TEXT PRIMARY KEY,           -- "yyyy-MM-dd"
                title   TEXT NOT NULL,
                place   TEXT NOT NULL,              -- Outside, Inside
                minutes INTEGER NOT NULL,
                kcal    INTEGER NOT NULL DEFAULT 0,
                done    TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS idea (
                id    TEXT PRIMARY KEY,             -- "idea-…", as meal_choice has it
                slot  TEXT NOT NULL,                -- MealSlot
                idea  TEXT NOT NULL,                -- Claude's KitchenIdea, as JSON
                saved TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS food_log (
                id     INTEGER PRIMARY KEY,
                at     TEXT NOT NULL,
                name   TEXT NOT NULL,
                amount TEXT NOT NULL DEFAULT '',  -- as typed: "2", "150 g"
                kcal   INTEGER NOT NULL DEFAULT 0,
                pantry TEXT,                       -- the stock it came from, if any
                used   REAL NOT NULL DEFAULT 0,     -- how much came off it, in its unit
                unit   TEXT NOT NULL DEFAULT '',
                meal   TEXT                        -- the meal it was had in place of: its Dose.Key
            );
            CREATE INDEX IF NOT EXISTS food_log_at ON food_log (at);
            CREATE TABLE IF NOT EXISTS stocked (
                name      TEXT NOT NULL COLLATE NOCASE,
                note      TEXT NOT NULL DEFAULT '' COLLATE NOCASE,  -- the brand; '' for none
                quantity  TEXT NOT NULL DEFAULT '',  -- how much came in the last time it was bought, as typed
                icon TEXT, color TEXT, food_group TEXT, place INTEGER,
                pack_word TEXT, size REAL, size_unit TEXT, size_of TEXT, per_pack REAL,
                bought    INTEGER NOT NULL DEFAULT 0,  -- how many times it came in
                first     TEXT NOT NULL,
                last      TEXT NOT NULL,
                forgotten INTEGER NOT NULL DEFAULT 0,  -- taken out of the history, until it's bought again
                PRIMARY KEY (name, note)
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
        // 7: the freezer, and best-before dates set by hand. The dates kept before are left as they were.
        AddColumn("pantry", "added", "TEXT");
        AddColumn("pantry", "frozen", "TEXT");
        if (AddColumn("pantry", "use_by_set", "INTEGER NOT NULL DEFAULT 0")) Execute("UPDATE pantry SET use_by_set = 1 WHERE use_by IS NOT NULL");
        // 8: the places things are kept, and the picture, colour and group picked for each. Null is what its name says.
        AddColumn("pantry", "place", "INTEGER");
        AddColumn("pantry", "icon", "TEXT");
        AddColumn("pantry", "color", "TEXT");
        AddColumn("pantry", "food_group", "TEXT");
        // 9: a second name (a brand), and the packs something's in and what a piece of it weighs.
        AddColumn("pantry", "note", "TEXT");
        AddColumn("pantry", "packs", "REAL");
        AddColumn("pantry", "pack_word", "TEXT");
        AddColumn("pantry", "size", "REAL");                // g, or mL with size_unit 'mL'
        AddColumn("pantry", "size_unit", "TEXT");
        AddColumn("pantry", "size_of", "TEXT");             // 'each' piece, or a whole 'pack'
        AddColumn("pantry", "per_pack", "REAL");
        // 12: things of the same name kept apart (a 900 g bag and a 1.5 kg one), each by an id of its own; and the food
        // logged by the one it came from.
        if (!HasColumn("pantry", "id"))
        {
            const string columns = "name, amount, unit, use_by, counted, unsure, added, frozen, use_by_set, place, icon, color, food_group, "
                                 + "note, packs, pack_word, size, size_unit, size_of, per_pack";
            // All or nothing: the stock is never left half copied.
            Execute($"""
                BEGIN;
                CREATE TABLE pantry_kept (
                    id INTEGER PRIMARY KEY, name TEXT NOT NULL COLLATE NOCASE, amount REAL NOT NULL, unit TEXT NOT NULL DEFAULT '',
                    use_by TEXT, counted TEXT NOT NULL, unsure INTEGER NOT NULL DEFAULT 0, added TEXT, frozen TEXT,
                    use_by_set INTEGER NOT NULL DEFAULT 0, place INTEGER, icon TEXT, color TEXT, food_group TEXT,
                    note TEXT, packs REAL, pack_word TEXT, size REAL, size_unit TEXT, size_of TEXT, per_pack REAL);
                INSERT INTO pantry_kept ({columns}) SELECT {columns} FROM pantry ORDER BY name COLLATE NOCASE;
                DROP TABLE pantry;
                ALTER TABLE pantry_kept RENAME TO pantry;
                COMMIT;
                """);
        }
        AddColumn("food_log", "pantry_id", "INTEGER");
        // 13: the brand to buy; and what's been in the kitchen, from what's there and what was bought off the list.
        AddColumn("grocery", "note", "TEXT");
        if (version < 13)
            Execute("""
                BEGIN;
                INSERT OR IGNORE INTO stocked (name, note, icon, color, food_group, place, pack_word, size, size_unit, size_of, per_pack, first, last)
                    SELECT name, coalesce(note, ''), icon, color, food_group, place, pack_word, size, size_unit, size_of, per_pack,
                           coalesce(added, counted), coalesce(added, counted)
                    FROM pantry ORDER BY id;
                INSERT INTO stocked (name, note, quantity, bought, first, last)
                    SELECT g.name, '', (SELECT l.quantity FROM grocery l WHERE l.name = g.name COLLATE NOCASE AND l.bought IS NOT NULL ORDER BY l.bought DESC LIMIT 1),
                           count(*), min(g.bought), max(g.bought)
                    FROM grocery g WHERE g.bought IS NOT NULL GROUP BY g.name COLLATE NOCASE
                    ON CONFLICT (name, note) DO UPDATE SET quantity = excluded.quantity, bought = excluded.bought,
                        first = min(first, excluded.first), last = max(last, excluded.last);
                COMMIT;
                """);
        if (Convert.ToInt64(Scalar("SELECT count(*) FROM place"), CultureInfo.InvariantCulture) == 0)
            foreach (var place in KitchenPlace.Defaults) Save(place);
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
                    Note = r["note"] as string,
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
                ? "INSERT INTO grocery (name, quantity, price, bought, added, cleared, used, use_by, note) VALUES ($name, $quantity, $price, $bought, $added, $cleared, $used, $useBy, $note) RETURNING id"
                : "UPDATE grocery SET name = $name, quantity = $quantity, price = $price, bought = $bought, cleared = $cleared, used = $used, use_by = $useBy, note = $note WHERE id = $id RETURNING id";
            var p = command.Parameters;
            p.AddWithValue("$id", item.Id);
            p.AddWithValue("$note", item.Note?.Trim() is { Length: > 0 } note ? note : DBNull.Value);
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

    /// <summary>Keeps an idea from Claude so it can be picked for a meal; its id, for <see cref="SetChoice"/>. Ideas over
    /// two weeks old are let go, as the choices are.</summary>
    public string SaveIdea(KitchenIdea idea, MealSlot slot)
    {
        var id = "idea-" + Guid.NewGuid().ToString("N")[..12];
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO idea (id, slot, idea, saved) VALUES ($id, $slot, $idea, $saved);
                DELETE FROM idea WHERE saved < $old;
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$slot", slot.ToString());
            command.Parameters.AddWithValue("$idea", System.Text.Json.JsonSerializer.Serialize(idea, AnswerJson.Default.KitchenIdea));
            command.Parameters.AddWithValue("$saved", DateTime.Now.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$old", DateTime.Now.AddDays(-14).ToString("s", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
        return id;
    }

    /// <summary>A recipe by its id: one of the app's, or an idea from Claude that was kept; null when there's none.</summary>
    public Recipe? Recipe(string? id)
    {
        if (Recipes.Get(id) is { } recipe) return recipe;
        if (id is null || !id.StartsWith("idea-", StringComparison.Ordinal)) return null;
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT slot, idea FROM idea WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            using var r = command.ExecuteReader();
            if (!r.Read()) return null;
            var idea = System.Text.Json.JsonSerializer.Deserialize(r.GetString(1), AnswerJson.Default.KitchenIdea);
            return idea?.ToRecipe(Enum.TryParse<MealSlot>(r.GetString(0), out var slot) ? slot : MealSlot.Dinner, id);
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
            command.CommandText = """
                SELECT name, amount, unit, use_by, counted, unsure, added, frozen, use_by_set, place, icon, color, food_group,
                       note, packs, pack_word, size, size_unit, size_of, per_pack, id
                FROM pantry ORDER BY name COLLATE NOCASE, id
                """;
            using var r = command.ExecuteReader();
            var items = new List<PantryItem>();
            DateTime? When(int column) => r.IsDBNull(column) ? null : DateTime.Parse(r.GetString(column), CultureInfo.InvariantCulture);
            string? Text(int column) => r.IsDBNull(column) ? null : r.GetString(column);
            double? Number(int column) => r.IsDBNull(column) ? null : r.GetDouble(column);
            while (r.Read())
            {
                var packing = new Packing(Number(14), Text(15) ?? "pack", Number(16), Text(17) == "mL", Text(18) == "pack", Number(19));
                items.Add(new(r.GetString(0), r.GetDouble(1), r.GetString(2), When(3), When(4)!.Value, r.GetInt64(5) != 0,
                    When(6) ?? default, When(7), r.GetInt64(8) != 0,
                    r.IsDBNull(9) ? null : r.GetInt64(9), Text(10), Text(11), Enum.TryParse<FoodGroup>(Text(12), out var group) ? group : null,
                    Text(13), packing.IsEmpty ? null : packing, r.GetInt64(20)));
            }
            return items;
        }
    }

    /// <summary>Something in the kitchen kept: a new one (its <see cref="PantryItem.Id"/> 0) added beside any of its
    /// name, or one there is changed. It's given back with its id.</summary>
    public PantryItem Save(PantryItem item)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            const string names = "name, amount, unit, use_by, counted, unsure, added, frozen, use_by_set, place, icon, color, food_group, "
                               + "note, packs, pack_word, size, size_unit, size_of, per_pack";
            const string values = "$name, $amount, $unit, $useBy, $counted, $unsure, $added, $frozen, $useBySet, $place, $icon, $color, $group, "
                                + "$note, $packs, $packWord, $size, $sizeUnit, $sizeOf, $perPack";
            // One taken off since (while it was open) comes back under its id.
            command.CommandText = item.Id == 0
                ? $"INSERT INTO pantry ({names}) VALUES ({values}) RETURNING id"
                : $"INSERT OR REPLACE INTO pantry (id, {names}) VALUES ($id, {values}) RETURNING id";
            command.Parameters.AddWithValue("$id", item.Id);
            object Or(object? value) => value ?? DBNull.Value;
            var packing = item.Packing;
            command.Parameters.AddWithValue("$note", Or(item.Note is { Length: > 0 } note ? note.Trim() : null));
            command.Parameters.AddWithValue("$packs", Or(packing?.Packs));
            command.Parameters.AddWithValue("$packWord", Or(packing?.Word));
            command.Parameters.AddWithValue("$size", Or(packing?.Size));
            command.Parameters.AddWithValue("$sizeUnit", Or(packing is null ? null : packing.Volume ? "mL" : "g"));
            command.Parameters.AddWithValue("$sizeOf", Or(packing is null ? null : packing.OfPack ? "pack" : "each"));
            command.Parameters.AddWithValue("$perPack", Or(packing?.PerPack));
            command.Parameters.AddWithValue("$place", item.Place is { } place ? place : DBNull.Value);
            command.Parameters.AddWithValue("$icon", (object?)item.Icon ?? DBNull.Value);
            command.Parameters.AddWithValue("$color", (object?)item.Color ?? DBNull.Value);
            command.Parameters.AddWithValue("$group", item.Group is { } group ? group.ToString() : DBNull.Value);
            command.Parameters.AddWithValue("$added", item.Added == default ? DBNull.Value : item.Added.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$frozen", item.Frozen is { } frozen ? frozen.ToString("s", CultureInfo.InvariantCulture) : DBNull.Value);
            command.Parameters.AddWithValue("$useBySet", item.UseBySet);
            command.Parameters.AddWithValue("$name", item.Name.Trim());
            command.Parameters.AddWithValue("$amount", item.Amount);
            command.Parameters.AddWithValue("$unit", item.Unit);
            command.Parameters.AddWithValue("$useBy", item.UseBy is { } by ? by.ToString("s", CultureInfo.InvariantCulture) : DBNull.Value);
            command.Parameters.AddWithValue("$counted", item.Counted.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$unsure", item.Unsure);
            var saved = item with { Id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) };
            Stock(saved, "", null);
            return saved;
        }
    }

    /// <summary>Something put in the kitchen as bought: how much came, and when, kept to buy it again.</summary>
    public void Bought(PantryItem item, string quantity, DateTime at)
    {
        lock (gate) Stock(item, quantity, at);
    }

    /// <summary>
    /// What's kept of something that's been in the kitchen, by its name and brand: how it's kept (its look, as it is
    /// now), and, when it was bought (<paramref name="boughtAt"/>), how much came and when, one time more; bought, it's
    /// back in the history if it was taken out.
    /// </summary>
    private void Stock(PantryItem item, string quantity, DateTime? boughtAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO stocked (name, note, quantity, icon, color, food_group, place, pack_word, size, size_unit, size_of, per_pack, bought, first, last)
            VALUES ($name, $note, $quantity, $icon, $color, $group, $place, $packWord, $size, $sizeUnit, $sizeOf, $perPack, $bought, $at, $at)
            ON CONFLICT (name, note) DO UPDATE SET
                icon = excluded.icon, color = excluded.color, food_group = excluded.food_group, place = excluded.place,
                pack_word = excluded.pack_word, size = excluded.size, size_unit = excluded.size_unit, size_of = excluded.size_of,
                per_pack = excluded.per_pack,
                quantity = CASE WHEN $bought = 1 AND excluded.quantity <> '' THEN excluded.quantity ELSE quantity END,
                bought = bought + $bought,
                last = CASE WHEN $bought = 1 THEN excluded.last ELSE last END,
                forgotten = CASE WHEN $bought = 1 THEN 0 ELSE forgotten END
            """;
        object Or(object? value) => value ?? DBNull.Value;
        var p = command.Parameters;
        var packing = item.Packing;
        p.AddWithValue("$name", item.Name.Trim());
        p.AddWithValue("$note", item.Note?.Trim() ?? "");
        p.AddWithValue("$quantity", quantity.Trim());
        p.AddWithValue("$icon", Or(item.Icon));
        p.AddWithValue("$color", Or(item.Color));
        p.AddWithValue("$group", Or(item.Group?.ToString()));
        p.AddWithValue("$place", Or(item.Place));
        p.AddWithValue("$packWord", Or(packing?.Word));
        p.AddWithValue("$size", Or(packing?.Size));
        p.AddWithValue("$sizeUnit", Or(packing is null ? null : packing.Volume ? "mL" : "g"));
        p.AddWithValue("$sizeOf", Or(packing is null ? null : packing.OfPack ? "pack" : "each"));
        p.AddWithValue("$perPack", Or(packing?.PerPack));
        p.AddWithValue("$bought", boughtAt is null ? 0 : 1);
        p.AddWithValue("$at", (boughtAt ?? item.Since).ToString("s", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    /// <summary>What's been in the kitchen and isn't taken out of the history, by name and brand.</summary>
    public List<StockedThing> Stocked() => ReadStocked("WHERE forgotten = 0 ORDER BY name, note");

    /// <summary>What there's been of a name, taken out of the history or not, the one last bought first.</summary>
    public List<StockedThing> StockedNamed(string name) =>
        ReadStocked("WHERE name = $name ORDER BY last DESC", ("$name", name.Trim()));

    /// <summary>Taken out of the history, until it's bought again.</summary>
    public void Forget(StockedThing thing)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE stocked SET forgotten = 1 WHERE name = $name AND note = $note";
            command.Parameters.AddWithValue("$name", thing.Name);
            command.Parameters.AddWithValue("$note", thing.Note);
            command.ExecuteNonQuery();
        }
    }

    private List<StockedThing> ReadStocked(string where, params (string Name, object Value)[] parameters)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name, note, quantity, icon, color, food_group, place, pack_word, size, size_unit, size_of, per_pack, bought, first, last FROM stocked " + where;
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            using var r = command.ExecuteReader();
            var things = new List<StockedThing>();
            string? Text(int column) => r.IsDBNull(column) ? null : r.GetString(column);
            double? Number(int column) => r.IsDBNull(column) ? null : r.GetDouble(column);
            DateTime When(int column) => DateTime.Parse(r.GetString(column), CultureInfo.InvariantCulture);
            while (r.Read())
            {
                var packing = new Packing(null, Text(7) ?? "pack", Number(8), Text(9) == "mL", Text(10) == "pack", Number(11));
                var like = new PantryItem(r.GetString(0), 0, "", null, When(14), false, Added: When(13),
                    Place: r.IsDBNull(6) ? null : r.GetInt64(6), Icon: Text(3), Color: Text(4),
                    Group: Enum.TryParse<FoodGroup>(Text(5), out var group) ? group : null,
                    Note: r.GetString(1) is { Length: > 0 } note ? note : null, Packing: packing.IsEmpty ? null : packing);
                things.Add(new StockedThing(like, r.GetString(2), r.GetInt32(12), When(13), When(14)));
            }
            return things;
        }
    }

    public void DeletePantry(PantryItem item)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM pantry WHERE id = $id";
            command.Parameters.AddWithValue("$id", item.Id);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>The places in the kitchen, in the order they're shown.</summary>
    public List<KitchenPlace> Places()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, name, kind, sort FROM place ORDER BY sort, id";
            using var r = command.ExecuteReader();
            var places = new List<KitchenPlace>();
            while (r.Read())
                places.Add(new(r.GetInt64(0), r.GetString(1), Enum.TryParse<Kept>(r.GetString(2), out var kind) ? kind : Kept.Pantry, r.GetInt32(3)));
            return places;
        }
    }

    /// <summary>A place added (an Id of 0) or changed; its Id.</summary>
    public long Save(KitchenPlace place)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = place.Id == 0
                ? "INSERT INTO place (name, kind, sort) VALUES ($name, $kind, $sort); SELECT last_insert_rowid()"
                : "UPDATE place SET name = $name, kind = $kind, sort = $sort WHERE id = $id; SELECT $id";
            command.Parameters.AddWithValue("$id", place.Id);
            command.Parameters.AddWithValue("$name", place.Name.Trim());
            command.Parameters.AddWithValue("$kind", place.Kind.ToString());
            command.Parameters.AddWithValue("$sort", place.Sort);
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>A place taken away: what was in it goes back to where its kind goes.</summary>
    public void DeletePlace(long id)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE pantry SET place = NULL WHERE place = $id; DELETE FROM place WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    /// <summary>The food logged as eaten from <paramref name="from"/> on, oldest first.</summary>
    public List<FoodEaten> FoodLog(DateTime from)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT id, at, name, amount, kcal, pantry, used, unit, meal, pantry_id FROM food_log WHERE at >= $from ORDER BY at, id";
            command.Parameters.AddWithValue("$from", from.ToString("s", CultureInfo.InvariantCulture));
            using var r = command.ExecuteReader();
            var log = new List<FoodEaten>();
            while (r.Read())
                log.Add(new(r.GetInt64(0), DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture), r.GetString(2), r.GetString(3), r.GetInt32(4),
                    r.IsDBNull(5) ? null : r.GetString(5), r.GetDouble(6), r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetInt64(9)));
            return log;
        }
    }

    /// <summary>Food eaten, logged; its id is set.</summary>
    public FoodEaten Save(FoodEaten food)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = food.Id == 0
                ? "INSERT INTO food_log (at, name, amount, kcal, pantry, used, unit, meal, pantry_id) VALUES ($at, $name, $amount, $kcal, $pantry, $used, $unit, $meal, $pantryId) RETURNING id"
                : "UPDATE food_log SET at = $at, name = $name, amount = $amount, kcal = $kcal, pantry = $pantry, used = $used, unit = $unit, meal = $meal, pantry_id = $pantryId WHERE id = $id RETURNING id";
            command.Parameters.AddWithValue("$id", food.Id);
            command.Parameters.AddWithValue("$at", food.At.ToString("s", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$name", food.Name.Trim());
            command.Parameters.AddWithValue("$amount", food.Amount.Trim());
            command.Parameters.AddWithValue("$kcal", food.Kcal);
            command.Parameters.AddWithValue("$pantry", (object?)food.Pantry ?? DBNull.Value);
            command.Parameters.AddWithValue("$used", food.Used);
            command.Parameters.AddWithValue("$unit", food.Unit);
            command.Parameters.AddWithValue("$meal", (object?)food.Meal ?? DBNull.Value);
            command.Parameters.AddWithValue("$pantryId", (object?)food.PantryId ?? DBNull.Value);
            return food with { Id = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) };
        }
    }

    public void Delete(FoodEaten food)
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM food_log WHERE id = $id";
            command.Parameters.AddWithValue("$id", food.Id);
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

    /// <summary>Adds a column if the table doesn't have it; true when it was added.</summary>
    private bool AddColumn(string table, string column, string type)
    {
        if (HasColumn(table, column)) return false;
        Execute($"ALTER TABLE {table} ADD COLUMN {column} {type}");
        return true;
    }

    private bool HasColumn(string table, string column) =>
        Convert.ToInt64(Scalar($"SELECT count(*) FROM pragma_table_info('{table}') WHERE name = '{column}'"), CultureInfo.InvariantCulture) != 0;

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
