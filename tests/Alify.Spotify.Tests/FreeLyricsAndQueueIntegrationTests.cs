using Alify.Features.Spotify.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Alify.Spotify.Tests;

public class FreeLyricsAndQueueIntegrationTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }

    [Fact]
    public void SanitizeTrackTitle_StripsFeaturesAndSpotifyNoise()
    {
        Assert.Equal("Cast", FreeLyricsProviderService.SanitizeTrackTitle("Cast (feat. ODUMODUBLVCK)"));
        Assert.Equal("Calm Down", FreeLyricsProviderService.SanitizeTrackTitle("Calm Down (with Selena Gomez)"));
        Assert.Equal("Unavailable", FreeLyricsProviderService.SanitizeTrackTitle("Unavailable [feat. Musa Keys] (Explicit)"));
        Assert.Equal("Essence", FreeLyricsProviderService.SanitizeTrackTitle("Essence - Remastered 2024"));
        Assert.Equal("Bohemian Rhapsody", FreeLyricsProviderService.SanitizeTrackTitle("Bohemian Rhapsody - Live"));
    }

    [Fact]
    public void SanitizeArtistName_ExtractsPrimaryArtist()
    {
        Assert.Equal("Shallipopi", FreeLyricsProviderService.SanitizeArtistName("Shallipopi feat. ODUMODUBLVCK"));
        Assert.Equal("Rema", FreeLyricsProviderService.SanitizeArtistName("Rema & Selena Gomez"));
        Assert.Equal("Wizkid", FreeLyricsProviderService.SanitizeArtistName("Wizkid, Tems"));
    }

    [Fact]
    public void ParseGeniusLyricsFromHtml_StripsAdsAndStitchesMultiContainers()
    {
        var sampleHtml = """
            <html>
            <body>
              <div id="lyrics-root">
                <div data-lyrics-container="true" class="Lyrics__Container">
                  <div data-exclude-from-selection="true" class="LyricsHeader">
                    <button>3 Contributors</button>
                    <span>Gbese Lyrics</span>
                  </div>
                  [Verse 1]<br/>
                  E ra moto one fifty thousand<br/>
                  Tonight, I'm ready to rock
                </div>
                <div class="inread-ad-wrapper" data-exclude-from-selection="true">
                  <div id="ad-slot-1">Special Discount Ad!</div>
                </div>
                <div data-lyrics-container="true" class="Lyrics__Container">
                </div>
                <div data-lyrics-container="true" class="Lyrics__Container">
                  [Chorus]<br/>
                  Gbese, gbese, gbe s'oke<br/>
                  Fa pala Jo guara guara oo
                </div>
              </div>
            </body>
            </html>
            """;

        var parsed = FreeLyricsProviderService.ParseGeniusLyricsFromHtml(sampleHtml);

        Assert.Contains("[Verse 1]", parsed);
        Assert.Contains("E ra moto one fifty thousand", parsed);
        Assert.Contains("[Chorus]", parsed);
        Assert.Contains("Gbese, gbese, gbe s'oke", parsed);
        Assert.DoesNotContain("3 Contributors", parsed);
        Assert.DoesNotContain("Special Discount Ad!", parsed);
    }

    [Fact]
    public async Task FreeLyricsProvider_ParsesPlainLyricsFromLrclib()
    {
        var sampleResponse = new
        {
            id = 12345,
            trackName = "Bohemian Rhapsody",
            artistName = "Queen",
            plainLyrics = "Mama, just killed a man\nPut a gun against his head",
            syncedLyrics = "[00:10.00] Mama, just killed a man"
        };

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.Host == "lrclib.net")
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(sampleResponse))
                };
            }
            return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
        });

        var httpClient = new HttpClient(handler);
        var service = new FreeLyricsProviderService(httpClient, NullLogger<FreeLyricsProviderService>.Instance);

        var lyrics = await service.GetLyricsAsync("Queen", "Bohemian Rhapsody");

        Assert.Equal("Mama, just killed a man\nPut a gun against his head", lyrics);
    }

    [Fact]
    public async Task FreeLyricsProvider_StripsSyncedTimestampsWhenPlainLyricsEmpty()
    {
        var sampleResponse = new
        {
            id = 12345,
            trackName = "Song",
            artistName = "Artist",
            plainLyrics = "",
            syncedLyrics = "[01:15.50] Line one\n[01:20.10] Line two"
        };

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.Host == "lrclib.net")
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(sampleResponse))
                };
            }
            return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
        });

        var httpClient = new HttpClient(handler);
        var service = new FreeLyricsProviderService(httpClient, NullLogger<FreeLyricsProviderService>.Instance);

        var lyrics = await service.GetLyricsAsync("Artist", "Song");

        Assert.Contains("Line one", lyrics);
        Assert.DoesNotContain("[01:15.50]", lyrics);
    }

    [Fact]
    public async Task FreeLyricsProvider_FallsBackToGeniusWhenLrclibReturns404()
    {
        var sampleGeniusSearch = new
        {
            response = new
            {
                sections = new[]
                {
                    new
                    {
                        type = "song",
                        hits = new[]
                        {
                            new
                            {
                                result = new
                                {
                                    url = "https://genius.com/Qdot-alagbe-gbese-lyrics"
                                }
                            }
                        }
                    }
                }
            }
        };

        var geniusPageHtml = """
            <html>
            <body>
              <div data-lyrics-container="true">
                <div data-exclude-from-selection="true">Ad</div>
                [Verse 1]<br/>
                Gbese lori ijo
              </div>
            </body>
            </html>
            """;

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.Host == "lrclib.net")
            {
                return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
            }
            if (req.RequestUri!.Host == "genius.com" && req.RequestUri.AbsolutePath.Contains("/api/search"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(sampleGeniusSearch))
                };
            }
            if (req.RequestUri!.Host == "genius.com" && req.RequestUri.AbsolutePath.Contains("gbese-lyrics"))
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(geniusPageHtml)
                };
            }
            return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
        });

        var httpClient = new HttpClient(handler);
        var service = new FreeLyricsProviderService(httpClient, NullLogger<FreeLyricsProviderService>.Instance);

        var lyrics = await service.GetLyricsAsync("Qdot", "Gbese");

        Assert.Contains("[Verse 1]", lyrics);
        Assert.Contains("Gbese lori ijo", lyrics);
        Assert.DoesNotContain("Ad", lyrics);
    }

    [Fact]
    public async Task FreeLyricsProvider_FallsBackToLyricsOvhWhenAllElseFails()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.Host == "lrclib.net" || req.RequestUri!.Host == "genius.com")
            {
                return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
            }
            if (req.RequestUri!.Host == "api.lyrics.ovh")
            {
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new { lyrics = "Fallback lyrics from ovh" }))
                };
            }
            return new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound };
        });

        var httpClient = new HttpClient(handler);
        var service = new FreeLyricsProviderService(httpClient, NullLogger<FreeLyricsProviderService>.Instance);

        var lyrics = await service.GetLyricsAsync("Unknown", "Track");

        Assert.Equal("Fallback lyrics from ovh", lyrics);
    }
}
