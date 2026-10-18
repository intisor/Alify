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
    public async Task FreeLyricsProvider_FallsBackToLyricsOvhWhenLrclibReturns404()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri!.Host == "lrclib.net")
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
