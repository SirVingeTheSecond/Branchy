using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Branchy.UI.Models;

namespace Branchy.UI.Services;

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

    Task<IReadOnlyList<Branch>> GetBranchesAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default
    );

    Task CheckoutAsync(
        string repositoryPath,
        string branchName,
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
