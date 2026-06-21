namespace Domain.Search;

public static class SearchConstants
{
    public const int EmbeddingDimensions = 1536;
    public const int MaxSemanticRerankCandidates = 500;
    public const double ContainsFallbackCosineDistance = 0.45;
    public const double NoMatchCosineDistance = 2.0;
}
