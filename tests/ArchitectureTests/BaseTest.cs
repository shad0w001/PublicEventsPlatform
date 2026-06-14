using System.Reflection;
using Application.Abstractions.Messaging;
using Domain.Users;
using Infrastructure.Database;
using NetArchTest.Rules;
using SharedKernel;
using WebApi.Controllers;

namespace ArchitectureTests;

public abstract class BaseTest
{
    protected static readonly Assembly SharedKernelAssembly = typeof(Entity).Assembly;
    protected static readonly Assembly DomainAssembly = typeof(User).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(ICommand).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;
    protected static readonly Assembly PresentationAssembly = typeof(UsersController).Assembly;

    protected static string FormatFailingTypes(TestResult result) =>
        result.IsSuccessful
            ? string.Empty
            : string.Join(", ", result.FailingTypes.Select(t => t.FullName));
}
