using Alify.Core.Models;

namespace Alify.Features.Spotify.Events
{
    public interface ISpotifySubject
    {
        void AddObserver(ISPotifyObserver observer);
        void RemoveObserver(ISPotifyObserver observer);
        Task NotifyTrackSkippedEventAsync(Track track);
        Task NotifyPlaybackInfoAsync(SpotifyPlaybackInfo playbackInfo);
    }
}