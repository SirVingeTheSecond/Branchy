using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Branchy.UI.Models;

namespace Branchy.UI.Services;

public sealed class GitCliService : IGitService
{
    public async Task<bool> IsRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunGitAsync("rev-parse --is-inside-work-tree", path, cancellationToken);
        return result.ExitCode == 0 && result.StandardOutput.Trim() == "true";
    }

    public async Task<RepositoryStatus> GetStatusAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunGitAsync(
            "status --porcelain=v2 -b",
            repositoryPath,
            cancellationToken
        );

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git status failed with exit code {result.ExitCode}{Environment.NewLine}{result.StandardError}"
            );
        }

        return GitStatusParser.Parse(repositoryPath, result.StandardOutput);
    }

    public async Task StageFileAsync(
        string repositoryPath,
        string relativePath,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunGitAsync(
            $"add \"{relativePath}\"",
            repositoryPath,
            cancellationToken
        );

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git add failed with exit code {result.ExitCode}{Environment.NewLine}{result.StandardError}"
            );
        }
    }

    public async Task UnstageFileAsync(
        string repositoryPath,
        string relativePath,
        CancellationToken cancellationToken = default
    )
    {
        var result = await RunGitAsync(
            $"restore --staged \"{relativePath}\"",
            repositoryPath,
            cancellationToken
        );

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git restore staged failed with exit code {result.ExitCode}{Environment.NewLine}{result.StandardError}"
            );
        }
    }

    public async Task CommitAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        var escapedMessage = message.Replace("\"", "\\\"");
        var args = $"commit -m \"{escapedMessage}\"";

        var result = await RunGitAsync(
            args,
            repositoryPath,
            cancellationToken
        );

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git commit failed with exit code {result.ExitCode}{Environment.NewLine}{result.StandardError}"
            );
        }
    }

    public async Task<string> GetDiffAsync(
        string repositoryPath,
        string relativePath,
        bool staged,
        CancellationToken cancellationToken = default
    )
    {
        var args = staged
            ? $"diff --cached -- \"{relativePath}\""
            : $"diff -- \"{relativePath}\"";

        var result = await RunGitAsync(
            args,
            repositoryPath,
            cancellationToken
        );

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git diff failed with exit code {result.ExitCode}{Environment.NewLine}{result.StandardError}"
            );
        }

        return result.StandardOutput;
    }

    private sealed record GitResult(int ExitCode, string StandardOutput, string StandardError);

    private static async Task<GitResult> RunGitAsync(
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken
    )
    {
        // This method ensures logic for process execution is just here so future changes to process behavior happen in one place
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ToDo:
        // Initialize object properties inside the 'using' statement to ensure that the object is disposed
        // if an exception is thrown during initialization
        using var process = new Process { StartInfo = startInfo };

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new GitResult(
            process.ExitCode,
            output.ToString(),
            error.ToString()
        );
    }
}
