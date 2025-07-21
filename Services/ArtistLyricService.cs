using Alify.Models;
using System.Text.RegularExpressions;

namespace Alify.Services
{
    public class ArtistLyricService
    {
        // Regex patterns for different types of annotations
        private static readonly Regex AnnotationPattern = new Regex(
            @"^\[(?<section>.*?)(?::\s*(?<artist>.*?))?\]$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        private static readonly Regex SectionOnlyPattern = new Regex(
            @"^\[(?<section>Verse|Chorus|Bridge|Pre-Chorus|Post-Chorus|Outro|Intro|Hook|Refrain)\s*\d*\]$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        public LyricMapping ParseLyricsWithArtistMapping(string lyrics)
        {
            if (string.IsNullOrWhiteSpace(lyrics))
            {
                return new LyricMapping();
            }

            var mapping = new LyricMapping();
            var lines = lyrics.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            
            string currentArtist = null;
            string currentSection = null;
            int lineNumber = 1;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                
                // Skip empty lines but still count them
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

                // Check if this is an annotation line
                var match = AnnotationPattern.Match(line);
                if (match.Success)
                {
                    lyricLine.IsAnnotation = true;
                    
                    var section = match.Groups["section"].Value.Trim();
                    var artist = match.Groups["artist"].Value.Trim();

                    // Update current context
                    currentSection = section;
                    if (!string.IsNullOrEmpty(artist))
                    {
                        currentArtist = artist;
                    }

                    lyricLine.Section = currentSection;
                    lyricLine.Artist = currentArtist;

                    // Add to section mapping
                    if (!string.IsNullOrEmpty(currentSection))
                    {
                        if (!mapping.SectionToLineNumbers.ContainsKey(currentSection))
                        {
                            mapping.SectionToLineNumbers[currentSection] = new List<int>();
                        }
                        mapping.SectionToLineNumbers[currentSection].Add(lineNumber);
                    }

                    // Add to artist mapping if artist is specified
                    if (!string.IsNullOrEmpty(currentArtist))
                    {
                        if (!mapping.ArtistToLineNumbers.ContainsKey(currentArtist))
                        {
                            mapping.ArtistToLineNumbers[currentArtist] = new List<int>();
                        }
                        mapping.ArtistToLineNumbers[currentArtist].Add(lineNumber);
                    }
                }
                else
                {
                    // This is a lyric line
                    lyricLine.IsAnnotation = false;
                    lyricLine.Section = currentSection;
                    lyricLine.Artist = currentArtist;

                    // Add to artist mapping for lyric lines
                    if (!string.IsNullOrEmpty(currentArtist))
                    {
                        if (!mapping.ArtistToLineNumbers.ContainsKey(currentArtist))
                        {
                            mapping.ArtistToLineNumbers[currentArtist] = new List<int>();
                        }
                        mapping.ArtistToLineNumbers[currentArtist].Add(lineNumber);
                    }

                    // Add to section mapping for lyric lines
                    if (!string.IsNullOrEmpty(currentSection))
                    {
                        if (!mapping.SectionToLineNumbers.ContainsKey(currentSection))
                        {
                            mapping.SectionToLineNumbers[currentSection] = new List<int>();
                        }
                        mapping.SectionToLineNumbers[currentSection].Add(lineNumber);
                    }
                }

                mapping.Lines.Add(lyricLine);
                lineNumber++;
            }

            return mapping;
        }

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

        public List<string> GetArtistsInLyrics(string lyrics)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.ArtistToLineNumbers.Keys.ToList();
        }

        public List<int> GetLineNumbersForArtist(string lyrics, string artist)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.ArtistToLineNumbers.ContainsKey(artist) 
                ? mapping.ArtistToLineNumbers[artist] 
                : new List<int>();
        }

        public List<int> GetLineNumbersForSection(string lyrics, string section)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.SectionToLineNumbers.ContainsKey(section) 
                ? mapping.SectionToLineNumbers[section] 
                : new List<int>();
        }

        public Dictionary<string, List<int>> GetArtistLineMapping(string lyrics)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.ArtistToLineNumbers;
        }

        public Dictionary<string, List<int>> GetSectionLineMapping(string lyrics)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.SectionToLineNumbers;
        }

        public List<LyricLine> GetLyricLinesForArtist(string lyrics, string artist)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.Lines.Where(l => l.Artist == artist).ToList();
        }

        public List<LyricLine> GetLyricLinesForSection(string lyrics, string section)
        {
            var mapping = ParseLyricsWithArtistMapping(lyrics);
            return mapping.Lines.Where(l => l.Section == section).ToList();
        }

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
