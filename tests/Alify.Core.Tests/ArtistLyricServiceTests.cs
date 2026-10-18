using Alify.Core.Services;
using Xunit;

namespace Alify.Core.Tests;

public class ArtistLyricServiceTests
{
    private readonly ArtistLyricService _service = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t \r\n ")]
    public void ParseLyricsWithArtistMapping_NullOrWhitespace_ReturnsEmptyMapping(string? input)
    {
        var result = _service.ParseLyricsWithArtistMapping(input!);

        Assert.NotNull(result);
        Assert.Empty(result.Lines);
        Assert.Empty(result.GetArtists());
        Assert.Empty(result.GetSections());
    }

    [Fact]
    public void ParseLyricsWithArtistMapping_PlainLyrics_AttributesToMainArtist()
    {
        var lyrics = "Line one\nLine two\nLine three";
        var result = _service.ParseLyricsWithArtistMapping(lyrics, "Kendrick Lamar");

        Assert.Equal(3, result.Lines.Count);
        Assert.All(result.Lines, line =>
        {
            Assert.Equal("Kendrick Lamar", line.Artist);
            Assert.False(line.IsAnnotation);
            Assert.Null(line.Section);
        });
        Assert.Equal(["Kendrick Lamar"], result.GetArtists());
    }

    [Fact]
    public void ParseLyricsWithArtistMapping_WithAnnotations_CorrectlySegmentsSectionsAndArtists()
    {
        var lyrics = @"[Intro]
Welcome to the track
[Verse 1: Drake]
Started from the bottom
Now we're here
[Chorus: Future & Drake]
Never stop the grind
[Outro]
Fading out";

        var result = _service.ParseLyricsWithArtistMapping(lyrics, "Drake");

        // Verify sections identified
        var sections = result.GetSections();
        Assert.Contains("Intro", sections);
        Assert.Contains("Verse 1", sections);
        Assert.Contains("Chorus", sections);
        Assert.Contains("Outro", sections);

        // Verify line 1 is Intro annotation
        var introAnnotation = result.Lines.First(l => l.Text == "[Intro]");
        Assert.True(introAnnotation.IsAnnotation);
        Assert.Equal("Intro", introAnnotation.Section);

        // Verify Drake lines in Verse 1
        var verse1Lines = _service.GetLyricLinesForSection(lyrics, "Verse 1");
        Assert.Contains(verse1Lines, l => l.Text == "Started from the bottom" && l.Artist == "Drake");

        // Verify line numbering increments accurately
        for (int i = 0; i < result.Lines.Count; i++)
        {
            Assert.True(result.Lines[i].LineNumber > 0);
        }
    }

    [Fact]
    public void ParseLyricsWithArtistMapping_PreservesLineNumbersAcrossEmptyLines()
    {
        var lyrics = "Line 1\n\nLine 3\n\n\nLine 6";
        var result = _service.ParseLyricsWithArtistMapping(lyrics);

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(1, result.Lines[0].LineNumber);
        Assert.Equal("Line 1", result.Lines[0].Text);

        Assert.Equal(3, result.Lines[1].LineNumber);
        Assert.Equal("Line 3", result.Lines[1].Text);

        Assert.Equal(6, result.Lines[2].LineNumber);
        Assert.Equal("Line 6", result.Lines[2].Text);
    }

    [Fact]
    public void GetNumberedLyrics_PadsNumbersCorrectly()
    {
        var lyrics = "First line\nSecond line";
        var result = _service.GetNumberedLyrics(lyrics);

        Assert.Contains("001: First line", result);
        Assert.Contains("002: Second line", result);
    }
}
