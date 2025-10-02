using Alify.Core.Models;
using Alify.Features.Spotify.Events;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Alify.Features.Spotify.Services
{
    public class SseService : ISseService, ISPotifyObserver, ISpotifySubject
    {
        private readonly ILogger<SseService> _logger;
        private readonly ConcurrentDictionary<string, HttpResponse> _connections = new();
        private readonly List<ISPotifyObserver> _observers = [];

        public SseService(ILogger<SseService> logger)
        {
            _logger = logger;
            // The service observes itself to broadcast to SSE clients
            AddObserver(this);
        }

        // --- ISseService Implementation ---
        public string AddConnection(HttpResponse response)
        {
            var connectionId = Guid.NewGuid().ToString();
            _connections.TryAdd(connectionId, response);
            _logger.LogInformation("New SSE connection established: {ConnectionId}", connectionId);
            return connectionId;
        }
        
        public void RemoveConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out _))
            {
                _logger.LogInformation("SSE connection closed: {ConnectionId}", connectionId);
            }
        }

        public async Task SendEventAsync(string eventName, object data)
        {
            await SendSseMessageAsync(eventName, data);
        }

        public void Cleanup()
        {
            _connections.Clear();
            _logger.LogInformation("All SSE connections cleared during cleanup");
        }

        // --- ISpotifyObserver Implementation ---
        public async Task SendTrackSkippedEventAsync(Track track)
        {
            await SendSseMessageAsync("trackSkipped", new
            {
                trackName = track.FullTrack?.Name ?? "Unknown",
                artist = track.FullTrack?.Artists.FirstOrDefault()?.Name ?? "Unknown",
                reason = "Explicit or inappropriate content"
            });
        }

        public async Task SendPlaybackInfoAsync(SpotifyPlaybackInfo playbackInfo)
        {
            await SendSseMessageAsync("playbackInfo", playbackInfo);
        }

        private async Task SendSseMessageAsync(string eventName, object data)
        {
            var message = $"event: {eventName}\ndata: {JsonSerializer.Serialize(data)}\n\n";
            
            foreach (var connectionEntry in _connections)
            {
                var connectionId = connectionEntry.Key;
                var connection = connectionEntry.Value;
                try
                {
                    await connection.WriteAsync(message);
                    await connection.Body.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending SSE message to connection {ConnectionId}", connectionId);
                }
            }
        }

        // --- ISpotifySubject Implementation ---
        public void AddObserver(ISPotifyObserver observer)
        {
            if (!_observers.Contains(observer)) _observers.Add(observer);
        }
        public void RemoveObserver(ISPotifyObserver observer)
        {
            if (_observers.Contains(observer)) _observers.Remove(observer);
        }
        public async Task NotifyTrackSkippedEventAsync(Track track)
        {
            foreach (var observer in _observers)
            {
                await observer.SendTrackSkippedEventAsync(track);
            }
        }

        public async Task NotifyPlaybackInfoAsync(SpotifyPlaybackInfo playbackInfo)
        {
            foreach (var observer in _observers)
            {
                await observer.SendPlaybackInfoAsync(playbackInfo);
            }
        }
    }
}