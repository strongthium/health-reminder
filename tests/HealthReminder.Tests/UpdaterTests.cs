using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Strongthium.HealthReminder.Tests;

/// <summary>Asking for the latest release and installing it, with GitHub's answers made up here.</summary>
public sealed class UpdaterTests : IDisposable
{
    private const string Repository = "strongthium/test";
    private const string Program = "test.exe";
    private const string Files = "https://github.com/strongthium/test/releases/download/v2.3.4/";
    private static string Archive(string version) => $"test-{version}-win-x64.zip";

    private readonly string folder = Directory.CreateTempSubdirectory("updater-tests-").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private sealed class FakeGitHub(Func<string, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Asked { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!.ToString());
            return Task.FromResult(answer(request.RequestUri.ToString()));
        }
    }

    private static HttpResponseMessage Ok(string text) => new(HttpStatusCode.OK) { Content = new StringContent(text) };

    private static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private static string LatestJson(string tag = "v2.3.4", string filesAt = Files) => $$"""
        {
          "tag_name": "{{tag}}",
          "html_url": "https://github.com/strongthium/test/releases/tag/{{tag}}",
          "assets": [
            { "name": "test-2.3.4-win-x64.zip", "browser_download_url": "{{filesAt}}test-2.3.4-win-x64.zip" },
            { "name": "SHA256SUMS", "browser_download_url": "{{filesAt}}SHA256SUMS" }
          ]
        }
        """;

    private static byte[] Zip(string program, string text)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, $"test-2.3.4-win-x64/{program}", text);
            Add(zip, "test-2.3.4-win-x64/README.md", "Not the program.");
        }
        return memory.ToArray();

        static void Add(ZipArchive zip, string name, string text)
        {
            using var stream = zip.CreateEntry(name).Open();
            stream.Write(Encoding.UTF8.GetBytes(text));
        }
    }

    private static Updater UpdaterFor(FakeGitHub github) => new(new HttpClient(github), Repository, Archive, Program);

    private static FakeGitHub Serving(byte[] zip, string? sums = null) => new(url =>
    {
        if (url.StartsWith("https://api.github.com/", StringComparison.Ordinal)) return Ok(LatestJson());
        if (url.EndsWith("SHA256SUMS", StringComparison.Ordinal)) return Ok(sums ?? $"{Convert.ToHexString(SHA256.HashData(zip))}  {Archive("2.3.4")}\n");
        return Ok(zip);
    });

    private static readonly Release Latest = new(new Version(2, 3, 4), "https://github.com/strongthium/test/releases/tag/v2.3.4", new Dictionary<string, string>
    {
        [Archive("2.3.4")] = Files + Archive("2.3.4"),
        ["SHA256SUMS"] = Files + "SHA256SUMS",
    });

    [Fact]
    public async Task ReadsTheVersionPageAndFilesOfTheLatestRelease()
    {
        var github = new FakeGitHub(_ => Ok(LatestJson()));
        var release = await UpdaterFor(github).LatestAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new Version(2, 3, 4), release.Version);
        Assert.Equal("https://github.com/strongthium/test/releases/tag/v2.3.4", release.Page);
        Assert.Equal(2, release.Files.Count);
        Assert.Equal(["https://api.github.com/repos/strongthium/test/releases/latest"], github.Asked);
    }

    [Theory]
    [InlineData("nightly")]
    [InlineData("v2.3")]
    [InlineData("v2.3.4.5")]
    public async Task ATagThatIsNotAVersionIsAProblem(string tag)
    {
        var github = new FakeGitHub(_ => Ok(LatestJson(tag)));
        await Assert.ThrowsAsync<InvalidDataException>(() => UpdaterFor(github).LatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FilesFromAnywhereElseAreLeftOut()
    {
        var github = new FakeGitHub(_ => Ok(LatestJson(filesAt: "https://example.com/strongthium/test/releases/download/v2.3.4/")));
        var release = await UpdaterFor(github).LatestAsync(TestContext.Current.CancellationToken);
        Assert.Empty(release.Files);
    }

    [Fact]
    public async Task BeingToldToSlowDownIsSaidPlainly()
    {
        var github = new FakeGitHub(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var problem = await Assert.ThrowsAsync<HttpRequestException>(() => UpdaterFor(github).LatestAsync(TestContext.Current.CancellationToken));
        Assert.Contains("too many questions", problem.Message);
    }

    [Fact]
    public async Task InstallingReplacesTheProgramAndKeepsTheOldOneAside()
    {
        var running = Path.Combine(folder, Program);
        File.WriteAllText(running, "old");
        var cancel = TestContext.Current.CancellationToken;

        await UpdaterFor(Serving(Zip(Program, "new"))).InstallAsync(Latest, running, cancel: cancel);

        Assert.Equal("new", File.ReadAllText(running));
        Assert.Equal("old", File.ReadAllText(running + ".old"));
        Assert.False(File.Exists(running + ".new"));
        Assert.Equal(2, Directory.GetFiles(folder).Length);

        Updater.Undo(running);
        Assert.Equal("old", File.ReadAllText(running));
    }

    [Fact]
    public async Task ADownloadThatDoesNotMatchChangesNothing()
    {
        var running = Path.Combine(folder, Program);
        File.WriteAllText(running, "old");
        var github = Serving(Zip(Program, "tampered with"), $"{new string('0', 64)}  {Archive("2.3.4")}\n");

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdaterFor(github).InstallAsync(Latest, running, cancel: TestContext.Current.CancellationToken));

        Assert.Equal("old", File.ReadAllText(running));
        Assert.Single(Directory.GetFiles(folder));
    }

    [Fact]
    public async Task AZipWithoutTheProgramChangesNothing()
    {
        var running = Path.Combine(folder, Program);
        File.WriteAllText(running, "old");
        var github = Serving(Zip("other.exe", "new"));

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdaterFor(github).InstallAsync(Latest, running, cancel: TestContext.Current.CancellationToken));

        Assert.Equal("old", File.ReadAllText(running));
        Assert.Single(Directory.GetFiles(folder));
    }

    [Fact]
    public async Task AReleaseWithoutChecksumsIsNotInstalled()
    {
        var running = Path.Combine(folder, Program);
        File.WriteAllText(running, "old");
        var unchecked_ = Latest with { Files = new Dictionary<string, string> { [Archive("2.3.4")] = Files + Archive("2.3.4") } };

        await Assert.ThrowsAsync<InvalidDataException>(() => UpdaterFor(Serving(Zip(Program, "new"))).InstallAsync(unchecked_, running, cancel: TestContext.Current.CancellationToken));

        Assert.Equal("old", File.ReadAllText(running));
    }

    [Theory]
    [InlineData("abc  test.zip\ndef  other.zip\n", "other.zip", "def")]
    [InlineData("abc *test.zip\r\n", "test.zip", "abc")]
    [InlineData("abc  test.zip\n", "other.zip", null)]
    public void FindsAChecksumInTheList(string sums, string name, string? expected) =>
        Assert.Equal(expected, Updater.HashIn(sums, name));

    [Fact]
    public void TheRunningVersionHasThreeParts() => Assert.Equal(3, Updater.RunningVersion.ToString(3).Split('.').Length);

    [Fact]
    public void BeingUnableToWriteSaysWhere()
    {
        var running = Path.Combine(folder, Program);
        Assert.Contains(folder, Updater.Explain(new UnauthorizedAccessException(), running));
    }
}
