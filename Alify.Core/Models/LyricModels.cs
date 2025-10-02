using System.Diagnostics;

namespace Alify.Core.Models
{
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

        public void AddLine(LyricLine line)
        {
            ArgumentNullException.ThrowIfNull(line);

            if (_lines.Exists(l => l.LineNumber == line.LineNumber))
                throw new ArgumentException($"Line number {line.LineNumber} already exists.", nameof(line));

            _lines.Add(line);
        }

        public bool RemoveLine(int lineNumber)
        {
            var line = _lines.FirstOrDefault(l => l.LineNumber == lineNumber);
            if (line == null)
                return false;

            _lines.Remove(line);
            return true;
        }

        public void Clear()
        {
            _lines.Clear();
        }

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

        public List<int> GetLineNumbersForArtist(string artist)
        {
            return [.. _lines.Where(l => l.Artist == artist).Select(l => l.LineNumber)];
        }

        public List<int> GetLineNumbersForSection(string section)
        {
            return [.. _lines.Where(l => l.Section == section).Select(l => l.LineNumber)];
        }

        public List<string> GetArtists() => [.. _lines.Select(l => l.Artist).Where(a => !string.IsNullOrEmpty(a)).Distinct()];

        public List<string> GetSections() => [.. _lines.Select(l => l.Section).Where(s => !string.IsNullOrEmpty(s)).Distinct()];
    }
}
