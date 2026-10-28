using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Strongthium.HealthReminder;

/// <summary>What came of recording: a video (its path under the data directory and its SHA-256), or done without one.</summary>
public sealed record RecordOutcome(string Video, string Sha256)
{
    public static readonly RecordOutcome WithoutVideo = new("", "");
}

/// <summary>
/// Records a video of the user taking their medication with the webcam. It starts recording as soon as the camera is
/// up, shows what it records, and stops at <see cref="App.VideoSeconds"/> or when told to. Each frame carries the date,
/// time and what is being taken. <see cref="Outcome"/> is null when cancelled.
/// </summary>
public sealed partial class RecordWindow : Window
{
    private readonly Camera camera;
    private readonly DispatcherTimer timer;
    private readonly string relativePath;
    private readonly string fullPath;
    private WriteableBitmap? bitmap;
    private long shownFrame = -1;
    private bool saving, saved;

    public RecordOutcome? Outcome { get; private set; }

    /// <param name="what">What is being taken, a line each: "Metformin 500 mg: Take 2 tablets".</param>
    public RecordWindow(IReadOnlyList<string> what)
    {
        InitializeComponent();
        Icon = App.Icon;
        WhatText.Text = string.Join("\n", what);
        var now = DateTime.Now;
        var slug = string.Concat(what[0].Split(':')[0].ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        if (slug.Length > 40) slug = slug[..40];
        relativePath = Path.Combine("videos", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), $"{now:HHmmss}-{slug}.mp4");
        fullPath = Path.Combine(App.Current.Database.Directory, relativePath);

        camera = new Camera(App.Current.Camera, string.Join(" · ", what));
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
        Closed += (_, _) =>
        {
            timer.Stop();
            camera.Dispose();
            // Closed with its X while recording: the part recorded isn't evidence of anything, so it goes.
            if (!saved && File.Exists(fullPath)) File.Delete(fullPath);
        };
    }

    private void Tick()
    {
        if (camera.Error is { } error)
        {
            Fail(error);
            return;
        }
        if (!camera.Ready) return;
        if (!camera.Recording && !saving)
        {
            camera.StartRecording(fullPath);
            StopButton.IsEnabled = true;
            Placeholder.Show(false);
        }
        if (camera.FrameNumber != shownFrame)
        {
            shownFrame = camera.FrameNumber;
            if (bitmap is null)
            {
                bitmap = new WriteableBitmap(camera.Width, camera.Height, 96, 96, PixelFormats.Bgr32, null);
                Preview.Source = bitmap;
            }
            bitmap.Lock();
            camera.CopyFrame(bitmap.BackBuffer, bitmap.BackBufferStride);
            bitmap.AddDirtyRect(new Int32Rect(0, 0, camera.Width, camera.Height));
            bitmap.Unlock();
        }
        if (camera.Recording && !saving)
        {
            var left = TimeSpan.FromSeconds(App.Current.VideoSeconds) - camera.Recorded;
            StatusText.Text = $"Recording with {camera.CameraName}: {camera.Recorded:m\\:ss}. Show the pill, then take it. It stops by itself in {Math.Max(0, (int)left.TotalSeconds)} s.";
            if (left <= TimeSpan.Zero) _ = Save();
        }
    }

    private void Fail(string error)
    {
        timer.Stop();
        Placeholder.Show(true);
        Placeholder.Text = "No video: " + error;
        StatusText.Text = "";
        StopButton.IsEnabled = false;
        WithoutButton.Show(true);
    }

    private async Task Save()
    {
        if (saving) return;
        saving = true;
        StopButton.IsEnabled = false;
        StatusText.Text = "Saving the video…";
        var path = await camera.StopRecordingAsync();
        if (path is null || !File.Exists(path))
        {
            saving = false;
            Fail(camera.Error ?? "the video couldn't be saved.");
            return;
        }
        var sha256 = await Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        });
        saved = true;
        Outcome = new RecordOutcome(relativePath, sha256);
        DialogResult = true;
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _ = Save();

    private void Without_Click(object sender, RoutedEventArgs e)
    {
        Outcome = RecordOutcome.WithoutVideo;
        DialogResult = true;
    }

    private async void Cancel_Click(object sender, RoutedEventArgs e)
    {
        saving = true;
        var path = await camera.StopRecordingAsync();
        if (path is not null) File.Delete(path);
        DialogResult = false;
    }
}

/// <summary>Recording the doses that ask for a video, before they are marked done.</summary>
internal static class Evidence
{
    /// <summary>The doses' lines on the video: "Metformin 500 mg: Take 2 tablets".</summary>
    public static List<string> Lines(IEnumerable<Dose> doses) =>
        doses.Select(d => $"{d.Reminder.Name}: {Format.Instruction(d.Reminder, d.Amount)}").ToList();

    /// <summary>Records a video for the doses that ask for one; WithoutVideo when none does, null when cancelled.</summary>
    public static RecordOutcome? Record(Window owner, IReadOnlyList<Dose> doses)
    {
        var filmed = doses.Where(d => d.Reminder.RecordVideo).ToList();
        if (filmed.Count == 0) return RecordOutcome.WithoutVideo;
        var window = new RecordWindow(Lines(filmed)) { Owner = owner };
        return window.ShowDialog() == true ? window.Outcome : null;
    }

    /// <summary>The words of the button of a dose that is recorded.</summary>
    public static string ButtonText(ReminderKind kind) => kind switch
    {
        _ when kind.IsMedication() => "Record taking it",
        _ when kind.IsCheck() => "Record and save",
        ReminderKind.Meal => "Record eating",
        ReminderKind.Drink => "Record drinking",
        _ => "Record it",
    };
}
