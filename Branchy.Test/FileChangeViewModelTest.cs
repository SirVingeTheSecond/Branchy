using Branchy.UI.Models;
using Branchy.UI.ViewModels;
using Xunit;

namespace Branchy.Test;

public sealed class FileChangeViewModelTests
{
    [Fact]
    public void Constructor_MapsProperties()
    {
        var change = new FileChange("path/to/file.txt", FileChangeKind.Modified, true);

        var viewModel = new FileChangeViewModel(change);

        Assert.Equal("path/to/file.txt", viewModel.Path);
        Assert.Equal("Modified", viewModel.Kind);
        Assert.True(viewModel.IsStaged);
    }

    [Fact]
    public void Constructor_UnstagedFile()
    {
        var change = new FileChange("file.txt", FileChangeKind.Added, false);

        var viewModel = new FileChangeViewModel(change);

        Assert.False(viewModel.IsStaged);
        Assert.Equal("Added", viewModel.Kind);
    }

    [Theory]
    [InlineData(FileChangeKind.Modified, "Modified")]
    [InlineData(FileChangeKind.Added, "Added")]
    [InlineData(FileChangeKind.Deleted, "Deleted")]
    [InlineData(FileChangeKind.Renamed, "Renamed")]
    [InlineData(FileChangeKind.Untracked, "Untracked")]
    public void Kind_MapsToString(FileChangeKind kind, string expected)
    {
        var change = new FileChange("file.txt", kind, false);

        var viewModel = new FileChangeViewModel(change);

        Assert.Equal(expected, viewModel.Kind);
    }

    [Fact]
    public void UpdateStagedState_ChangesIsStaged()
    {
        var change = new FileChange("file.txt", FileChangeKind.Modified, false);
        var viewModel = new FileChangeViewModel(change);

        Assert.False(viewModel.IsStaged);

        viewModel.UpdateStagedState(true);

        Assert.True(viewModel.IsStaged);
    }
}
