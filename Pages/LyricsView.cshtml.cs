using Alify.Models;
using Alify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alify.Pages
{
    public class LyricsViewModel : PageModel
    {
        private readonly ArtistLyricService _artistLyricService;
        private readonly LyricService _lyricService;

        public LyricsViewModel(ArtistLyricService artistLyricService, LyricService lyricService)
        {
            _artistLyricService = artistLyricService;
            _lyricService = lyricService;
        }

        public LyricMapping LyricMapping { get; set; } = new();
        public List<ChatMessage> ChatMessages { get; set; } = new();
        public string Artist { get; set; }
        public string Title { get; set; }
        public string RawLyrics { get; set; }
        public bool LyricsFound { get; set; }

        public async Task OnGetAsync(string artist, string title)
        {
            Artist = artist;
            Title = title;

            if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
            {
                // Fetch lyrics from the service
                RawLyrics = await _lyricService.GetLyricsAsync(artist, title);
                LyricsFound = !string.IsNullOrEmpty(RawLyrics);

                if (LyricsFound)
                {
                    // Parse lyrics with artist mapping
                    LyricMapping = _artistLyricService.ParseLyricsWithArtistMapping(RawLyrics);
                    
                    // Convert to chat messages
                    ChatMessages = ConvertLyricsToChat(LyricMapping);
                }
            }
            else
            {
                // Demo lyrics for testing
                var demoLyrics = @"[Verse 1: Rumi]
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

                Artist = "Demo";
                Title = "Sample Song";
                RawLyrics = demoLyrics;
                LyricsFound = true;
                LyricMapping = _artistLyricService.ParseLyricsWithArtistMapping(demoLyrics);
                ChatMessages = ConvertLyricsToChat(LyricMapping);
            }
        }

        private List<ChatMessage> ConvertLyricsToChat(LyricMapping mapping)
        {
            var messages = new List<ChatMessage>();
            var currentMessage = new ChatMessage();
            
            foreach (var line in mapping.Lines)
            {
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line.Text))
                    continue;

                // If this is an annotation line, start a new message
                if (line.IsAnnotation)
                {
                    // Save previous message if it has content
                    if (!string.IsNullOrEmpty(currentMessage.Content))
                    {
                        messages.Add(currentMessage);
                    }

                    // Start new message
                    currentMessage = new ChatMessage
                    {
                        Artist = line.Artist ?? "Unknown",
                        Section = line.Section ?? "",
                        StartLineNumber = line.LineNumber,
                        Content = "",
                        LineNumbers = new List<int>()
                    };
                }
                else
                {
                    // Add lyric content to current message
                    if (!string.IsNullOrEmpty(currentMessage.Content))
                    {
                        currentMessage.Content += "\n";
                    }
                    currentMessage.Content += line.Text;
                    currentMessage.LineNumbers.Add(line.LineNumber);
                    currentMessage.EndLineNumber = line.LineNumber;
                }
            }

            // Add the last message if it has content
            if (!string.IsNullOrEmpty(currentMessage.Content))
            {
                messages.Add(currentMessage);
            }

            return messages;
        }
    }

    public class ChatMessage
    {
        public string Artist { get; set; } = "";
        public string Section { get; set; } = "";
        public string Content { get; set; } = "";
        public int StartLineNumber { get; set; }
        public int EndLineNumber { get; set; }
        public List<int> LineNumbers { get; set; } = new();
        public string AvatarColor => GetAvatarColor(Artist);
        public string InitialLetters => GetInitials(Artist);

        private string GetAvatarColor(string artist)
        {
            if (string.IsNullOrEmpty(artist)) return "#005c4b";
            
            // WhatsApp-style colors for different artists
            var colors = new[]
            {
                "#005c4b", // WhatsApp green
                "#7c3aed", // Purple
                "#dc2626", // Red
                "#ea580c", // Orange
                "#0891b2", // Cyan
                "#059669", // Emerald
                "#4338ca", // Indigo
                "#be185d", // Pink
                "#0f766e", // Teal
                "#7c2d12"  // Brown
            };
            
            var hash = artist.GetHashCode();
            return colors[Math.Abs(hash) % colors.Length];
        }

        private string GetInitials(string artist)
        {
            if (string.IsNullOrEmpty(artist)) return "U";
            
            var words = artist.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                return words[0].Length >= 2 ? words[0].Substring(0, 2).ToUpper() : words[0].ToUpper();
            }
            
            return string.Join("", words.Take(2).Select(w => w[0])).ToUpper();
        }
    }
}
