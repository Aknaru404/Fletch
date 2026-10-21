using System.Net;
using Avalonia;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using QuiverLauncher.Services;

namespace QuiverLauncher.Tests;

public class LauncherArtworkLoaderTests
{
    private const string Grid = "https://cdn2.steamgriddb.com/grid/93f56e33053be5d9e391480a49bcd0d0.png";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a7S8AAAAASUVORK5CYII=");

    [Theory]
    [InlineData(600, 900, 256, 156, 256, 384)]
    [InlineData(920, 430, 256, 156, 334, 156)]
    [InlineData(512, 512, 256, 156, 256, 256)]
    [InlineData(200, 300, 256, 156, 200, 300)]
    [InlineData(600, 900, 1024, 1024, 600, 900)]
    public void Artwork_is_shrunk_to_the_smallest_size_that_fills_its_card(int width, int height, double boxWidth, double boxHeight,
        int expectedWidth, int expectedHeight) =>
        LauncherArtworkLoader.FittedSize(new PixelSize(width, height), new Size(boxWidth, boxHeight))
            .Should().Be(new PixelSize(expectedWidth, expectedHeight));

    [AvaloniaFact]
    public async Task Decoded_artwork_is_reused_until_cards_grow()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quiver-artwork-tests", Guid.NewGuid().ToString("N"));
        var previous = LauncherArtworkLoader.ThumbnailBox;
        try
        {
            using var loader = new LauncherArtworkLoader(Path.Combine(directory, "Images"), new HttpClient(new Handler()));
            LauncherArtworkLoader.ThumbnailBox = () => new Size(256, 156);
            var first = await loader.ProvideImageAsync(Grid);
            (await loader.ProvideImageAsync(Grid)).Should().BeSameAs(first);
            LauncherArtworkLoader.ThumbnailBox = () => new Size(180, 124);
            (await loader.ProvideImageAsync(Grid)).Should().BeSameAs(first, "a larger copy serves smaller cards");
            LauncherArtworkLoader.ThumbnailBox = () => new Size(304, 220);
            (await loader.ProvideImageAsync(Grid)).Should().NotBeSameAs(first, "bigger cards need a bigger copy");
        }
        finally
        {
            LauncherArtworkLoader.ThumbnailBox = previous;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Full_size_artwork_is_asked_for_once_and_empty_sources_stay_empty()
    {
        var hero = LauncherArtworkLoader.FullSize("https://example.com/hero.png");
        hero.Should().NotBe("https://example.com/hero.png");
        LauncherArtworkLoader.FullSize(hero).Should().Be(hero);
        LauncherArtworkLoader.FullSize(null).Should().BeNull();
        LauncherArtworkLoader.FullSize("").Should().Be("");
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void Art_is_never_sent_through_an_image_resizer(double scale)
    {
        var previous = LauncherArtworkLoader.DisplayScale;
        try
        {
            LauncherArtworkLoader.DisplayScale = () => scale;
            var hero = "https://cdn2.steamgriddb.com/hero/5f5048350d1ed3a2227930926411f64c.png";
            foreach (var url in new[]
                     {
                         Grid, hero,
                         "https://cdn2.steamgriddb.com/icon/52967a3855319c3e7ac5731091dda96e.png",
                         "https://raw.githubusercontent.com/owner/repo/main/icon.png",
                         "https://github.com/owner.png?size=256",
                     })
            {
                LauncherArtworkLoader.CardUrl(url).Should().Be(url);
                LauncherArtworkLoader.BannerUrl(url).Should().Be(url);
            }
        }
        finally { LauncherArtworkLoader.DisplayScale = previous; }
    }

    [AvaloniaFact]
    public async Task Card_art_downloads_only_from_its_own_address_and_is_cached()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quiver-artwork-tests", Guid.NewGuid().ToString("N"));
        var handler = new Handler();
        try
        {
            using (var loader = new LauncherArtworkLoader(Path.Combine(directory, "Images"), new HttpClient(handler)))
                (await loader.ProvideImageAsync(Grid)).Should().NotBeNull();

            handler.Requests.Should().Equal(Grid);
            File.Exists(LauncherArtworkLoader.CachedPath(directory, Grid)).Should().BeTrue();

            handler.Requests.Clear();
            using (var loader = new LauncherArtworkLoader(Path.Combine(directory, "Images"), new HttpClient(handler)))
                (await loader.ProvideImageAsync(Grid)).Should().NotBeNull();
            handler.Requests.Should().BeEmpty("the cached copy is used on the next start");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.OriginalString);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Png) });
        }
    }
}
