using Alify.Core.Models;
using Alify.Features.Spotify.Events;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Alify.Features.Spotify.Services
{
    internal sealed class SseClient
    {
        public string Id { get; }
        public HttpResponse Response { get; }
        public Channel<string> Channel { get; }
        public CancellationTokenSource Cts { get; }

        public SseClient(string id, HttpResponse response)
        {
            Id = id;
            Response = response;
            Cts = new CancellationTokenSource();
            Channel = System.Threading.Channels.Channel.CreateBounded<string>(
                new BoundedChannelOptions(50)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleWriter = false,
                    SingleReader = true
                });
        }
    }

    /// <summary>
    /// Thread-safe Server-Sent Events (SSE) broadcast service (ALI-012 / AUD-006).
    /// Uses bounded per-client channels with DropOldest backpressure and automatic client disconnect cleanup.
    /// </summary>
    public class SseService : ISseService, ISPotifyObserver, ISpotifySubject
    {
        private readonly ILogger<SseService> _logger;
        private readonly ConcurrentDictionary<string, SseClient> _connections = new();
        private readonly ConcurrentDictionary<ISPotifyObserver, byte> _observers = new();

        public SseService(ILogger<SseService> logger)
        {
            _logger = logger;
            // The service observes itself to broadcast to SSE clients
            AddObserver(this);
        }

        // --- ISseService Implementation ---
        public string AddConnection(HttpResponse response)
        {
            var connectionId = Guid.NewGuid().ToString("N");
            var client = new SseClient(connectionId, response);
            _connections.TryAdd(connectionId, client);

            // Launch background processing loop for this client
            _ = ProcessClientChannelAsync(client);

            _logger.LogInformation("New bounded SSE connection established: {ConnectionId}", connectionId);
            return connectionId;
        }

        public void RemoveConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var client))
            {
                client.Channel.Writer.TryComplete();
                try
                {
                    client.Cts.Cancel();
                }
                catch (ObjectDisposedException) { }

                _logger.LogInformation("SSE connection closed: {ConnectionId}", connectionId);
            }
        }

        public async Task SendEventAsync(string eventName, object data)
        {
            await SendSseMessageAsync(eventName, data);
        }

        public void Cleanup()
        {
            foreach (var key in _connections.Keys)
            {
                RemoveConnection(key);
            }
            _logger.LogInformation("All SSE connections cleared during cleanup");
        }

        // --- ISpotifyObserver Implementation ---
        public async Task SendTrackSkippedEventAsync(Track track)
        {
            await SendSseMessageAsync("trackSkipped", new
            {
                trackName = track.FullTrack?.Name ?? "Unknown",
                artist = track.FullTrack?.Artists?.FirstOrDefault()?.Name ?? "Unknown",
                reason = "Explicit or inappropriate content"
            });
        }

        public async Task SendPlaybackInfoAsync(SpotifyPlaybackInfo playbackInfo)
        {
            await SendSseMessageAsync("playbackInfo", playbackInfo);
        }

        private Task SendSseMessageAsync(string eventName, object data)
        {
            var message = $"event: {eventName}\ndata: {JsonSerializer.Serialize(data)}\n\n";

            foreach (var client in _connections.Values)
            {
                // Non-blocking write into bounded per-client queue
                if (!client.Channel.Writer.TryWrite(message))
                {
                    _logger.LogWarning("SSE channel capacity reached for client {ConnectionId}; dropped oldest event", client.Id);
                }
            }

            return Task.CompletedTask;
        }

        private async Task ProcessClientChannelAsync(SseClient client)
        {
            try
            {
                var reader = client.Channel.Reader;
                while (await reader.WaitToReadAsync(client.Cts.Token))
                {
                    while (reader.TryRead(out var message))
                    {
                        await client.Response.WriteAsync(message, client.Cts.Token);
                        await client.Response.Body.FlushAsync(client.Cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when client disconnects or connection is removed
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SSE client write error for connection {ConnectionId}; cleaning up", client.Id);
            }
            finally
            {
                _connections.TryRemove(client.Id, out _);
                client.Cts.Dispose();
            }
        }

        // --- ISpotifySubject Implementation ---
        public void AddObserver(ISPotifyObserver observer)
        {
            _observers.TryAdd(observer, 0);
        }

        public void RemoveObserver(ISPotifyObserver observer)
        {
            _observers.TryRemove(observer, out _);
        }

        public async Task NotifyTrackSkippedEventAsync(Track track)
        {
            foreach (var observer in _observers.Keys)
            {
                try
                {
                    await observer.SendTrackSkippedEventAsync(track);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error notifying observer of track skipped event");
                }
            }
        }

        public async Task NotifyPlaybackInfoAsync(SpotifyPlaybackInfo playbackInfo)
        {
            foreach (var observer in _observers.Keys)
            {
                try
                {
                    await observer.SendPlaybackInfoAsync(playbackInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error notifying observer of playback info event");
                }
            }
        }
    }
}