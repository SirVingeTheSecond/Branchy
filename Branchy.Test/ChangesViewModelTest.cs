using System.Reactive.Linq;
using System.Reactive.Subjects;
using Branchy.Application.Git;
using Branchy.Domain.Models;
using Branchy.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace Branchy.Test;

public sealed class ChangesViewModelTests : IDisposable
{
    private readonly IGitService _gitService;
    private readonly BehaviorSubject<bool> _hasRepository;
    private readonly ChangesViewModel _viewModel;

    private bool _stageCompleted;
    private bool _unstageCompleted;

    public ChangesViewModelTests()
    {
        _gitService = Substitute.For<IGitService>();
        _hasRepository = new BehaviorSubject<bool>(false);

        _viewModel = new ChangesViewModel(
            _gitService,
            () => "/repo",
            () => _hasRepository.Value,
            _hasRepository,
            () => { _stageCompleted = true; return Task.CompletedTask; },
            () => { _unstageCompleted = true; return Task.CompletedTask; }
        );
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        _hasRepository.Dispose();
    }

    [Fact]
    public void InitialState_ShowsEmptyRepository()
    {
        Assert.True(_viewModel.ShowEmptyRepository);
        Assert.False(_viewModel.ShowList);
        Assert.False(_viewModel.ShowEmptyChanges);
    }

    [Fact]
    public void HasRepository_NoChanges_ShowsEmptyChanges()
    {
        _hasRepository.OnNext(true);

        Assert.False(_viewModel.ShowEmptyRepository);
        Assert.False(_viewModel.ShowList);
        Assert.True(_viewModel.ShowEmptyChanges);
    }

    [Fact]
    public void HasRepository_WithChanges_ShowsList()
    {
        _hasRepository.OnNext(true);

        var status = new RepositoryStatus(
            "/repo",
            new BranchStatus("main", 0, 0),
            new[] { new FileChange("file.txt", FileChangeKind.Modified, false) }
        );

        _viewModel.Update(status, null);

        Assert.False(_viewModel.ShowEmptyRepository);
        Assert.True(_viewModel.ShowList);
        Assert.False(_viewModel.ShowEmptyChanges);
    }

    [Fact]
    public void Update_PopulatesChanges()
    {
        var status = new RepositoryStatus(
            "/repo",
            new BranchStatus("main", 0, 0),
            new[]
            {
                new FileChange("file1.txt", FileChangeKind.Modified, false),
                new FileChange("file2.txt", FileChangeKind.Added, true)
            }
        );

        _viewModel.Update(status, null);

        Assert.Equal(2, _viewModel.Changes.Count);
        Assert.Equal("file1.txt", _viewModel.Changes[0].Path);
        Assert.Equal("file2.txt", _viewModel.Changes[1].Path);
    }

    [Fact]
    public void Update_PreservesSelection()
    {
        var status1 = new RepositoryStatus(
            "/repo",
            new BranchStatus("main", 0, 0),
            new[] { new FileChange("file.txt", FileChangeKind.Modified, false) }
        );

        _viewModel.Update(status1, null);
        _viewModel.SelectedChange = _viewModel.Changes[0];

        var status2 = new RepositoryStatus(
            "/repo",
            new BranchStatus("main", 0, 0),
            new[] { new FileChange("file.txt", FileChangeKind.Modified, true) }
        );

        _viewModel.Update(status2, "file.txt");

        Assert.NotNull(_viewModel.SelectedChange);
        Assert.Equal("file.txt", _viewModel.SelectedChange.Path);
    }

    [Fact]
    public void SelectedChange_SetsHasSelection()
    {
        var status = new RepositoryStatus(
            "/repo",
            new BranchStatus("main", 0, 0),
            new[] { new FileChange("file.txt", FileChangeKind.Modified, false) }
        );

        _viewModel.Update(status, null);

        Assert.False(_viewModel.HasSelection);

        _viewModel.SelectedChange = _viewModel.Changes[0];

        Assert.True(_viewModel.HasSelection);
    }

    [Fact]
    public async Task StageCommand_CallsGitService()
    {
        _hasRepository.OnNext(true);

        var status = new RepositoryStatus(
            "/repo",
            new BranchStatus("main", 0, 0),
            new[] { new FileChange("file.txt", FileChangeKind.Modified, false) }
        );

        _viewModel.Update(status, null);
        var change = _viewModel.Changes[0];

        await _viewModel.StageCommand.Execute(change);

        await _gitService.Received(1).StageFileAsync("/repo", "file.txt", Arg.Any<CancellationToken>());
        Assert.True(_stageCompleted);
    }

    [Fact]
    public async Task UnstageCommand_CallsGitService()
    {
        _hasRepository.OnNext(true);

        var status = new RepositoryStatus(
            "/repo",
            new BranchStatus("main", 0, 0),
            new[] { new FileChange("file.txt", FileChangeKind.Modified, true) }
        );

        _viewModel.Update(status, null);
        var change = _viewModel.Changes[0];

        await _viewModel.UnstageCommand.Execute(change);

        await _gitService.Received(1).UnstageFileAsync("/repo", "file.txt", Arg.Any<CancellationToken>());
        Assert.True(_unstageCompleted);
    }

    [Fact]
    public async Task StageCommand_NullChange_DoesNothing()
    {
        _hasRepository.OnNext(true);

        await _viewModel.StageCommand.Execute(null);

        await _gitService.DidNotReceive().StageFileAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.False(_stageCompleted);
    }

    [Fact]
    public void ClearSelection_SetsSelectedChangeToNull()
    {
        var status = new RepositoryStatus(
            "/repo",
            new BranchStatus("main", 0, 0),
            new[] { new FileChange("file.txt", FileChangeKind.Modified, false) }
        );

        _viewModel.Update(status, null);
        _viewModel.SelectedChange = _viewModel.Changes[0];

        _viewModel.ClearSelection();

        Assert.Null(_viewModel.SelectedChange);
    }
}
