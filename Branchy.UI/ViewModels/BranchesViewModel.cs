using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Branchy.UI.Models;
using Branchy.UI.Services;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class BranchesViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly Func<string> _getRepositoryPath;
    private readonly Func<Task> _onOperationCompleted;
    private readonly IGitService _gitService;

    private BranchViewModel? _selectedBranch;
    private bool _hasRepository;
    private bool _showContent;
    private bool _showEmptyRepository = true;
    private bool _showEmptyBranches;

    public BranchesViewModel(
        IGitService gitService,
        Func<string> getRepositoryPath,
        Func<bool> getHasRepository,
        IObservable<bool> hasRepositoryChanged,
        Func<Task> onOperationCompleted
    )
    {
        _gitService = gitService;
        _getRepositoryPath = getRepositoryPath;
        _onOperationCompleted = onOperationCompleted;
        _hasRepository = getHasRepository();

        hasRepositoryChanged
            .Subscribe(hasRepo =>
            {
                _hasRepository = hasRepo;
                UpdateVisibilityState();
            })
            .DisposeWith(_disposables);

        CheckoutCommand = ReactiveCommand.CreateFromTask<BranchViewModel?>(
            CheckoutAsync,
            hasRepositoryChanged
        );

        Branches.CollectionChanged += OnCollectionChanged;
    }

    public ObservableCollection<BranchViewModel> Branches { get; } = new();

    public BranchViewModel? SelectedBranch
    {
        get => _selectedBranch;
        set => this.RaiseAndSetIfChanged(ref _selectedBranch, value);
    }

    public bool ShowContent
    {
        get => _showContent;
        private set => this.RaiseAndSetIfChanged(ref _showContent, value);
    }

    public bool ShowEmptyRepository
    {
        get => _showEmptyRepository;
        private set => this.RaiseAndSetIfChanged(ref _showEmptyRepository, value);
    }

    public bool ShowEmptyBranches
    {
        get => _showEmptyBranches;
        private set => this.RaiseAndSetIfChanged(ref _showEmptyBranches, value);
    }

    public ReactiveCommand<BranchViewModel?, Unit> CheckoutCommand { get; }

    public void Dispose()
    {
        Branches.CollectionChanged -= OnCollectionChanged;
        CheckoutCommand.Dispose();
        _disposables.Dispose();
    }

    public void Update(IReadOnlyList<Branch> branches)
    {
        Branches.Clear();

        foreach (var branch in branches)
        {
            Branches.Add(new BranchViewModel(branch));
        }

        SelectedBranch = FindCurrentBranch();
    }

    public void Clear()
    {
        Branches.Clear();
        SelectedBranch = null;
    }

    private BranchViewModel? FindCurrentBranch()
    {
        foreach (var branch in Branches)
        {
            if (branch.IsCurrent)
            {
                return branch;
            }
        }

        return null;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisibilityState();
    }

    private void UpdateVisibilityState()
    {
        ShowContent = _hasRepository && Branches.Count > 0;
        ShowEmptyRepository = !_hasRepository;
        ShowEmptyBranches = _hasRepository && Branches.Count == 0;
    }

    private async Task CheckoutAsync(BranchViewModel? branch)
    {
        if (branch == null || branch.IsCurrent)
        {
            return;
        }

        var repoPath = _getRepositoryPath();
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return;
        }

        await _gitService.CheckoutAsync(repoPath, branch.Name);
        await _onOperationCompleted();
    }
}
