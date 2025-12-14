using Branchy.Application.Git;
using Branchy.Domain.Models;
using Branchy.UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Branchy.Test;

public sealed class DiffViewModelTests : IDisposable
{
    private readonly IGitService _gitService;
    private readonly DiffViewModel _viewModel;
    private Exception? _reportedError;

    public DiffViewModelTests()
    {
        _gitService = Substitute.For<IGitService>();

        _viewModel = new DiffViewModel(
            _gitService,
            () => "/repo",
            ex => _reportedError = ex
        );
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }

    [Fact]
    public void InitialState_ShowsEmptyDiff()
    {
        Assert.True(_viewModel.ShowEmptyDiff);
        Assert.False(_viewModel.ShowDiff);
        Assert.Equal(string.Empty, _viewModel.DiffText);
    }

    [Fact]
    public async Task Load_WithChange_LoadsDiff()
    {
        var change = new FileChangeViewModel(new FileChange("file.txt", FileChangeKind.Modified, false));

        _gitService
            .GetDiffAsync("/repo", "file.txt", false, Arg.Any<CancellationToken>())
            .Returns("diff content");

        _viewModel.Load(change);

        await Task.Delay(50);

        Assert.Equal("diff content", _viewModel.DiffText);
        Assert.True(_viewModel.ShowDiff);
        Assert.False(_viewModel.ShowEmptyDiff);
    }

    [Fact]
    public async Task Load_StagedChange_LoadsStagedDiff()
    {
        var change = new FileChangeViewModel(new FileChange("file.txt", FileChangeKind.Modified, true));

        _gitService
            .GetDiffAsync("/repo", "file.txt", true, Arg.Any<CancellationToken>())
            .Returns("staged diff");

        _viewModel.Load(change);

        await Task.Delay(50);

        Assert.Equal("staged diff", _viewModel.DiffText);
    }

    [Fact]
    public void Load_NullChange_ClearsDiff()
    {
        _viewModel.Load(null);

        Assert.Equal(string.Empty, _viewModel.DiffText);
        Assert.True(_viewModel.ShowEmptyDiff);
        Assert.False(_viewModel.ShowDiff);
    }

    [Fact]
    public void Clear_ResetsDiffText()
    {
        _viewModel.Load(new FileChangeViewModel(new FileChange("file.txt", FileChangeKind.Modified, false)));
        
        _viewModel.Clear();

        Assert.Equal(string.Empty, _viewModel.DiffText);
        Assert.True(_viewModel.ShowEmptyDiff);
    }

    [Fact]
    public async Task Load_Error_ReportsError()
    {
        var change = new FileChangeViewModel(new FileChange("file.txt", FileChangeKind.Modified, false));
        var exception = new InvalidOperationException("Git error");

        _gitService
            .GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Throws(exception);

        _viewModel.Load(change);

        await Task.Delay(50);

        Assert.NotNull(_reportedError);
        Assert.Equal("Git error", _reportedError.Message);
        Assert.Equal(string.Empty, _viewModel.DiffText);
    }

    [Fact]
    public async Task Load_RapidChanges_CancelsPrevious()
    {
        var change1 = new FileChangeViewModel(new FileChange("file1.txt", FileChangeKind.Modified, false));
        var change2 = new FileChangeViewModel(new FileChange("file2.txt", FileChangeKind.Modified, false));

        _gitService
            .GetDiffAsync("/repo", "file1.txt", false, Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(200, callInfo.Arg<CancellationToken>());
                return "diff1";
            });

        _gitService
            .GetDiffAsync("/repo", "file2.txt", false, Arg.Any<CancellationToken>())
            .Returns("diff2");

        _viewModel.Load(change1);
        _viewModel.Load(change2);

        await Task.Delay(100);

        Assert.Equal("diff2", _viewModel.DiffText);
    }
}
