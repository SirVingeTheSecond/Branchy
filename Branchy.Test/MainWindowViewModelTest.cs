using System.Reactive.Linq;
using Branchy.UI.Models;
using Branchy.UI.Services;
using Branchy.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace Branchy.Test;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly IGitService _gitService;
    private readonly IDialogService _dialogService;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        _gitService = Substitute.For<IGitService>();
        _dialogService = Substitute.For<IDialogService>();

        _viewModel = new MainWindowViewModel(_gitService, _dialogService);
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public void InitialState_NoRepository()
    {
        Assert.Equal(string.Empty, _viewModel.RepositoryPath);
        Assert.False(_viewModel.HasRepository);
        Assert.Equal(string.Empty, _viewModel.BranchDisplay);
        Assert.False(_viewModel.HasError);
    }

    [Fact]
    public async Task BrowseCommand_UserCancels_NoChange()
    {
        _dialogService.ShowFolderPickerAsync().Returns((string?)null);

        await _viewModel.BrowseRepositoryCommand.Execute();

        Assert.Equal(string.Empty, _viewModel.RepositoryPath);
    }

    [Fact]
    public async Task BrowseCommand_NotGitRepo_ShowsError()
    {
        _dialogService.ShowFolderPickerAsync().Returns("/not/a/repo");
        _gitService.IsRepositoryAsync("/not/a/repo", Arg.Any<CancellationToken>()).Returns(false);

        await _viewModel.BrowseRepositoryCommand.Execute();

        Assert.True(_viewModel.HasError);
        Assert.Contains("not a Git repository", _viewModel.ErrorMessage);
        Assert.Equal(string.Empty, _viewModel.RepositoryPath);
    }

    [Fact]
    public async Task BrowseCommand_ValidRepo_LoadsStatus()
    {
        _dialogService.ShowFolderPickerAsync().Returns("/valid/repo");
        _gitService.IsRepositoryAsync("/valid/repo", Arg.Any<CancellationToken>()).Returns(true);
        _gitService.GetStatusAsync("/valid/repo", Arg.Any<CancellationToken>())
            .Returns(new RepositoryStatus(
                "/valid/repo",
                new BranchStatus("main", 1, 2),
                Array.Empty<FileChange>()
            ));

        await _viewModel.BrowseRepositoryCommand.Execute();

        Assert.Equal("/valid/repo", _viewModel.RepositoryPath);
        Assert.True(_viewModel.HasRepository);
        Assert.Equal("main ↑1 ↓2", _viewModel.BranchDisplay);
        Assert.False(_viewModel.HasError);
    }

    [Fact]
    public async Task ReloadCommand_UpdatesChanges()
    {
        _viewModel.RepositoryPath = "/repo";
        _gitService.GetStatusAsync("/repo", Arg.Any<CancellationToken>())
            .Returns(new RepositoryStatus(
                "/repo",
                new BranchStatus("feature", 0, 0),
                new[] { new FileChange("file.txt", FileChangeKind.Modified, false) }
            ));

        await _viewModel.ReloadStatusCommand.Execute();

        Assert.Equal("feature ↑0 ↓0", _viewModel.BranchDisplay);
        Assert.Single(_viewModel.Changes.Changes);
    }

    [Fact]
    public void DismissErrorCommand_ClearsError()
    {
        _viewModel.BrowseRepositoryCommand.Execute().Wait();

        _viewModel.DismissErrorCommand.Execute().Wait();

        Assert.False(_viewModel.HasError);
        Assert.Null(_viewModel.ErrorMessage);
    }

    [Fact]
    public void SettingRepositoryPath_UpdatesHasRepository()
    {
        Assert.False(_viewModel.HasRepository);

        _viewModel.RepositoryPath = "/some/path";

        Assert.True(_viewModel.HasRepository);
    }

    [Fact]
    public void ChildViewModels_AreInitialized()
    {
        Assert.NotNull(_viewModel.Changes);
        Assert.NotNull(_viewModel.Diff);
        Assert.NotNull(_viewModel.Commit);
    }

    [Fact]
    public async Task SelectingChange_LoadsDiff()
    {
        _viewModel.RepositoryPath = "/repo";
        
        _gitService.GetStatusAsync("/repo", Arg.Any<CancellationToken>())
            .Returns(new RepositoryStatus(
                "/repo",
                new BranchStatus("main", 0, 0),
                new[] { new FileChange("file.txt", FileChangeKind.Modified, false) }
            ));

        _gitService.GetDiffAsync("/repo", "file.txt", false, Arg.Any<CancellationToken>())
            .Returns("diff content");

        await _viewModel.ReloadStatusCommand.Execute();
        _viewModel.Changes.SelectedChange = _viewModel.Changes.Changes[0];

        await Task.Delay(50);

        Assert.Equal("diff content", _viewModel.Diff.DiffText);
    }
}
