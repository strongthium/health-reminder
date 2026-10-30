using System.IO.Pipes;

namespace Strongthium.HealthReminder;

internal static class Program
{
    /// <summary>Started from Main, rather than by a designer or a test that only makes the App: only then does it open
    /// the reminders, put itself in the tray and ring.</summary>
    public static bool Running { get; private set; }

    /// <summary>Started at sign-in: stay in the tray until a dose is due.</summary>
    public static bool StartMinimized { get; private set; }

    /// <summary>Where the reminders are kept: <see cref="Database.DefaultDirectory"/>, or the folder given with
    /// <c>--data</c> (to keep them on a USB stick, say, or to try the program out without touching them).</summary>
    public static string DataDirectory { get; private set; } = Database.DefaultDirectory;

    /// <summary>Whether <see cref="DataDirectory"/> was given with <c>--data</c>.</summary>
    public static bool OwnDataDirectory { get; private set; }

    // One a user and data folder.
    private static string PipeName => "strongthium-health-reminder-" + Environment.UserName
        + (OwnDataDirectory ? "-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DataDirectory.ToUpperInvariant())))[..16] : "");

    [STAThread]
    private static int Main(string[] args)
    {
        StartMinimized = args.Contains("--minimized");
        var data = Array.IndexOf(args, "--data");
        if (data >= 0 && data + 1 < args.Length)
        {
            DataDirectory = Path.GetFullPath(args[data + 1]);
            OwnDataDirectory = true;
        }
        // Updated: the copy that started this one is quitting, and lets go of the lock as it does.
        var waitFor = Array.IndexOf(args, "--wait-for");
        Updater.Tidy(waitFor >= 0 && waitFor + 1 < args.Length && int.TryParse(args[waitFor + 1], out var pid) ? pid : null);

        Directory.CreateDirectory(DataDirectory);

        // One copy a user: two would ring twice. Starting it again shows the copy that's running.
        FileStream instanceLock;
        try
        {
            instanceLock = new FileStream(Path.Combine(DataDirectory, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            if (!StartMinimized) SignalRunningCopy();
            return 0;
        }

        using (instanceLock)
        {
            Running = true;
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
    }

    private static void SignalRunningCopy()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(2000);
            client.WriteByte(1);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or UnauthorizedAccessException)
        {
            // The other copy is starting or closing; nothing to show.
        }
    }

    /// <summary>Calls <paramref name="show"/> each time health-reminder is started again while this copy runs.</summary>
    public static void ListenForOtherCopies(Action show)
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
                    server.WaitForConnection();
                    if (server.ReadByte() == 1) show();
                }
                catch (IOException)
                {
                    Thread.Sleep(1000);
                }
            }
        }) { IsBackground = true, Name = "health-reminder pipe" };
        thread.Start();
    }
}

