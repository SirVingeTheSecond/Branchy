using Branchy.Application.Git;
using Branchy.Domain.Models;

namespace Branchy.Application.Repositories;

public sealed class GetRepositoryStatusUseCase
{
    private IGitService _gitService;

    public GetRepositoryStatusUseCase(IGitService gitService)
    {
        _gitService = gitService;
    }

    public Task<RepositoryStatus> ExecuteAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        return _gitService.GetStatusAsync(repositoryPath, cancellationToken);
    }
}