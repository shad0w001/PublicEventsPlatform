using SharedKernel;

namespace Domain.Search.Services;

public static class EventEmbeddingService
{
    public static Result ValidateDimensions(float[] embedding)
    {
        if (embedding.Length != SearchConstants.EmbeddingDimensions)
        {
            return Result.Failure(Error.Validation(
                "Search.InvalidEmbeddingDimensions",
                $"Embedding must have {SearchConstants.EmbeddingDimensions} dimensions."));
        }

        return Result.Success();
    }

    public static Result<EventEmbedding> Create(Guid eventId, float[] embedding, DateTime updatedAt)
    {
        var validation = ValidateDimensions(embedding);
        if (validation.IsFailure)
        {
            return Result.Failure<EventEmbedding>(validation.Error);
        }

        return Result.Success(new EventEmbedding
        {
            EventId = eventId,
            Embedding = embedding,
            UpdatedAt = updatedAt
        });
    }

    public static Result Update(EventEmbedding row, float[] embedding, DateTime updatedAt)
    {
        var validation = ValidateDimensions(embedding);
        if (validation.IsFailure)
        {
            return validation;
        }

        row.Embedding = embedding;
        row.UpdatedAt = updatedAt;

        return Result.Success();
    }
}
