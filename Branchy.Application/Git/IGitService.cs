using Branchy.Domain.Models;

namespace Branchy.Application.Git;

public interface IGitService
{
    Task<bool> IsRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default
    );

    Task<RepositoryStatus> GetStatusAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default
    );

    Task StageFileAsync(
        string repositoryPath,
        string relativePath,
        CancellationToken cancellationToken = default
    );

    Task UnstageFileAsync(
        string repositoryPath,
        string relativePath,
        CancellationToken cancellationToken = default
    );

    Task CommitAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken = default
    );

    Task<string> GetDiffAsync(
        string repositoryPath,
        string relativePath,
        bool staged,
        CancellationToken cancellationToken = default
    );
}
