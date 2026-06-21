namespace Application.Search;

public sealed class EmbeddingsOptions
{
    public const string SectionName = "Search:Embeddings";

    public string ApiKey { get; init; } = string.Empty;

    public string Model { get; init; } = "gemini-embedding-001";

    public int Dimensions { get; init; } = 1536;

    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";
}
