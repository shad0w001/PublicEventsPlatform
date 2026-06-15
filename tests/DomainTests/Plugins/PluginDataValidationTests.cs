using System.Text.Json;
using Domain.Plugins;
using Domain.Plugins.Services;

namespace DomainTests.Plugins;

public class PluginDataValidationTests
{
    private static readonly DateTime EventStart = new(2026, 7, 1, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EventEnd = new(2026, 7, 1, 22, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PluginService_Should_ValidateAgendaSessions_When_DataIsValid()
    {
        // Arrange
        var sessions = JsonSerializer.Serialize(new[]
        {
            new
            {
                title = "Keynote",
                description = "Opening talk",
                start = EventStart,
                end = EventStart.AddHours(1),
                speaker = "Jane Doe"
            }
        });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeySessions] = sessions };

        // Act
        var result = PluginService.ValidatePluginData(
            PluginConstants.CodeAgenda,
            data,
            EventStart,
            EventEnd);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void PluginService_Should_ReturnInvalidData_When_AgendaSessionsMissing()
    {
        // Arrange
        var data = new Dictionary<string, string?> { ["other"] = "[]" };

        // Act
        var result = PluginService.ValidatePluginData(
            PluginConstants.CodeAgenda,
            data,
            EventStart,
            EventEnd);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.InvalidData", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ReturnInvalidData_When_AgendaSessionOutOfEventWindow()
    {
        // Arrange
        var sessions = JsonSerializer.Serialize(new[]
        {
            new
            {
                title = "Late",
                description = "Too late",
                start = EventEnd,
                end = EventEnd.AddHours(1)
            }
        });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeySessions] = sessions };

        // Act
        var result = PluginService.ValidatePluginData(
            PluginConstants.CodeAgenda,
            data,
            EventStart,
            EventEnd);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.InvalidData", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ReturnInvalidData_When_AgendaSessionTimeRangeInvalid()
    {
        // Arrange
        var sessions = JsonSerializer.Serialize(new[]
        {
            new
            {
                title = "Bad range",
                description = "End before start",
                start = EventStart.AddHours(2),
                end = EventStart.AddHours(1)
            }
        });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeySessions] = sessions };

        // Act
        var result = PluginService.ValidatePluginData(
            PluginConstants.CodeAgenda,
            data,
            EventStart,
            EventEnd);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.InvalidData", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ValidateFaqEntries_When_DataIsValid()
    {
        // Arrange
        var entries = JsonSerializer.Serialize(new[]
        {
            new { question = "Is parking available?", answer = "Yes, on Main St." }
        });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = entries };

        // Act
        var result = PluginService.ValidatePluginData(PluginConstants.CodeFaq, data, EventStart, EventEnd);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void PluginService_Should_ReturnInvalidData_When_FaqAnswerEmpty()
    {
        // Arrange
        var entries = JsonSerializer.Serialize(new[] { new { question = "Q?", answer = "  " } });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeyEntries] = entries };

        // Act
        var result = PluginService.ValidatePluginData(PluginConstants.CodeFaq, data, EventStart, EventEnd);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.InvalidData", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ValidateLinks_When_DataIsValid()
    {
        // Arrange
        var links = JsonSerializer.Serialize(new[]
        {
            new
            {
                label = "Slides",
                url = "https://example.com/slides",
                description = "Talk materials"
            }
        });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeyLinks] = links };

        // Act
        var result = PluginService.ValidatePluginData(PluginConstants.CodeLinks, data, EventStart, EventEnd);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void PluginService_Should_ReturnInvalidData_When_LinkUrlInvalid()
    {
        // Arrange
        var links = JsonSerializer.Serialize(new[] { new { label = "Bad", url = "not-a-url" } });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeyLinks] = links };

        // Act
        var result = PluginService.ValidatePluginData(PluginConstants.CodeLinks, data, EventStart, EventEnd);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.InvalidData", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ReturnInvalidData_When_LinkLabelMissing()
    {
        // Arrange
        var links = JsonSerializer.Serialize(new[] { new { label = "", url = "https://example.com" } });
        var data = new Dictionary<string, string?> { [PluginConstants.DataKeyLinks] = links };

        // Act
        var result = PluginService.ValidatePluginData(PluginConstants.CodeLinks, data, EventStart, EventEnd);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.InvalidData", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ReturnDataRequired_When_DataEmpty()
    {
        // Arrange
        var data = new Dictionary<string, string?>();

        // Act
        var result = PluginService.ValidatePluginData(PluginConstants.CodeAgenda, data, EventStart, EventEnd);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.DataRequired", result.Error.Code);
    }

    [Fact]
    public void PluginService_Should_ReturnUnknownCode_When_CodeNotRecognized()
    {
        // Arrange
        var data = new Dictionary<string, string?> { ["x"] = "y" };

        // Act
        var result = PluginService.ValidatePluginData("unknown", data, EventStart, EventEnd);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Plugins.UnknownCode", result.Error.Code);
    }
}
