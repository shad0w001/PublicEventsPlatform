using Application.Abstractions.Search;
using Application.Search;
using Domain.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.Search;

public static class EmbeddingsStartupValidator
{
    public static async Task ValidateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var options = serviceProvider.GetRequiredService<IOptions<EmbeddingsOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                "Search:Embeddings:ApiKey is missing. " +
                "Create a Gemini API key at https://aistudio.google.com/apikey and set it in appsettings.Development.json.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException("Search:Embeddings:Model is missing.");
        }

        if (options.Dimensions != SearchConstants.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Search:Embeddings:Dimensions must be {SearchConstants.EmbeddingDimensions}.");
        }

        var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator>();
        var embedding = await generator.GenerateAsync("healthcheck", cancellationToken);

        if (embedding.Length != SearchConstants.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding provider returned {embedding.Length} dimensions; expected {SearchConstants.EmbeddingDimensions}.");
        }
    }
}
