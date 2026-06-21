using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Application.Abstractions.Search;
using Application.Search;
using Domain.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Search;

internal sealed class GeminiEmbeddingGenerator(
    HttpClient httpClient,
    IOptions<EmbeddingsOptions> options,
    ILogger<GeminiEmbeddingGenerator> logger) : IEmbeddingGenerator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<float[]> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddingOptions = options.Value;
        var modelPath = NormalizeModelPath(embeddingOptions.Model);
        var requestUri = $"{embeddingOptions.BaseUrl.TrimEnd('/')}/models/{modelPath}:embedContent";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("x-goog-api-key", embeddingOptions.ApiKey);
        request.Content = JsonContent.Create(
            new GeminiEmbedRequest
            {
                Model = ToApiModelName(embeddingOptions.Model),
                Content = new GeminiEmbedContent
                {
                    Parts = [new GeminiEmbedPart { Text = text }]
                },
                OutputDimensionality = embeddingOptions.Dimensions
            },
            options: SerializerOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Gemini embedContent failed with status {StatusCode}: {Body}",
                (int)response.StatusCode,
                body);

            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    new InvalidOperationException(
                        "Gemini API key is invalid or unauthorized. Check Search:Embeddings:ApiKey."),
                HttpStatusCode.TooManyRequests =>
                    new InvalidOperationException("Gemini API rate limit exceeded."),
                _ => new InvalidOperationException(
                    $"Gemini embedContent failed with status {(int)response.StatusCode}.")
            };
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiEmbedResponse>(
            SerializerOptions,
            cancellationToken);

        var values = payload?.Embedding?.Values;
        if (values is null || values.Length == 0)
        {
            throw new InvalidOperationException("Gemini embedContent returned an empty embedding.");
        }

        if (values.Length != embeddingOptions.Dimensions)
        {
            throw new InvalidOperationException(
                $"Gemini returned {values.Length} dimensions; expected {embeddingOptions.Dimensions}.");
        }

        if (values.Length != SearchConstants.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Gemini returned {values.Length} dimensions; expected {SearchConstants.EmbeddingDimensions}.");
        }

        return values;
    }

    internal static string NormalizeModelPath(string model) =>
        model.StartsWith("models/", StringComparison.Ordinal)
            ? model["models/".Length..]
            : model;

    internal static string ToApiModelName(string model) =>
        model.StartsWith("models/", StringComparison.Ordinal)
            ? model
            : $"models/{model}";

    private sealed class GeminiEmbedRequest
    {
        public required string Model { get; init; }

        public required GeminiEmbedContent Content { get; init; }

        public int OutputDimensionality { get; init; }
    }

    private sealed class GeminiEmbedContent
    {
        public required GeminiEmbedPart[] Parts { get; init; }
    }

    private sealed class GeminiEmbedPart
    {
        public required string Text { get; init; }
    }

    private sealed class GeminiEmbedResponse
    {
        public GeminiEmbedResult? Embedding { get; init; }
    }

    private sealed class GeminiEmbedResult
    {
        public float[] Values { get; init; } = [];
    }
}
