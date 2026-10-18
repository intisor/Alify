using Alify.Core.Infrastructure.Security;
using Xunit;

namespace Alify.Core.Tests;

public class PkceUtilTests
{
    [Fact]
    public void GenerateCodeVerifier_DefaultLength_Returns64Characters()
    {
        var verifier = PkceUtil.GenerateCodeVerifier();

        Assert.Equal(64, verifier.Length);
        Assert.Matches("^[a-zA-Z0-9-._~]+$", verifier);
    }

    [Theory]
    [InlineData(43)]
    [InlineData(64)]
    [InlineData(128)]
    public void GenerateCodeVerifier_ValidLengths_Succeeds(int length)
    {
        var verifier = PkceUtil.GenerateCodeVerifier(length);

        Assert.Equal(length, verifier.Length);
        Assert.Matches("^[a-zA-Z0-9-._~]+$", verifier);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(129)]
    [InlineData(0)]
    [InlineData(-5)]
    public void GenerateCodeVerifier_InvalidLengths_ThrowsArgumentOutOfRangeException(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PkceUtil.GenerateCodeVerifier(length));
    }

    [Fact]
    public void GenerateCodeVerifier_SubsequentCalls_AreUnique()
    {
        var v1 = PkceUtil.GenerateCodeVerifier();
        var v2 = PkceUtil.GenerateCodeVerifier();

        Assert.NotEqual(v1, v2);
    }

    /// <summary>
    /// Verifies compliance with the official RFC 7636 Appendix B test vector:
    /// code_verifier: "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
    /// Expected code_challenge: "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"
    /// </summary>
    [Fact]
    public void GenerateCodeChallenge_RFC7636_AppendixB_Vector_MatchesExpected()
    {
        const string rfcVerifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        var actualChallenge = PkceUtil.GenerateCodeChallenge(rfcVerifier);

        Assert.Equal(expectedChallenge, actualChallenge);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateCodeChallenge_NullOrWhitespace_ThrowsArgumentException(string? input)
    {
        Assert.ThrowsAny<ArgumentException>(() => PkceUtil.GenerateCodeChallenge(input!));
    }
}
