using Infrastructure.Search;

namespace InfrastructureTests.Search;

public class GeminiEmbeddingGeneratorTests
{
    [Theory]
    [InlineData("gemini-embedding-001", "gemini-embedding-001")]
    [InlineData("models/gemini-embedding-001", "gemini-embedding-001")]
    public void NormalizeModelPath_Should_ReturnModelNameWithoutPrefix_When_ModelProvided(
        string model,
        string expected)
    {
        // Act
        var result = GeminiEmbeddingGenerator.NormalizeModelPath(model);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("gemini-embedding-001", "models/gemini-embedding-001")]
    [InlineData("models/gemini-embedding-001", "models/gemini-embedding-001")]
    public void ToApiModelName_Should_ReturnPrefixedModelName_When_ModelProvided(
        string model,
        string expected)
    {
        // Act
        var result = GeminiEmbeddingGenerator.ToApiModelName(model);

        // Assert
        Assert.Equal(expected, result);
    }
}
