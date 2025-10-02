namespace Alify.Features.Spotify.Services
{
    public interface ISseService
    {
        string AddConnection(HttpResponse response);
        void RemoveConnection(string connectionId);
        Task SendEventAsync(string eventName, object data);
        void Cleanup();
    }
}