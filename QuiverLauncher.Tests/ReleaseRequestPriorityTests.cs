using System.Net;
using QuiverLauncher.Core.Services;

namespace QuiverLauncher.Tests;

public class ReleaseRequestPriorityTests
{
    [Fact]
    public async Task Interactive_waiter_promotes_shared_background_request()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paths = new List<string>();
        using var client = new HttpClient(new Handler(async (request, token) =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            if (paths.Count == 1) { started.SetResult(); await release.Task.WaitAsync(token); }
            return new(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }));
        var coordinator = ReleaseRequestCoordinator.For(client);
        // Fill the provider's slots so later requests queue.
        coordinator.MaxConcurrentRequestsPerProvider = 1;
        Task Fetch(string path) => coordinator.FetchAsync(client, new Uri("https://api.github.com/" + path), "github", null, _ => [], TestContext.Current.CancellationToken);
        var first = Fetch("active");
        await started.Task;
        var background = Fetch("background");
        var shared = Fetch("shared");
        Task interactive;
        using (ReleaseRequestCoordinator.PrioritizeInteractiveChecks()) interactive = Fetch("shared");
        await Task.Delay(100, TestContext.Current.CancellationToken);
        release.SetResult();
        await Task.WhenAll(first, background, shared, interactive);
        Assert.Equal(new[] { "/active", "/shared", "/background" }, paths);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
    [Fact]
    public async Task Interactive_request_overtakes_queued_background_work_but_never_exceeds_the_provider_limit()
    {
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paths = new List<string>();
        var active = 0;
        using var client = new HttpClient(new Handler(async (r, ct) =>
        {
            Assert.Equal(1, Interlocked.Increment(ref active));
            paths.Add(r.RequestUri!.AbsolutePath);
            if (paths.Count == 1) { firstStarted.SetResult(); await releaseFirst.Task.WaitAsync(ct); }
            Interlocked.Decrement(ref active);
            return new(HttpStatusCode.OK) { Content = new StringContent("[]") };
        }));
        var coordinator = ReleaseRequestCoordinator.For(client);
        // Fill the provider's slots so later requests queue.
        coordinator.MaxConcurrentRequestsPerProvider = 1;
        Task Fetch(string path) => coordinator.FetchAsync(client, new Uri("https://api.github.com/" + path), "github", null, _ => [], TestContext.Current.CancellationToken);
        var first = Fetch("first"); await firstStarted.Task;
        var background = Fetch("background");
        Task foreground;
        using (ReleaseRequestCoordinator.PrioritizeInteractiveChecks()) foreground = Fetch("foreground");
        await Task.Delay(100, TestContext.Current.CancellationToken);
        releaseFirst.SetResult();
        await Task.WhenAll(first, background, foreground);
        Assert.Equal(new[] { "/first", "/foreground", "/background" }, paths);
    }
}
