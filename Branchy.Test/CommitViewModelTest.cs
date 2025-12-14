using System.Reactive.Linq;
using System.Reactive.Subjects;
using Branchy.Application.Git;
using Branchy.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace Branchy.Test;

public sealed class CommitViewModelTests : IDisposable
{
    private readonly IGitService _gitService;
    private readonly BehaviorSubject<bool> _hasRepository;
    private readonly CommitViewModel _viewModel;
    private bool _commitCompleted;

    public CommitViewModelTests()
    {
        _gitService = Substitute.For<IGitService>();
        _hasRepository = new BehaviorSubject<bool>(false);

        _viewModel = new CommitViewModel(
            _gitService,
            () => "/repo",
            _hasRepository,
            () => { _commitCompleted = true; return Task.CompletedTask; }
        );
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        _hasRepository.Dispose();
    }

    [Fact]
    public void InitialState_CommitMessageEmpty()
    {
        Assert.Equal(string.Empty, _viewModel.CommitMessage);
    }

    [Fact]
    public async Task CommitCommand_NoRepository_CannotExecute()
    {
        _viewModel.CommitMessage = "test message";

        var canExecute = await _viewModel.CommitCommand.CanExecute.FirstAsync();

        Assert.False(canExecute);
    }

    [Fact]
    public async Task CommitCommand_EmptyMessage_CannotExecute()
    {
        _hasRepository.OnNext(true);
        _viewModel.CommitMessage = "";

        var canExecute = await _viewModel.CommitCommand.CanExecute.FirstAsync();

        Assert.False(canExecute);
    }

    [Fact]
    public async Task CommitCommand_WhitespaceMessage_CannotExecute()
    {
        _hasRepository.OnNext(true);
        _viewModel.CommitMessage = "   ";

        var canExecute = await _viewModel.CommitCommand.CanExecute.FirstAsync();

        Assert.False(canExecute);
    }

    [Fact]
    public async Task CommitCommand_ValidState_CanExecute()
    {
        _hasRepository.OnNext(true);
        _viewModel.CommitMessage = "test message";

        var canExecute = await _viewModel.CommitCommand.CanExecute.FirstAsync();

        Assert.True(canExecute);
    }

    [Fact]
    public async Task CommitCommand_CallsGitService()
    {
        _hasRepository.OnNext(true);
        _viewModel.CommitMessage = "test message";

        await _viewModel.CommitCommand.Execute();

        await _gitService.Received(1).CommitAsync("/repo", "test message", Arg.Any<CancellationToken>());
        Assert.True(_commitCompleted);
    }

    [Fact]
    public void Clear_ResetsCommitMessage()
    {
        _viewModel.CommitMessage = "some message";

        _viewModel.Clear();

        Assert.Equal(string.Empty, _viewModel.CommitMessage);
    }
}
