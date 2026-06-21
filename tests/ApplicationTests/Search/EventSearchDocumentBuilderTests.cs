using Application.Search;
using Domain.Events.EventLocations;

namespace ApplicationTests.Search;

public class EventSearchDocumentBuilderTests
{
    [Fact]
    public void Build_Should_IncludeTitleDescriptionCategoryHostAndLocations_When_FullInputProvided()
    {
        // Arrange
        var input = new EventSearchDocumentBuilder.Input(
            Title: "Jazz Night",
            Description: "Live music downtown",
            CategoryName: "Music",
            Locations:
            [
                new EventLocation
                {
                    Name = "Main Hall",
                    Kind = EventLocationKind.Physical,
                    Address = "123 Main St",
                    City = "Sofia",
                    Country = "Bulgaria"
                },
                new EventLocation
                {
                    Name = "Stream",
                    Kind = EventLocationKind.Virtual,
                    Url = "https://stream.example.com/live"
                }
            ],
            HostDisplayName: "City Jazz Org",
            HostBioOrGroupDescription: "We promote local jazz.");

        // Act
        var document = EventSearchDocumentBuilder.Build(input);

        // Assert
        Assert.Contains("Jazz Night", document);
        Assert.Contains("Live music downtown", document);
        Assert.Contains("Music", document);
        Assert.Contains("City Jazz Org", document);
        Assert.Contains("We promote local jazz.", document);
        Assert.Contains("Main Hall", document);
        Assert.Contains("Sofia", document);
        Assert.Contains("123 Main St", document);
        Assert.Contains("Bulgaria", document);
        Assert.Contains("https://stream.example.com/live", document);
    }

    [Fact]
    public void Build_Should_OmitBlankOptionalFields_When_TheyAreEmpty()
    {
        // Arrange
        var input = new EventSearchDocumentBuilder.Input(
            Title: "Meetup",
            Description: "Casual hangout",
            CategoryName: null,
            Locations: [],
            HostDisplayName: "Host",
            HostBioOrGroupDescription: null);

        // Act
        var document = EventSearchDocumentBuilder.Build(input);

        // Assert
        Assert.Equal("Meetup\nCasual hangout\nHost", document);
    }
}
