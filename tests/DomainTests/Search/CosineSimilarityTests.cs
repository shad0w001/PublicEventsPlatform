using Domain.Search;

namespace DomainTests.Search;

public class CosineSimilarityTests
{
    [Fact]
    public void CosineSimilarity_Should_ReturnZeroDistance_When_VectorsAreIdentical()
    {
        // Arrange
        var vector = new float[] { 1f, 0f, 0f };

        // Act
        var distance = CosineSimilarity.Distance(vector, vector);

        // Assert
        Assert.Equal(0, distance, precision: 5);
    }

    [Fact]
    public void CosineSimilarity_Should_ReturnHigherDistance_When_VectorsAreOrthogonal()
    {
        // Arrange
        var vectorA = new float[] { 1f, 0f, 0f };
        var vectorB = new float[] { 0f, 1f, 0f };

        // Act
        var distance = CosineSimilarity.Distance(vectorA, vectorB);

        // Assert
        Assert.Equal(1, distance, precision: 5);
    }
}
