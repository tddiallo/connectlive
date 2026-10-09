using ConnectLive.Core.Api.Sessions;

namespace ConnectLive.Core.Api.Tests.Sessions;

public class SessionCodeGeneratorTests
{
    [Fact]
    public void Generate_ReturnsSixCharactersFromTheUnambiguousAlphabet()
    {
        for (var i = 0; i < 200; i++)
        {
            var code = SessionCodeGenerator.Generate();

            Assert.Equal(SessionCodeGenerator.CodeLength, code.Length);
            Assert.All(code, c => Assert.Contains(c, SessionCodeGenerator.Alphabet));
        }
    }

    [Theory]
    [InlineData('I')]
    [InlineData('O')]
    [InlineData('0')]
    [InlineData('1')]
    public void Alphabet_ExcludesAmbiguousCharacters(char ambiguous)
    {
        Assert.DoesNotContain(ambiguous, SessionCodeGenerator.Alphabet);
    }

    [Fact]
    public void Generate_ProducesDifferentCodes()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => SessionCodeGenerator.Generate()).ToHashSet();

        Assert.True(codes.Count > 45);
    }
}
