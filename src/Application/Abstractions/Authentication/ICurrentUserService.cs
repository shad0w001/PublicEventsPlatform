using Domain.Users;
using SharedKernel;

namespace Application.Abstractions.Authentication;

public interface ICurrentUserService
{
    Task<Result<User>> GetOrProvisionAsync(CancellationToken cancellationToken = default);
}
