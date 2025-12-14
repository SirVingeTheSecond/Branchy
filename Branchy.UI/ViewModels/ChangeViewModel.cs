using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using Branchy.UI.Models;
using Branchy.UI.Services;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class ChangesViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();
    private readonly Func<string> _getRepositoryPath;
    private readonly Func<Task> _onStageCompleted;
    private readonly Func<Task> _onUnstageCompleted;
    private readonly IGitService _gitService;

    private FileChangeViewModel? _selectedChange;
    private bool _hasRepository;

    public ChangesViewModel(
        IGitService gitService,
        Func<string> getRepositoryPath,
        Func<bool> getHasRepository,
        IObservable<bool> hasRepositoryChanged,
        Func<Task> onStageCompleted,
        Func<Task> onUnstageCompleted
    )
    {
        _gitService = gitService;
        _getRepositoryPath = getRepositoryPath;
        _onStageCompleted = onStageCompleted;
        _onUnstageCompleted = onUnstageCompleted;

        _hasRepository = getHasRepository();

        hasRepositoryChanged
            .Subscribe(hasRepo =>
            {
                _hasRepository = hasRepo;
                UpdateVisibilityState();
            })
            .DisposeWith(_disposables);

        StageCommand = ReactiveCommand.CreateFromTask<FileChangeViewModel?>(StageAsync, hasRepositoryChanged);
        UnstageCommand = ReactiveCommand.CreateFromTask<FileChangeViewModel?>(UnstageAsync, hasRepositoryChanged);

        Changes.CollectionChanged += OnChangesCollectionChanged;
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

    public bool ShowList { get; private set; }

    public bool ShowEmptyRepository { get; private set; } = true;

    public bool ShowEmptyChanges { get; private set; }

    public ReactiveCommand<FileChangeViewModel?, Unit> StageCommand { get; }

    public ReactiveCommand<FileChangeViewModel?, Unit> UnstageCommand { get; }

    public void Dispose()
    {
        Changes.CollectionChanged -= OnChangesCollectionChanged;
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

        SelectedChange = selectionPath == null
            ? null
            : FindChangeByPath(selectionPath);
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

    private void OnChangesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisibilityState();
    }

    private void UpdateVisibilityState()
    {
        var previousShowList = ShowList;
        var previousShowEmptyRepository = ShowEmptyRepository;
        var previousShowEmptyChanges = ShowEmptyChanges;

        ShowList = _hasRepository && Changes.Count > 0;
        ShowEmptyRepository = !_hasRepository;
        ShowEmptyChanges = _hasRepository && Changes.Count == 0;

        if (previousShowList != ShowList)
            this.RaisePropertyChanged(nameof(ShowList));
        if (previousShowEmptyRepository != ShowEmptyRepository)
            this.RaisePropertyChanged(nameof(ShowEmptyRepository));
        if (previousShowEmptyChanges != ShowEmptyChanges)
            this.RaisePropertyChanged(nameof(ShowEmptyChanges));
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
        await _onStageCompleted();
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
        await _onUnstageCompleted();
    }
}
