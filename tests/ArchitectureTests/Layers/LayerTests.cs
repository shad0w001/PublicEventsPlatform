using NetArchTest.Rules;

namespace ArchitectureTests.Layers;

public class LayerTests : BaseTest
{
    [Fact]
    public void SharedKernel_Should_NotHaveDependencyOn_DomainLayer()
    {
        // Arrange — SharedKernel is the innermost layer

        // Act
        var result = Types.InAssembly(SharedKernelAssembly)
            .Should()
            .NotHaveDependencyOn(DomainAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void SharedKernel_Should_NotHaveDependencyOn_ApplicationLayer()
    {
        // Arrange — SharedKernel is the innermost layer

        // Act
        var result = Types.InAssembly(SharedKernelAssembly)
            .Should()
            .NotHaveDependencyOn(ApplicationAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void SharedKernel_Should_NotHaveDependencyOn_InfrastructureLayer()
    {
        // Arrange — SharedKernel is the innermost layer

        // Act
        var result = Types.InAssembly(SharedKernelAssembly)
            .Should()
            .NotHaveDependencyOn(InfrastructureAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void SharedKernel_Should_NotHaveDependencyOn_PresentationLayer()
    {
        // Arrange — SharedKernel is the innermost layer

        // Act
        var result = Types.InAssembly(SharedKernelAssembly)
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void Domain_Should_NotHaveDependencyOn_ApplicationLayer()
    {
        // Arrange — Domain must not reference use-case orchestration

        // Act
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ApplicationAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void Domain_Should_NotHaveDependencyOn_InfrastructureLayer()
    {
        // Arrange — Domain must stay free of EF, Auth0, and other adapters

        // Act
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn(InfrastructureAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void Domain_Should_NotHaveDependencyOn_PresentationLayer()
    {
        // Arrange — Domain must not reference HTTP host types

        // Act
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void Application_Should_NotHaveDependencyOn_InfrastructureLayer()
    {
        // Arrange — Application defines abstractions; Infrastructure implements them

        // Act
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn(InfrastructureAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void Application_Should_NotHaveDependencyOn_PresentationLayer()
    {
        // Arrange — Application must not reference controllers or WebApi host

        // Act
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }

    [Fact]
    public void Infrastructure_Should_NotHaveDependencyOn_PresentationLayer()
    {
        // Arrange — Infrastructure must not depend on the HTTP host

        // Act
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name!)
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, FormatFailingTypes(result));
    }
}
