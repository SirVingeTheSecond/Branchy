using System;
using System.Collections.Generic;
using Branchy.UI.Models;

namespace Branchy.UI.Services;

public static class GitBranchParser
{
    public static IReadOnlyList<Branch> Parse(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var branches = new List<Branch>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            var branch = ParseLine(line);
            if (branch != null)
            {
                branches.Add(branch);
            }
        }

        return branches;
    }

    private static Branch? ParseLine(string line)
    {
        // Format: name|*|refs/heads or refs/remotes
        var parts = line.Split('|');
        if (parts.Length < 2)
        {
            return null;
        }

        var name = parts[0].Trim();
        var isCurrent = parts[1].Trim() == "*";
        var isRemote = name.StartsWith("origin/", StringComparison.Ordinal);

        // Skip HEAD pointer for remotes
        if (name == "origin/HEAD")
        {
            return null;
        }

        return new Branch(name, isCurrent, isRemote);
    }
}
