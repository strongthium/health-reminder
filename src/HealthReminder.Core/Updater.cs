using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Strongthium.HealthReminder;

/// <summary>A release on GitHub: its version, its page, and where its files are, by name.</summary>
public sealed record Release(Version Version, string Page, IReadOnlyDictionary<string, string> Files);

/// <summary>
/// Asks GitHub for the latest release of this program, and puts it in the place of the running one. It asks only when
/// told to, and sends GitHub nothing but the question.
/// </summary>
public sealed class Updater(HttpClient http, string repository, Func<string, string> archiveName, string programFile)
{
    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(20);

    /// <summary>The version that's running, as major.minor.patch.</summary>
    public static Version RunningVersion { get; } = Plain(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    /// <summary>The releases' page on GitHub, for when one can't be installed from here.</summary>
    public string ReleasesPage => $"https://github.com/{repository}/releases/latest";

    /// <summary>A client for one program, which says who's asking, as GitHub requires. Downloads take as long as they
    /// take, so the time allowed is set a request at a time.</summary>
    public static HttpClient NewClient(string program)
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{program}/{RunningVersion.ToString(3)}");
        return client;
    }

    /// <summary>The latest release. Only files of this repository's own releases are kept from what GitHub answers.</summary>
    public async Task<Release> LatestAsync(CancellationToken cancel = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(AskTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, timeout.Token);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            throw new HttpRequestException("GitHub has had too many questions from this address for now. Try again in an hour.");
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(timeout.Token));
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v'), out var version) || version.Build < 0 || version.Revision >= 0)
            throw new InvalidDataException($"GitHub's latest release is called \"{tag}\", which isn't a version.");

        var ownFiles = $"https://github.com/{repository}/releases/download/";
        var files = new Dictionary<string, string>();
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("browser_download_url").GetString();
            if (name is not null && url is not null && url.StartsWith(ownFiles, StringComparison.Ordinal)) files[name] = url;
        }
        var page = root.GetProperty("html_url").GetString() is { } shown && shown.StartsWith($"https://github.com/{repository}/releases/", StringComparison.Ordinal)
            ? shown : ReleasesPage;
        return new Release(version, page, files);
    }

    /// <summary>Downloads a release and puts its program in the place of <paramref name="running"/>, which starts it
    /// the next time. Nothing is replaced unless the download matches the release's SHA256SUMS.</summary>
    public async Task InstallAsync(Release release, string running, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        var work = Directory.CreateTempSubdirectory("strongthium-update-");
        var fresh = running + ".new";
        try
        {
            var archive = await DownloadAsync(release, work.FullName, progress, cancel);
            Unpack(archive, programFile, fresh);
            Replace(fresh, running);
        }
        finally
        {
            work.Delete(recursive: true);
            File.Delete(fresh);
        }
    }

    /// <summary>Downloads a release's archive into a folder, checks it and says where it is.</summary>
    public async Task<string> DownloadAsync(Release release, string folder, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        var name = archiveName(release.Version.ToString(3));
        if (!release.Files.TryGetValue(name, out var url)) throw new InvalidDataException($"The release has no {name}.");
        if (!release.Files.TryGetValue("SHA256SUMS", out var sumsUrl)) throw new InvalidDataException("The release has no SHA256SUMS to check it against.");

        string expected;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
        {
            timeout.CancelAfter(AskTimeout);
            expected = HashIn(await http.GetStringAsync(sumsUrl, timeout.Token), name)
                ?? throw new InvalidDataException($"SHA256SUMS doesn't list {name}.");
        }

        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        var path = Path.Combine(folder, name);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (var source = await response.Content.ReadAsStreamAsync(cancel))
        using (var target = File.Create(path))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancel)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancel);
                hash.AppendData(buffer, 0, read);
                done += read;
                if (total > 0) progress?.Report((double)done / total.Value);
            }
        }
        if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The download of {name} doesn't match the release's SHA256SUMS, so nothing was changed.");
        return path;
    }

    /// <summary>The checksum a SHA256SUMS file gives a name (<c>hash  name</c>, a line each), if it does.</summary>
    public static string? HashIn(string sums, string name)
    {
        foreach (var line in sums.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && parts[1].TrimStart('*') == name) return parts[0];
        }
        return null;
    }

    /// <summary>Takes the program, and nothing else, out of a release's zip.</summary>
    public static void Unpack(string archive, string program, string destination)
    {
        using var zip = ZipFile.OpenRead(archive);
        var entry = zip.Entries.FirstOrDefault(found => found.Name == program)
            ?? throw new InvalidDataException($"The download doesn't hold {program}.");
        entry.ExtractToFile(destination, overwrite: true);
    }

    /// <summary>Puts a new program in the place of the running one. Windows lets a running program be renamed but not
    /// overwritten, so the old one steps aside, and is deleted by <see cref="Tidy"/> when the new one starts.</summary>
    public static void Replace(string fresh, string running)
    {
        if (OperatingSystem.IsWindows())
        {
            var old = running + ".old";
            File.Delete(old);
            File.Move(running, old);
            try
            {
                File.Move(fresh, running);
            }
            catch
            {
                File.Move(old, running);
                throw;
            }
        }
        else
        {
            File.SetUnixFileMode(fresh, File.GetUnixFileMode(running));
            File.Move(fresh, running, overwrite: true);
        }
    }

    /// <summary>Goes back to the old program, when the new one wouldn't start.</summary>
    public static void Undo(string running)
    {
        if (OperatingSystem.IsWindows()) File.Move(running + ".old", running, overwrite: true);
    }

    /// <summary>When a program starts: waits for the copy it's replacing to quit, then deletes that copy's file.</summary>
    public static void Tidy(int? waitFor = null)
    {
        if (waitFor is { } other)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(other);
                process.WaitForExit(15_000);
            }
            catch (ArgumentException)
            {
                // It has quit already.
            }
        }
        try
        {
            if (Environment.ProcessPath is { } path) File.Delete(path + ".old");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Still going, or not ours to delete: next time.
        }
    }

    /// <summary>Whether an exception from asking or installing is the kind to tell the user about.</summary>
    public static bool IsExpected(Exception exception) =>
        exception is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException
            or InvalidOperationException or IOException or UnauthorizedAccessException or InvalidDataException;

    /// <summary>What to tell the user, in a sentence.</summary>
    public static string Explain(Exception exception, string running) => exception switch
    {
        UnauthorizedAccessException => $"This program can't be replaced from here: {Path.GetDirectoryName(running)} needs an administrator to change it.",
        OperationCanceledException => "GitHub took too long to answer.",
        JsonException or KeyNotFoundException or InvalidOperationException => "GitHub's answer couldn't be read.",
        _ => exception.Message,
    };

    private static Version Plain(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
