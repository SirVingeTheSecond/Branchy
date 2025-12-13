using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Branchy.Application.Git;
using Branchy.Application.Repositories;
using Branchy.Domain.Models;
using Branchy.UI.Services;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly GetRepositoryStatusUseCase _getStatusUseCase;
    private readonly IGitService _gitService;
    private readonly IDialogService _dialogService;
    private readonly CompositeDisposable _disposables = new();
    private readonly BehaviorSubject<bool> _hasRepositorySubject = new(false);

    private string _repositoryPath = string.Empty;
    private string _branchDisplay = string.Empty;
    private string? _errorMessage;

    private readonly ObservableAsPropertyHelper<bool> _isBusy;

    public MainWindowViewModel(
        GetRepositoryStatusUseCase getStatusUseCase,
        IGitService gitService,
        IDialogService dialogService
    )
    {
        _getStatusUseCase = getStatusUseCase;
        _gitService = gitService;
        _dialogService = dialogService;

        var hasRepositoryObservable = _hasRepositorySubject.AsObservable();

        Changes = new ChangesViewModel(
            gitService,
            () => RepositoryPath,
            () => HasRepository,
            hasRepositoryObservable,
            () => ReloadAfterStageAsync(),
            () => ReloadAfterUnstageAsync()
        );

        Diff = new DiffViewModel(
            gitService,
            () => RepositoryPath,
            ex => ErrorMessage = FormatErrorMessage(ex)
        );

        Commit = new CommitViewModel(
            gitService,
            () => RepositoryPath,
            hasRepositoryObservable,
            () => ReloadAfterCommitAsync()
        );

        BrowseRepositoryCommand = ReactiveCommand.CreateFromTask(BrowseRepositoryAsync);
        ReloadStatusCommand = ReactiveCommand.CreateFromTask(
            () => ReloadStatusAsync(null, true),
            hasRepositoryObservable
        );
        DismissErrorCommand = ReactiveCommand.Create(() => ErrorMessage = null);

        _isBusy = Observable.CombineLatest(
                BrowseRepositoryCommand.IsExecuting,
                ReloadStatusCommand.IsExecuting,
                Commit.CommitCommand.IsExecuting,
                Changes.StageCommand.IsExecuting,
                Changes.UnstageCommand.IsExecuting,
                (browse, reload, commit, stage, unstage) => browse || reload || commit || stage || unstage)
            .ToProperty(this, x => x.IsBusy)
            .DisposeWith(_disposables);

        Observable.Merge(
                BrowseRepositoryCommand.ThrownExceptions,
                ReloadStatusCommand.ThrownExceptions,
                Commit.CommitCommand.ThrownExceptions,
                Changes.StageCommand.ThrownExceptions,
                Changes.UnstageCommand.ThrownExceptions)
            .Subscribe(ex => ErrorMessage = FormatErrorMessage(ex))
            .DisposeWith(_disposables);

        Changes.WhenAnyValue(x => x.SelectedChange)
            .Subscribe(change => Diff.Load(change))
            .DisposeWith(_disposables);
    }

    public string RepositoryPath
    {
        get => _repositoryPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _repositoryPath, value);
            var hasRepo = !string.IsNullOrWhiteSpace(value);
            _hasRepositorySubject.OnNext(hasRepo);
            this.RaisePropertyChanged(nameof(HasRepository));
        }
    }

    public bool HasRepository => !string.IsNullOrWhiteSpace(RepositoryPath);

    public bool IsBusy => _isBusy.Value;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _errorMessage, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string BranchDisplay
    {
        get => _branchDisplay;
        private set => this.RaiseAndSetIfChanged(ref _branchDisplay, value);
    }

    public ChangesViewModel Changes { get; }

    public DiffViewModel Diff { get; }

    public CommitViewModel Commit { get; }

    public ReactiveCommand<Unit, Unit> BrowseRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> ReloadStatusCommand { get; }

    public ReactiveCommand<Unit, string?> DismissErrorCommand { get; }

    public void Dispose()
    {
        Changes.Dispose();
        Diff.Dispose();
        Commit.Dispose();
        _hasRepositorySubject.Dispose();
        _disposables.Dispose();
    }

    private async Task BrowseRepositoryAsync()
    {
        var localPath = await _dialogService.ShowFolderPickerAsync();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        var isRepo = await _gitService.IsRepositoryAsync(localPath);
        if (!isRepo)
        {
            ErrorMessage = "The selected folder is not a Git repository.";
            return;
        }

        ErrorMessage = null;
        RepositoryPath = localPath;
        await ReloadStatusAsync(null, true);
    }

    private async Task ReloadStatusAsync(string? preservedSelectionPath, bool resetCommitMessage)
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath))
        {
            return;
        }

        var status = await _getStatusUseCase.ExecuteAsync(RepositoryPath);

        BranchDisplay = FormatBranchDisplay(status.Branch);
        Changes.Update(status, preservedSelectionPath);
        Diff.Clear();

        if (resetCommitMessage)
        {
            Commit.Clear();
        }
    }

    private Task ReloadAfterStageAsync()
    {
        var selectedPath = Changes.SelectedChange?.Path;
        return ReloadStatusAsync(selectedPath, false);
    }

    private Task ReloadAfterUnstageAsync()
    {
        var selectedPath = Changes.SelectedChange?.Path;
        return ReloadStatusAsync(selectedPath, false);
    }

    private Task ReloadAfterCommitAsync()
    {
        return ReloadStatusAsync(null, true);
    }

    private static string FormatBranchDisplay(BranchStatus branch)
    {
        return $"{branch.Name} ↑{branch.AheadBy} ↓{branch.BehindBy}";
    }

    private static string FormatErrorMessage(Exception ex)
    {
        return ex switch
        {
            InvalidOperationException ioe when ioe.Message.Contains("Git") => ioe.Message,
            _ => $"An error occurred: {ex.Message}"
        };
    }
}
