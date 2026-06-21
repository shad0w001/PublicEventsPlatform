using Application.Abstractions.Search;
using Domain.Search;

namespace ApplicationTests.Search;

internal sealed class FakeEmbeddingGenerator : IEmbeddingGenerator
{
    public string? LastText { get; private set; }

    public int CallCount { get; private set; }

    public Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        LastText = text;
        CallCount++;
        return Task.FromResult(CreateEmbedding());
    }

    internal static float[] CreateEmbedding()
    {
        var embedding = new float[SearchConstants.EmbeddingDimensions];
        Array.Fill(embedding, 0.01f);
        return embedding;
    }
}
