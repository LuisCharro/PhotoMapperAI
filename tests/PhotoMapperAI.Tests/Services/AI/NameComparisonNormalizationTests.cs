using PhotoMapperAI.Services.AI;
using System.Reflection;
using Xunit;

namespace PhotoMapperAI.Tests.Services.AI;

public class NameComparisonNormalizationTests
{
    [Fact]
    public void ToCoreTokens_NormalizesSpellingVariants()
    {
        var method = typeof(NameComparisonPromptBuilder).GetMethod("ToCoreTokens",
            BindingFlags.NonPublic | BindingFlags.Static);

        var tokens1 = (System.Collections.Generic.List<string>?)method?.Invoke(null, new object[] { "Fernández" });
        var tokens2 = (System.Collections.Generic.List<string>?)method?.Invoke(null, new object[] { "Fernandes" });

        Assert.NotNull(tokens1);
        Assert.NotNull(tokens2);
        Assert.Single(tokens1);
        Assert.Single(tokens2);
        Assert.Equal(tokens1[0], tokens2[0]); // Should both be "fernandez"
    }

    [Fact]
    public void ToCoreTokens_NormalizesGonzalezVariants()
    {
        var method = typeof(NameComparisonPromptBuilder).GetMethod("ToCoreTokens",
            BindingFlags.NonPublic | BindingFlags.Static);

        var tokens1 = (System.Collections.Generic.List<string>?)method?.Invoke(null, new object[] { "Gonzalez" });
        var tokens2 = (System.Collections.Generic.List<string>?)method?.Invoke(null, new object[] { "Gonsales" });

        Assert.NotNull(tokens1);
        Assert.NotNull(tokens2);
        Assert.Single(tokens1);
        Assert.Single(tokens2);
        Assert.Equal(tokens1[0], tokens2[0]); // Should both be "gonzalez"
    }

    [Fact]
    public void ToCoreTokens_NormalizesChicoToFrancisco()
    {
        var method = typeof(NameComparisonPromptBuilder).GetMethod("ToCoreTokens",
            BindingFlags.NonPublic | BindingFlags.Static);

        var nicknameTokens = (System.Collections.Generic.List<string>?)method?.Invoke(null, new object[] { "Chico" });
        var fullNameTokens = (System.Collections.Generic.List<string>?)method?.Invoke(null, new object[] { "Francisco" });

        Assert.NotNull(nicknameTokens);
        Assert.NotNull(fullNameTokens);
        Assert.Single(nicknameTokens);
        Assert.Single(fullNameTokens);
        Assert.Equal(fullNameTokens[0], nicknameTokens[0]);
    }

    [Fact]
    public void ToCoreTokens_FollowsAliasChainForZanderAndAlexander()
    {
        var method = typeof(NameComparisonPromptBuilder).GetMethod("ToCoreTokens",
            BindingFlags.NonPublic | BindingFlags.Static);

        var nicknameTokens = (System.Collections.Generic.List<string>?)method?.Invoke(null, new object[] { "Zander" });
        var fullNameTokens = (System.Collections.Generic.List<string>?)method?.Invoke(null, new object[] { "Alexander" });

        Assert.NotNull(nicknameTokens);
        Assert.NotNull(fullNameTokens);
        Assert.Single(nicknameTokens);
        Assert.Single(fullNameTokens);
        Assert.Equal(fullNameTokens[0], nicknameTokens[0]);
    }
}
