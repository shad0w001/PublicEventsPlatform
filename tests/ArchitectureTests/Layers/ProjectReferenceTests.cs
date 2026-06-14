using System.Xml.Linq;

namespace ArchitectureTests.Layers;

public class ProjectReferenceTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();

    public static IEnumerable<object[]> AllowedProjectReferences =>
    [
        new object[] { "SharedKernel", Array.Empty<string>() },
        new object[] { "Domain", new[] { "SharedKernel" } },
        new object[] { "Application", new[] { "Domain", "SharedKernel" } },
        new object[] { "Infrastructure", new[] { "Application", "Domain", "SharedKernel" } },
        new object[] { "WebApi", new[] { "Application", "Infrastructure", "SharedKernel" } },
    ];

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void Project_Should_OnlyReferenceAllowedProjects_When_CsprojIsInspected(
        string projectName,
        string[] allowedReferences)
    {
        // Arrange
        var csprojPath = Path.Combine(SolutionRoot, "src", projectName, $"{projectName}.csproj");
        var allowed = allowedReferences.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var actualReferences = ReadProjectReferences(csprojPath);

        // Assert
        var unexpected = actualReferences
            .Where(reference => !allowed.Contains(reference))
            .OrderBy(reference => reference)
            .ToArray();

        Assert.True(
            unexpected.Length == 0,
            $"{projectName} has unexpected ProjectReference(s): {string.Join(", ", unexpected)}. " +
            $"Allowed: {(allowedReferences.Length == 0 ? "(none)" : string.Join(", ", allowedReferences))}.");
    }

    private static IReadOnlyCollection<string> ReadProjectReferences(string csprojPath)
    {
        var document = XDocument.Load(csprojPath);

        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(path => path is not null)
            .Select(path => Path.GetFileNameWithoutExtension(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("PublicEventsPlatform.sln").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate PublicEventsPlatform.sln from test output directory.");
    }
}
