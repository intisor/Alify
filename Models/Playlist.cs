using System.Diagnostics;
using SpotifyAPI.Web;

namespace Alify.Models
{
    /// <summary>
    /// Represents a Spotify playlist.
    /// </summary>
    [DebuggerDisplay("Id: {Id}, Name: {Name}, TrackCount: {TrackCount}, ActualTracks: {Tracks.Count}")]
    public class Playlist
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int TrackCount { get; set; }
        public List<Track> Tracks { get; set; } = new();
    }

    /// <summary>
    /// Represents a track with its lyrics and moderation status.
    /// </summary>
    [DebuggerDisplay("Track: {FullTrack?.Artists?.FirstOrDefault()?.Name} - {FullTrack?.Name}, IsFlagged: {IsFlagged}, HasLyrics: {!string.IsNullOrEmpty(Lyrics)}")]
    public class Track
    {
        public FullTrack FullTrack { get; set; }
        public string Lyrics { get; set; }
        public bool IsFlagged { get; set; }
    }

    /// <summary>
    /// Represents the current Spotify playback information.
    /// </summary>
    [DebuggerDisplay("CurrentlyPlaying: {CurrentlyPlaying?.FullTrack?.Name}, QueueCount: {Queue.Count}, RemainingTime: {RemainingTimeMs}ms")]
    public class SpotifyPlaybackInfo
    {
        public Track CurrentlyPlaying { get; set; }
        public List<Track> Queue { get; set; } = new();
        public int? RemainingTimeMs { get; set; }
    }

    /// <summary>
    /// Represents a single line of lyrics with its metadata.
    /// </summary>
    [DebuggerDisplay("Line {LineNumber}: {Text} [{Artist}] [{Section}] IsAnnotation: {IsAnnotation}")]
    public class LyricLine
    {
        public int LineNumber { get; set; }
        public string Text { get; set; }
        public string Artist { get; set; }
        public string Section { get; set; } // e.g., "Verse 1", "Chorus", "Bridge"
        public bool IsAnnotation { get; set; }
    }

    /// <summary>
    /// Manages a collection of lyric lines.
    /// </summary>
    [DebuggerDisplay("TotalLines: {Lines.Count}, Artists: {GetArtists().Count}, Sections: {GetSections().Count}")]
    public class LyricMapping
    {
        private readonly List<LyricLine> _lines = [];

        // Read-only property to access the collections
        public IReadOnlyList<LyricLine> Lines => _lines.AsReadOnly();

        /// <summary>
        /// Adds a lyric line.
        /// </summary>
        /// <param name="line">The lyric line to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when line is null.</exception>
        /// <exception cref="ArgumentException">Thrown when line number already exists.</exception>
        public void AddLine(LyricLine line)
        {
            ArgumentNullException.ThrowIfNull(line);

            if (_lines.Exists(l => l.LineNumber == line.LineNumber))
                throw new ArgumentException($"Line number {line.LineNumber} already exists.", nameof(line));

            _lines.Add(line);
        }

        /// <summary>
        /// Removes a line by line number.
        /// </summary>
        /// <param name="lineNumber">The line number to remove.</param>
        /// <returns>True if the line was found and removed, false otherwise.</returns>
        public bool RemoveLine(int lineNumber)
        {
            var line = _lines.FirstOrDefault(l => l.LineNumber == lineNumber);
            if (line == null)
                return false;

            _lines.Remove(line);
            return true;
        }

        /// <summary>
        /// Clears all lines.
        /// </summary>
        public void Clear()
        {
            _lines.Clear();
        }

        /// <summary>
        /// Updates an existing line.
        /// </summary>
        /// <param name="updatedLine">The updated line.</param>
        /// <returns>True if the line was found and updated, false otherwise.</returns>
        public bool UpdateLine(LyricLine updatedLine)
        {
            ArgumentNullException.ThrowIfNull(updatedLine);

            var existingLine = _lines.FirstOrDefault(l => l.LineNumber == updatedLine.LineNumber);
            if (existingLine == null)
                return false;

            // Update the line properties
            existingLine.Text = updatedLine.Text;
            existingLine.Artist = updatedLine.Artist;
            existingLine.Section = updatedLine.Section;
            existingLine.IsAnnotation = updatedLine.IsAnnotation;

            return true;
        }

        /// <summary>
        /// Gets line numbers for a specific artist.
        /// </summary>
        /// <param name="artist">The artist name.</param>
        /// <returns>A new list of line numbers for the artist, or an empty list if not found.</returns>
        public List<int> GetLineNumbersForArtist(string artist)
        {
            return _lines.Where(l => l.Artist == artist).Select(l => l.LineNumber).ToList();
        }

        /// <summary>
        /// Gets line numbers for a specific section.
        /// </summary>
        /// <param name="section">The section name.</param>
        /// <returns>A new list of line numbers for the section, or an empty list if not found.</returns>
        public List<int> GetLineNumbersForSection(string section)
        {
            return _lines.Where(l => l.Section == section).Select(l => l.LineNumber).ToList();
        }

        /// <summary>
        /// Gets all artists mentioned in the lyrics.
        /// </summary>
        /// <returns>List of unique artist names.</returns>
        public List<string> GetArtists() => _lines.Select(l => l.Artist).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();

        /// <summary>
        // Gets all sections in the lyrics.
        /// </summary>
        /// <returns>List of unique section names.</returns>
        public List<string> GetSections() => _lines.Select(l => l.Section).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
    }
}
