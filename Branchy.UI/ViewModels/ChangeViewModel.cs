using System;
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

public sealed class ChangesViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly Func<string> _getRepositoryPath;
    private readonly Func<Task> _onOperationCompleted;
    private readonly IGitService _gitService;

    private FileChangeViewModel? _selectedChange;
    private bool _hasRepository;
    private bool _showContent;
    private bool _showEmptyRepository = true;
    private bool _showEmptyChanges;

    public ChangesViewModel(
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

        StageCommand = ReactiveCommand.CreateFromTask<FileChangeViewModel?>(
            StageAsync,
            hasRepositoryChanged
        );

        UnstageCommand = ReactiveCommand.CreateFromTask<FileChangeViewModel?>(
            UnstageAsync,
            hasRepositoryChanged
        );

        Changes.CollectionChanged += OnCollectionChanged;
    }

    public ObservableCollection<FileChangeViewModel> Changes { get; } = new();

    public FileChangeViewModel? SelectedChange
    {
        get => _selectedChange;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedChange, value);
            this.RaisePropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedChange != null;

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

    public bool ShowEmptyChanges
    {
        get => _showEmptyChanges;
        private set => this.RaiseAndSetIfChanged(ref _showEmptyChanges, value);
    }

    public ReactiveCommand<FileChangeViewModel?, Unit> StageCommand { get; }

    public ReactiveCommand<FileChangeViewModel?, Unit> UnstageCommand { get; }

    public void Dispose()
    {
        Changes.CollectionChanged -= OnCollectionChanged;
        StageCommand.Dispose();
        UnstageCommand.Dispose();
        _disposables.Dispose();
    }

    public void Update(RepositoryStatus status, string? preserveSelectionPath)
    {
        var selectionPath = preserveSelectionPath ?? SelectedChange?.Path;

        Changes.Clear();

        foreach (var change in status.Changes)
        {
            Changes.Add(new FileChangeViewModel(change));
        }

        SelectedChange = selectionPath != null
            ? FindChangeByPath(selectionPath)
            : null;
    }

    public void ClearSelection()
    {
        SelectedChange = null;
    }

    private FileChangeViewModel? FindChangeByPath(string path)
    {
        foreach (var change in Changes)
        {
            if (change.Path == path)
            {
                return change;
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
        ShowContent = _hasRepository && Changes.Count > 0;
        ShowEmptyRepository = !_hasRepository;
        ShowEmptyChanges = _hasRepository && Changes.Count == 0;
    }

    private async Task StageAsync(FileChangeViewModel? change)
    {
        if (change == null)
        {
            return;
        }

        var repoPath = _getRepositoryPath();
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return;
        }

        await _gitService.StageFileAsync(repoPath, change.Path);
        await _onOperationCompleted();
    }

    private async Task UnstageAsync(FileChangeViewModel? change)
    {
        if (change == null)
        {
            return;
        }

        var repoPath = _getRepositoryPath();
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return;
        }

        await _gitService.UnstageFileAsync(repoPath, change.Path);
        await _onOperationCompleted();
    }
}
