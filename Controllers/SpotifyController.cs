using Alify.Services;
using Microsoft.AspNetCore.Mvc;

namespace Alify.Controllers
{
    [Route("api/spotify")]
    [ApiController]
    public class SpotifyController : ControllerBase
    {
        private readonly SpotifyQueueMonitorService _monitorService;
        private readonly SpotifyService _spotifyService;
        private readonly ArtistLyricService _artistLyricService;

        public SpotifyController(SpotifyQueueMonitorService monitorService, SpotifyService spotifyService, ArtistLyricService artistLyricService)
        {
            _monitorService = monitorService;
            _spotifyService = spotifyService;
            _artistLyricService = artistLyricService;
        }

        [HttpPost("start-monitor")]
        public IActionResult StartMonitor()
        {
            var spotify = _spotifyService.GetSpotifyClient();
            if (spotify == null) 
                return BadRequest("Authentication required. Please login to Spotify first.");

            _monitorService.StartMonitoring();
            return Ok("Queue monitoring started successfully.");
        }

        [HttpPost("stop-monitor")]
        public IActionResult StopMonitor()
        {
            _monitorService.StopMonitoring();
            return Ok("Queue monitoring stopped successfully.");
        }

        [HttpGet("monitor-status")]
        public IActionResult GetMonitorStatus()
        {
            return Ok(new { 
                IsMonitoring = _monitorService.IsMonitoring,
                Status = _monitorService.IsMonitoring ? "Running" : "Stopped"
            });
        }

        [HttpPost("test-lyrics-parsing")]
        public IActionResult TestLyricsParsing()
        {
            var sampleLyrics = @"[Verse 1: Rumi]
I tried to hide but something broke
I tried to sing, couldn't hit the notes
The words kept catching in my throat
I tried to smile, I was suffocating though
But here with you, I can finally breathe
You say you're no good, but you're good for me
I've been hoping to change, now I know we can change
But I won't if you're not by my side

[Chorus: Rumi]
Why does it feel right every time I let you in?
Why does it feel like I can tell you anything?
All the secrets that keep me in chains, and
All the damage that might make me dangerous
You got a dark side, guess you're not the only one
What if we both tried fighting what we're running from?
We can't fix it if we never face it
What if we find a way to escape it?

[Post-Chorus: Rumi]
We could be free, free
We can't fix it if we never face it
Let the past be the past 'til it's weightless

[Verse 2: Jinu]
Ooh, time goes by, and I lose perspective
Yeah, hope only hurts, so I just forget it
But you're breaking through all the dark in me when I thought that nobody could
And you're waking up all these parts of me that I thought were buried for good";

            var mapping = _artistLyricService.ParseLyricsWithArtistMapping(sampleLyrics);
            var numberedLyrics = _artistLyricService.GetNumberedLyrics(sampleLyrics);
            var artists = _artistLyricService.GetArtistsInLyrics(sampleLyrics);
            var artistLineMapping = _artistLyricService.GetArtistLineMapping(sampleLyrics);
            var sectionLineMapping = _artistLyricService.GetSectionLineMapping(sampleLyrics);

            return Ok(new {
                NumberedLyrics = numberedLyrics,
                Artists = artists,
                ArtistLineMapping = artistLineMapping,
                SectionLineMapping = sectionLineMapping,
                TotalLines = mapping.Lines.Count,
                AnnotationLines = mapping.Lines.Count(l => l.IsAnnotation),
                LyricLines = mapping.Lines.Count(l => !l.IsAnnotation)
            });
        }

        [HttpGet("lyrics-for-artist/{artist}")]
        public IActionResult GetLyricsForArtist(string artist)
        {
            var sampleLyrics = @"[Verse 1: Rumi]
I tried to hide but something broke
I tried to sing, couldn't hit the notes
The words kept catching in my throat
I tried to smile, I was suffocating though
But here with you, I can finally breathe
You say you're no good, but you're good for me
I've been hoping to change, now I know we can change
But I won't if you're not by my side

[Chorus: Rumi]
Why does it feel right every time I let you in?
Why does it feel like I can tell you anything?
All the secrets that keep me in chains, and
All the damage that might make me dangerous
You got a dark side, guess you're not the only one
What if we both tried fighting what we're running from?
We can't fix it if we never face it
What if we find a way to escape it?

[Post-Chorus: Rumi]
We could be free, free
We can't fix it if we never face it
Let the past be the past 'til it's weightless

[Verse 2: Jinu]
Ooh, time goes by, and I lose perspective
Yeah, hope only hurts, so I just forget it
But you're breaking through all the dark in me when I thought that nobody could
And you're waking up all these parts of me that I thought were buried for good";

            var lineNumbers = _artistLyricService.GetLineNumbersForArtist(sampleLyrics, artist);
            var lyricLines = _artistLyricService.GetLyricLinesForArtist(sampleLyrics, artist);

            return Ok(new {
                Artist = artist,
                LineNumbers = lineNumbers,
                LyricLines = lyricLines.Select(l => new { l.LineNumber, l.Text, l.Section, l.IsAnnotation })
            });
        }
    }
}