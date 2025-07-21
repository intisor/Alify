using Alify.Services;
using System;

namespace Alify.Demo
{
    public static class ArtistLyricServiceDemo
    {
        public static void RunDemo()
        {
            var artistLyricService = new ArtistLyricService();
            
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

            Console.WriteLine("=== ARTIST LYRIC SERVICE DEMO ===\n");

            // 1. Show numbered lyrics
            Console.WriteLine("1. NUMBERED LYRICS:");
            Console.WriteLine(artistLyricService.GetNumberedLyrics(sampleLyrics));
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // 2. Show artists found
            Console.WriteLine("2. ARTISTS FOUND:");
            var artists = artistLyricService.GetArtistsInLyrics(sampleLyrics);
            foreach (var artist in artists)
            {
                Console.WriteLine($"   - {artist}");
            }
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // 3. Show artist to line number mapping
            Console.WriteLine("3. ARTIST TO LINE NUMBERS MAPPING:");
            var artistMapping = artistLyricService.GetArtistLineMapping(sampleLyrics);
            foreach (var kvp in artistMapping)
            {
                Console.WriteLine($"   {kvp.Key}: Lines {string.Join(", ", kvp.Value)}");
            }
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // 4. Show section to line number mapping
            Console.WriteLine("4. SECTION TO LINE NUMBERS MAPPING:");
            var sectionMapping = artistLyricService.GetSectionLineMapping(sampleLyrics);
            foreach (var kvp in sectionMapping)
            {
                Console.WriteLine($"   {kvp.Key}: Lines {string.Join(", ", kvp.Value)}");
            }
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // 5. Show specific artist lines
            Console.WriteLine("5. LINES FOR ARTIST 'Rumi':");
            var rumiLines = artistLyricService.GetLyricLinesForArtist(sampleLyrics, "Rumi");
            foreach (var line in rumiLines)
            {
                var type = line.IsAnnotation ? "[ANNOTATION]" : "[LYRIC]";
                Console.WriteLine($"   Line {line.LineNumber:D3} {type}: {line.Text}");
            }
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // 6. Show specific artist lines
            Console.WriteLine("6. LINES FOR ARTIST 'Jinu':");
            var jinuLines = artistLyricService.GetLyricLinesForArtist(sampleLyrics, "Jinu");
            foreach (var line in jinuLines)
            {
                var type = line.IsAnnotation ? "[ANNOTATION]" : "[LYRIC]";
                Console.WriteLine($"   Line {line.LineNumber:D3} {type}: {line.Text}");
            }
            Console.WriteLine("\n" + new string('=', 50) + "\n");

            // 7. Show formatted lyrics with mapping
            Console.WriteLine("7. FORMATTED LYRICS WITH ARTIST MAPPING:");
            Console.WriteLine(artistLyricService.GetFormattedLyricsWithMapping(sampleLyrics));
        }
    }
}
