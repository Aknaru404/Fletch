using System.Net;
using FluentAssertions;
using QuiverLauncher.Core.Models;
using QuiverLauncher.Core.Services;
using QuiverLauncher.Models;
using QuiverLauncher.Services;

namespace QuiverLauncher.Tests;

public class StartupVersionRefreshTests
{
    private const string Release = """{"tag_name":"2.0","assets":[{"name":"app.zip","browser_download_url":"https://example.com/app.zip"}]}""";
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
    private static GameInfo App(string? pin = null) => new()
    {
        Repository = "startup-tests/" + Guid.NewGuid().ToString("N"), FolderName = Guid.NewGuid().ToString("N"),
        PreferredVersion = pin, InstalledVersion = "1.0", Status = GameStatus.Installed
    };
    private static HttpResponseMessage Ok(HttpRequestMessage request) => new(HttpStatusCode.OK)
    { Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/latest") ? Release : "[" + Release + "]") };

    [Fact]
    public async Task Expired_cache_is_display_only_and_success_clears_hint()
    {
        var app = App();
        GitHubApiCache.SetCache(null, app.Repository!, "2.0", "", new() { tag_name = "2.0" }, persist: false);
        GitHubApiCache.TryGetLastKnownVersion(null, app.Repository, out var cache).Should().BeTrue();
        cache!.LastChecked = DateTime.UtcNow.AddDays(-2);
        GitHubApiCache.TryGetCachedVersion(null, app.Repository, out _).Should().BeFalse();
        using var client = new HttpClient(new Handler((r, _) => Task.FromResult(Ok(r))));
        await GameStatusService.CheckStatusAsync(app, client, Path.GetTempPath(), checkRemoteVersion: false);
        app.LatestVersionLabel.Should().Be("Latest: 2.0 (pending check)");
        app.LatestVersionToolTip.Should().Contain("Verification is pending");
        app.LatestVersion.Should().BeNullOrEmpty();
        app.GetLatestRelease().Should().BeNull();
        app.Status.Should().Be(GameStatus.NotInstalled);
        (await new LibraryUpdateChecker(client, new()).CheckStartupAsync([app], TestContext.Current.CancellationToken)).Complete.Should().BeTrue();
        app.LatestVersionLabel.Should().Be("Latest: 2.0");
        app.LatestVersionToolTip.Should().BeNull();
    }

    [Fact]
    public async Task Failed_verification_preserves_hint_and_pin()
    {
        var app = App("1.5");
        app.ApplyLastKnownVersion("1.5");
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var result = await new LibraryUpdateChecker(client, new()).CheckStartupAsync([app], TestContext.Current.CancellationToken);
        result.Failed.Should().Be(1);
        app.LatestVersionLabel.Should().Be("Latest: 1.5 (pending check)");
        app.PreferredVersion.Should().Be("1.5");
        app.HasRepositoryCheckError.Should().BeTrue();
        app.Status.Should().Be(GameStatus.Installed);
    }

    [Fact]
    public async Task Startup_retries_only_transient_failures_once()
    {
        var good = App(); var transient = App(); var missing = App();
        var counts = new Dictionary<string, int>();
        using var client = new HttpClient(new Handler((r, _) =>
        {
            var path = r.RequestUri!.AbsolutePath;
            int count;
            // The checker sends requests concurrently.
            lock (counts) count = counts[path] = counts.GetValueOrDefault(path) + 1;
            var status = path.Contains(missing.Repository!) ? HttpStatusCode.NotFound :
                path.Contains(transient.Repository!) && count == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            return Task.FromResult(status == HttpStatusCode.OK ? Ok(r) : new HttpResponseMessage(status));
        }));
        var result = await new LibraryUpdateChecker(client, new()).CheckStartupAsync([good, transient, missing],
            TestContext.Current.CancellationToken, TimeSpan.Zero);
        result.Successful.Should().Be(2); result.Failed.Should().Be(1);
        counts.Where(x => x.Key.Contains(good.Repository!) || x.Key.Contains(missing.Repository!)).Should().OnlyContain(x => x.Value == 1);
        counts.Values.Should().OnlyContain(x => x <= 2);
        transient.LatestVersion.Should().Be("2.0");
    }

    [Theory]
    [InlineData(GameStatus.Downloading)]
    [InlineData(GameStatus.Installing)]
    [InlineData(GameStatus.Updating)]
    public async Task Metadata_refresh_preserves_active_operation_status(GameStatus status)
    {
        var app = App(); app.Status = status;
        using var client = new HttpClient(new Handler((r, _) => Task.FromResult(Ok(r))));
        await new LibraryUpdateChecker(client, new()).CheckStartupAsync([app], TestContext.Current.CancellationToken);
        app.Status.Should().Be(status);
    }

    [Fact]
    public async Task Shutdown_cancels_retry_delay()
    {
        using var cancel = new CancellationTokenSource();
        using var client = new HttpClient(new Handler((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))));
        var check = new LibraryUpdateChecker(client, new()).CheckStartupAsync([App()], cancel.Token, TimeSpan.FromMinutes(1));
        cancel.CancelAfter(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.WaitAsync(TimeSpan.FromSeconds(3)));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 1)]
    [InlineData(HttpStatusCode.Forbidden, 1)]
    [InlineData(HttpStatusCode.TooManyRequests, 1)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 4)]
    public async Task Retry_is_bounded_and_excludes_authentication_and_rate_limits(HttpStatusCode status, int expectedRequests)
    {
        var calls = 0;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(status));
        }));
        var result = await new LibraryUpdateChecker(client, new()).CheckStartupAsync([App()],
            TestContext.Current.CancellationToken, TimeSpan.Zero);
        result.Complete.Should().BeFalse();
        calls.Should().Be(expectedRequests);
    }
}
