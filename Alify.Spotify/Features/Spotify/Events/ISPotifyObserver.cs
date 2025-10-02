using Alify.Core.Models;

namespace Alify.Features.Spotify.Events
{
    public interface ISPotifyObserver
    {
        Task SendTrackSkippedEventAsync(Track track);
        Task SendPlaybackInfoAsync(SpotifyPlaybackInfo playbackInfo);
    }
}
