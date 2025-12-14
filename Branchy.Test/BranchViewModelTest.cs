using System.Reactive.Linq;
using System.Reactive.Subjects;
using Branchy.UI.Models;
using Branchy.UI.Services;
using Branchy.UI.ViewModels;
using NSubstitute;
using Xunit;

namespace Branchy.Test;

public sealed class BranchesViewModelTest : IDisposable
{
    private readonly IGitService _gitService;
    private readonly BehaviorSubject<bool> _hasRepository;
    private readonly BranchesViewModel _viewModel;

    private bool _operationCompleted;

    public BranchesViewModelTest()
    {
        _gitService = Substitute.For<IGitService>();
        _hasRepository = new BehaviorSubject<bool>(false);

        _viewModel = new BranchesViewModel(
            _gitService,
            () => "/repo",
            () => _hasRepository.Value,
            _hasRepository,
            () => { _operationCompleted = true; return Task.CompletedTask; }
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
        Assert.False(_viewModel.ShowContent);
        Assert.False(_viewModel.ShowEmptyBranches);
    }

    [Fact]
    public void HasRepository_NoBranches_ShowsEmptyBranches()
    {
        _hasRepository.OnNext(true);

        Assert.False(_viewModel.ShowEmptyRepository);
        Assert.False(_viewModel.ShowContent);
        Assert.True(_viewModel.ShowEmptyBranches);
    }

    [Fact]
    public void HasRepository_WithBranches_ShowsContent()
    {
        _hasRepository.OnNext(true);

        var branches = new[]
        {
            new Branch("main", true, false),
            new Branch("feature", false, false)
        };

        _viewModel.Update(branches);

        Assert.False(_viewModel.ShowEmptyRepository);
        Assert.True(_viewModel.ShowContent);
        Assert.False(_viewModel.ShowEmptyBranches);
    }

    [Fact]
    public void Update_PopulatesBranches()
    {
        var branches = new[]
        {
            new Branch("main", true, false),
            new Branch("feature", false, false),
            new Branch("origin/main", false, true)
        };

        _viewModel.Update(branches);

        Assert.Equal(3, _viewModel.Branches.Count);
        Assert.Equal("main", _viewModel.Branches[0].Name);
        Assert.Equal("feature", _viewModel.Branches[1].Name);
        Assert.Equal("origin/main", _viewModel.Branches[2].Name);
    }

    [Fact]
    public void Update_SelectsCurrentBranch()
    {
        var branches = new[]
        {
            new Branch("main", false, false),
            new Branch("feature", true, false)
        };

        _viewModel.Update(branches);

        Assert.NotNull(_viewModel.SelectedBranch);
        Assert.Equal("feature", _viewModel.SelectedBranch.Name);
        Assert.True(_viewModel.SelectedBranch.IsCurrent);
    }

    [Fact]
    public void Clear_RemovesBranchesAndSelection()
    {
        var branches = new[]
        {
            new Branch("main", true, false)
        };

        _viewModel.Update(branches);
        Assert.Single(_viewModel.Branches);

        _viewModel.Clear();

        Assert.Empty(_viewModel.Branches);
        Assert.Null(_viewModel.SelectedBranch);
    }

    [Fact]
    public async Task CheckoutCommand_CallsGitService()
    {
        _hasRepository.OnNext(true);

        var branches = new[]
        {
            new Branch("main", true, false),
            new Branch("feature", false, false)
        };

        _viewModel.Update(branches);
        var featureBranch = _viewModel.Branches[1];

        await _viewModel.CheckoutCommand.Execute(featureBranch);

        await _gitService.Received(1).CheckoutAsync("/repo", "feature", Arg.Any<CancellationToken>());
        Assert.True(_operationCompleted);
    }

    [Fact]
    public async Task CheckoutCommand_CurrentBranch_DoesNothing()
    {
        _hasRepository.OnNext(true);

        var branches = new[]
        {
            new Branch("main", true, false)
        };

        _viewModel.Update(branches);
        var currentBranch = _viewModel.Branches[0];

        await _viewModel.CheckoutCommand.Execute(currentBranch);

        await _gitService.DidNotReceive().CheckoutAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.False(_operationCompleted);
    }

    [Fact]
    public async Task CheckoutCommand_NullBranch_DoesNothing()
    {
        _hasRepository.OnNext(true);

        await _viewModel.CheckoutCommand.Execute(null);

        await _gitService.DidNotReceive().CheckoutAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.False(_operationCompleted);
    }

    [Fact]
    public void BranchViewModel_DisplayName_StripsOriginPrefix()
    {
        var remoteBranch = new Branch("origin/feature", false, true);
        var viewModel = new BranchViewModel(remoteBranch);

        Assert.Equal("feature", viewModel.DisplayName);
        Assert.Equal("origin/feature", viewModel.Name);
    }

    [Fact]
    public void BranchViewModel_DisplayName_PreservesLocalName()
    {
        var localBranch = new Branch("feature", false, false);
        var viewModel = new BranchViewModel(localBranch);

        Assert.Equal("feature", viewModel.DisplayName);
        Assert.Equal("feature", viewModel.Name);
    }
}
