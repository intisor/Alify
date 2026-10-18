using Alify.Core.Features.Analysis.Ledger;
using Xunit;

namespace Alify.Core.Tests;

public class CanonicalLyricNormalizerTests
{
    private readonly CanonicalLyricNormalizer _normalizer = new();

    [Fact]
    public void Normalize_HandlesCRLFandLF_ProducingIdenticalCanonicalHash()
    {
        var lfText = "[Verse 1]\nFirst line of lyrics\n\nSecond line of lyrics";
        var crlfText = "[Verse 1]\r\nFirst line of lyrics\r\n\r\nSecond line of lyrics";

        var v1 = _normalizer.Normalize(lfText);
        var v2 = _normalizer.Normalize(crlfText);

        // Raw hashes differ because raw bytes have \r\n vs \n
        Assert.NotEqual(v1.RawContentHash, v2.RawContentHash);

        // Canonical hashes MUST match because canonical LF form is identical
        Assert.Equal(v1.CanonicalContentHash, v2.CanonicalContentHash);
        Assert.Equal(v1.Lines.Count, v2.Lines.Count);
        Assert.Equal(4, v1.Lines.Count);
    }

    [Fact]
    public void Normalize_AssignsStable1BasedLineNumbersAndKinds()
    {
        var lyrics = @"[Intro: Drake]
Yeah, look
[ar: Drake]

[Chorus]
Started from the bottom";

        var version = _normalizer.Normalize(lyrics);

        Assert.Equal(6, version.Lines.Count);

        // Line 1: Section with artist annotation
        Assert.Equal(1, version.Lines[0].LineNumber);
        Assert.Equal(SourceLineKind.Section, version.Lines[0].Kind);
        Assert.Equal("Intro", version.Lines[0].SectionName);
        Assert.Equal("Drake", version.Lines[0].ArtistAnnotation);

        // Line 2: Lyric
        Assert.Equal(2, version.Lines[1].LineNumber);
        Assert.Equal(SourceLineKind.Lyric, version.Lines[1].Kind);
        Assert.Equal("Yeah, look", version.Lines[1].CanonicalText);

        // Line 3: Metadata
        Assert.Equal(3, version.Lines[2].LineNumber);
        Assert.Equal(SourceLineKind.Metadata, version.Lines[2].Kind);

        // Line 4: Blank
        Assert.Equal(4, version.Lines[3].LineNumber);
        Assert.Equal(SourceLineKind.Blank, version.Lines[3].Kind);

        // Line 5: Section
        Assert.Equal(5, version.Lines[4].LineNumber);
        Assert.Equal(SourceLineKind.Section, version.Lines[4].Kind);

        // Line 6: Lyric
        Assert.Equal(6, version.Lines[5].LineNumber);
        Assert.Equal(SourceLineKind.Lyric, version.Lines[5].Kind);
    }

    [Fact]
    public void GetCanonicalEvidenceExcerpt_ReturnsExactJoinedSlice()
    {
        var text = "Line 1\nLine 2\nLine 3\nLine 4";
        var version = _normalizer.Normalize(text);

        var excerpt = version.GetCanonicalEvidenceExcerpt(2, 3);
        Assert.Equal("Line 2\nLine 3", excerpt);
    }
}
