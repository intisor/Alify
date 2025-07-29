using Alify.Models;
using SpotifyAPI.Web;
using System.Text.RegularExpressions;

namespace Alify.Services
{
    /// <summary>
    /// Provides services for parsing and analyzing song lyrics that contain artist and section annotations.
    /// This service is responsible for transforming raw lyric strings into structured data.
    /// </summary>
    public partial class ArtistLyricService
    {
        // BOOKMARK: Regex for Lyric Parsing
        // The following regular expressions are used to identify structural elements within the lyrics.
        // Using source generators ([GeneratedRegex]) improves performance by pre-compiling the regex.

        /// <summary>
        /// Matches annotations like "[Verse 1: ArtistName]" or "[Chorus]".
        /// It captures the section name and, optionally, the artist name.
        /// </summary>
        [GeneratedRegex(@"^\[(?<section>.*?)(?::\s*(?<artist>.*?))?\]$", RegexOptions.IgnoreCase)]
        private static partial Regex AnnotationPattern();

        /// <summary>
        /// Matches simple section annotations without artists, like "[Verse]", "[Chorus]", etc.
        /// This is a fallback or simpler case of the main annotation pattern.
        /// </summary>
        [GeneratedRegex(@"^\[(?<section>Verse|Chorus|Bridge|Pre-Chorus|Post-Chorus|Outro|Intro|Hook|Refrain)\s*\d*\]$", RegexOptions.IgnoreCase)]
        private static partial Regex SectionOnlyPattern();

        /// <summary>
        /// Parses a raw string of lyrics and maps each line to an artist and song section.
        /// This is the core method for understanding the structure of a song's lyrics.
        /// </summary>
        /// <param name="lyrics">The full lyrics of a song as a single string.</param>
        /// <returns>A <see cref="LyricMapping"/> object containing the structured lyric data.</returns>
        public LyricMapping ParseLyricsWithArtistMapping(string lyrics, string mainArtist = null)
        {
            if (string.IsNullOrWhiteSpace(lyrics))
            {
                return new LyricMapping();
            }

            var mapping = new LyricMapping();
            var lines = lyrics.Split(['\r', '\n'], StringSplitOptions.None);
            
            // BOOKMARK: State Management during Parsing
            // These variables keep track of the current artist and section as we iterate through the lines.
            // This is a state machine approach to parsing.
            string currentArtist = mainArtist;
            string currentSection = null;
            int lineNumber = 1;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                
                // Skip empty lines but still count them for accurate line numbering.
                if (string.IsNullOrWhiteSpace(line))
                {
                    lineNumber++;
                    continue;
                }

                var lyricLine = new LyricLine
                {
                    LineNumber = lineNumber,
                    Text = line
                };

                // BOOKMARK: Annotation Detection
                // Here, we check if a line is a structural annotation (e.g., "[Chorus: Artist]")
                // or an actual lyric line.
                var match = AnnotationPattern().Match(line);
                if (match.Success)
                {
                    lyricLine.IsAnnotation = true;
               

                    var section = match.Groups["section"].Value.Trim();
                    var artist = match.Groups["artist"].Value.Trim();

                    // Update the current context (state) for subsequent lyric lines.
                    currentSection = section;
                    if (!string.IsNullOrEmpty(artist))
                    {
                        currentArtist = artist;
                    }

                    lyricLine.Section = currentSection;
                    lyricLine.Artist = currentArtist;
                }
                else
                {
                    // This is a standard lyric line.
                    // Assign the current artist and section based on the last annotation found.
                    lyricLine.IsAnnotation = false;
                    lyricLine.Section = currentSection;
                    lyricLine.Artist = currentArtist;
                }

                // Add the processed line to our structured mapping.
                mapping.AddLine(lyricLine);
                lineNumber++;
            }

            return mapping;
        }

        /// <summary>
        /// Formats lyrics with line numbers for display.
        /// </summary>
        /// <param name="lyrics">The raw lyrics string.</param>
        /// <returns>A string with each line prefixed by its number.</returns>
        public string GetNumberedLyrics(string lyrics)
        {
            if (string.IsNullOrWhiteSpace(lyrics))
            {
                return string.Empty;
            }

            var lines = lyrics.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var numberedLines = new List<string>();
            
            for (int i = 0; i < lines.Length; i++)
            {
                var lineNumber = i + 1;
                var line = lines[i];
                numberedLines.Add($"{lineNumber:D3}: {line}");
            }

            return string.Join(Environment.NewLine, numberedLines);
        }

        // BOOKMARK: Utility Methods
        // The following methods provide convenient ways to query the parsed lyric data.
        // They all rely on the core ParseLyricsWithArtistMapping method.

        /// <summary>
        /// Extracts a unique list of artists from the lyrics.
        /// </summary>
        public List<string> GetArtistsInLyrics(string lyrics)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.GetArtists();
        }

        /// <summary>
        /// Gets all line numbers attributed to a specific artist.
        /// </summary>
        public List<int> GetLineNumbersForArtist(string lyrics, string artist)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.GetLineNumbersForArtist(artist);
        }

        /// <summary>
        /// Gets all line numbers belonging to a specific section (e.g., "Chorus").
        /// </summary>
        public List<int> GetLineNumbersForSection(string lyrics, string section)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.GetLineNumbersForSection(section);
        }

        /// <summary>
        /// Creates a dictionary mapping each artist to their line numbers.
        /// </summary>
        public Dictionary<string, List<int>> GetArtistLineMapping(string lyrics)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.GetArtists().ToDictionary(artist => artist, artist => mapping.GetLineNumbersForArtist(artist));
        }

        /// <summary>
        /// Creates a dictionary mapping each section to its line numbers.
        /// </summary>
        public Dictionary<string, List<int>> GetSectionLineMapping(string lyrics)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.GetSections().ToDictionary(section => section, section => mapping.GetLineNumbersForSection(section));
        }

        /// <summary>
        /// Retrieves all lyric lines for a specific artist.
        /// </summary>
        public List<LyricLine> GetLyricLinesForArtist(string lyrics, string artist)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.Lines.Where(l => l.Artist == artist).ToList();
        }

        /// <summary>
        /// Retrieves all lyric lines for a specific section.
        /// </summary>
        public List<LyricLine> GetLyricLinesForSection(string lyrics, string section)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.Lines.Where(l => l.Section == section).ToList();
        }

        /// <summary>
        /// Formats the lyrics to show the line number and associated artist/section metadata.
        /// Useful for debugging or a detailed view.
        /// </summary>
        public string GetFormattedLyricsWithMapping(string lyrics)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            var result = new List<string>();

            foreach (var line in mapping.Lines)
            {
                var prefix = $"{line.LineNumber:D3}:";
                var artistInfo = !string.IsNullOrEmpty(line.Artist) ? $" [{line.Artist}]" : "";
                var sectionInfo = !string.IsNullOrEmpty(line.Section) && string.IsNullOrEmpty(line.Artist) ? $" [{line.Section}]" : "";
                
                result.Add($"{prefix} {line.Text}{artistInfo}{sectionInfo}");
            }

            return string.Join(Environment.NewLine, result);
        }
    }
}
