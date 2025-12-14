using System;
using System.Collections.Generic;
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
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync("rev-parse --is-inside-work-tree", path, cancellationToken);
        return result.ExitCode == 0 && result.StandardOutput.Trim() == "true";
    }

    public async Task<RepositoryStatus> GetStatusAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync("status --porcelain=v2 -b", repositoryPath, cancellationToken);
        ThrowIfFailed(result);
        return GitStatusParser.Parse(repositoryPath, result.StandardOutput);
    }

    public async Task<IReadOnlyList<Branch>> GetBranchesAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(
            "branch -a --format=%(refname:short)|%(HEAD)|%(refname:rstrip=-2)",
            repositoryPath,
            cancellationToken);
        ThrowIfFailed(result);
        return GitBranchParser.Parse(result.StandardOutput);
    }

    public async Task CheckoutAsync(
        string repositoryPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync($"checkout \"{branchName}\"", repositoryPath, cancellationToken);
        ThrowIfFailed(result);
    }

    public async Task StageFileAsync(
        string repositoryPath,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync($"add \"{relativePath}\"", repositoryPath, cancellationToken);
        ThrowIfFailed(result);
    }

    public async Task UnstageFileAsync(
        string repositoryPath,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync($"restore --staged \"{relativePath}\"", repositoryPath, cancellationToken);
        ThrowIfFailed(result);
    }

    public async Task CommitAsync(
        string repositoryPath,
        string message,
        CancellationToken cancellationToken = default)
    {
        var escapedMessage = message.Replace("\"", "\\\"");
        var result = await RunGitAsync($"commit -m \"{escapedMessage}\"", repositoryPath, cancellationToken);
        ThrowIfFailed(result);
    }

    public async Task<string> GetDiffAsync(
        string repositoryPath,
        string relativePath,
        bool staged,
        CancellationToken cancellationToken = default)
    {
        var args = staged
            ? $"diff --cached -- \"{relativePath}\""
            : $"diff -- \"{relativePath}\"";

        var result = await RunGitAsync(args, repositoryPath, cancellationToken);
        ThrowIfFailed(result);
        return result.StandardOutput;
    }

    private static void ThrowIfFailed(GitResult result)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(ExtractErrorMessage(result.StandardError));
    }

    private static string ExtractErrorMessage(string stderr)
    {
        var message = stderr.Trim();

        if (string.IsNullOrEmpty(message))
        {
            return "An unknown error occurred.";
        }

        // Extract first line only
        var newlineIndex = message.IndexOf('\n');
        if (newlineIndex > 0)
        {
            message = message[..newlineIndex].Trim();
        }

        // Strip common prefixes
        message = StripPrefix(message, "fatal:");
        message = StripPrefix(message, "error:");

        // Capitalize first letter
        if (message.Length > 0 && char.IsLower(message[0]))
        {
            message = char.ToUpper(message[0]) + message[1..];
        }

        return message;
    }

    private static string StripPrefix(string message, string prefix)
    {
        if (message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return message[prefix.Length..].TrimStart();
        }

        return message;
    }

    private sealed record GitResult(int ExitCode, string StandardOutput, string StandardError);

    private static async Task<GitResult> RunGitAsync(
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
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

        return new GitResult(process.ExitCode, output.ToString(), error.ToString());
    }
}
