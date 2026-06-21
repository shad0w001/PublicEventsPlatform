namespace Domain.Search;

public static class CosineSimilarity
{
    public static double Distance(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Embedding vectors must have the same length.");
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return SearchConstants.NoMatchCosineDistance;
        }

        return 1.0 - (dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}
