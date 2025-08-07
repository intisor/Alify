using Alify.Models;

namespace Alify.Services.Events
{
    public interface ISPotifyObserver
    {
        Task SendTrackSkippedEventAsync(Track track);
    }

    public interface ISpotifySubject
    {
        void AddObserver(ISPotifyObserver observer);
        void RemoveObserver(ISPotifyObserver observer);
        Task NotifyTrackSkippedEventAsync(Track track);
    }

}
