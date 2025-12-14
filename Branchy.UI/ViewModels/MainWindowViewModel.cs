using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Branchy.UI.Models;
using Branchy.UI.Services;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private const int ErrorDisplayMs = 3000;
    private const int ProgressUpdateMs = 100;

    private readonly IGitService _gitService;
    private readonly IDialogService _dialogService;
    private readonly IFileWatcherService _fileWatcher;
    private readonly CompositeDisposable _disposables = new();
    private readonly BehaviorSubject<bool> _hasRepositorySubject = new(false);

    private string _repositoryPath = string.Empty;
    private string _branchDisplay = string.Empty;
    private string? _errorMessage;
    private double _errorDismissProgress = 100;
    private CancellationTokenSource? _errorDismissCts;

    private readonly ObservableAsPropertyHelper<bool> _isBusy;

    public MainWindowViewModel(
        IGitService gitService,
        IDialogService dialogService,
        IFileWatcherService fileWatcher
    )
    {
        _gitService = gitService;
        _dialogService = dialogService;
        _fileWatcher = fileWatcher;

        var hasRepositoryObservable = _hasRepositorySubject.AsObservable();

        Changes = new ChangesViewModel(
            gitService,
            () => RepositoryPath,
            () => HasRepository,
            hasRepositoryObservable,
            () => ReloadAfterOperationAsync()
        );

        Diff = new DiffViewModel(
            gitService,
            () => RepositoryPath,
            ex => ShowError(ex)
        );

        Commit = new CommitViewModel(
            gitService,
            () => RepositoryPath,
            hasRepositoryObservable,
            () => ReloadAfterCommitAsync()
        );

        Branches = new BranchesViewModel(
            gitService,
            () => RepositoryPath,
            () => HasRepository,
            hasRepositoryObservable,
            () => ReloadAfterCheckoutAsync()
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
                Branches.CheckoutCommand.IsExecuting,
                (browse, reload, commit, stage, unstage, checkout) => 
                    browse || reload || commit || stage || unstage || checkout)
            .ToProperty(this, x => x.IsBusy)
            .DisposeWith(_disposables);

        Observable.Merge(
                BrowseRepositoryCommand.ThrownExceptions,
                ReloadStatusCommand.ThrownExceptions,
                Commit.CommitCommand.ThrownExceptions,
                Changes.StageCommand.ThrownExceptions,
                Changes.UnstageCommand.ThrownExceptions,
                Branches.CheckoutCommand.ThrownExceptions)
            .Subscribe(async ex =>
            {
                ShowError(ex);
                await ReloadStatusAsync(null, false);
            })
            .DisposeWith(_disposables);

        Changes.WhenAnyValue(x => x.SelectedChange)
            .Subscribe(change => Diff.Load(change))
            .DisposeWith(_disposables);

        _fileWatcher.Changed += OnFileSystemChanged;
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

            if (hasRepo)
            {
                _fileWatcher.Watch(value);
            }
            else
            {
                _fileWatcher.Stop();
            }
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

            if (value != null)
            {
                ScheduleErrorDismiss();
            }
            else
            {
                CancelErrorDismiss();
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public double ErrorDismissProgress
    {
        get => _errorDismissProgress;
        private set => this.RaiseAndSetIfChanged(ref _errorDismissProgress, value);
    }

    public string BranchDisplay
    {
        get => _branchDisplay;
        private set => this.RaiseAndSetIfChanged(ref _branchDisplay, value);
    }

    public ChangesViewModel Changes { get; }
    public BranchesViewModel Branches { get; }
    public DiffViewModel Diff { get; }
    public CommitViewModel Commit { get; }

    public ReactiveCommand<Unit, Unit> BrowseRepositoryCommand { get; }
    public ReactiveCommand<Unit, Unit> ReloadStatusCommand { get; }
    public ReactiveCommand<Unit, string?> DismissErrorCommand { get; }

    public void Dispose()
    {
        CancelErrorDismiss();
        _fileWatcher.Changed -= OnFileSystemChanged;
        _fileWatcher.Stop();
        Changes.Dispose();
        Branches.Dispose();
        Diff.Dispose();
        Commit.Dispose();
        _hasRepositorySubject.Dispose();
        _disposables.Dispose();
    }

    private void OnFileSystemChanged()
    {
        if (!HasRepository || IsBusy)
        {
            return;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            await ReloadStatusAsync(Changes.SelectedChange?.Path, false);
        });
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

        try
        {
            var status = await _gitService.GetStatusAsync(RepositoryPath);
            var branches = await _gitService.GetBranchesAsync(RepositoryPath);

            BranchDisplay = FormatBranchDisplay(status.Branch);
            Changes.Update(status, preservedSelectionPath);
            Branches.Update(branches);
            Diff.Clear();

            if (resetCommitMessage)
            {
                Commit.Clear();
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private Task ReloadAfterOperationAsync()
    {
        var selectedPath = Changes.SelectedChange?.Path;
        return ReloadStatusAsync(selectedPath, false);
    }

    private Task ReloadAfterCommitAsync()
    {
        return ReloadStatusAsync(null, true);
    }

    private Task ReloadAfterCheckoutAsync()
    {
        return ReloadStatusAsync(null, true);
    }

    private void ShowError(Exception ex)
    {
        var message = FormatErrorMessage(ex);
        if (message != null)
        {
            ErrorMessage = message;
        }
    }

    private static string FormatBranchDisplay(BranchStatus branch)
    {
        return $"{branch.Name} ↑{branch.AheadBy} ↓{branch.BehindBy}";
    }

    private static string? FormatErrorMessage(Exception ex)
    {
        // Internal errors - suppress silently
        if (ex is OperationCanceledException or ObjectDisposedException)
        {
            return null;
        }

        // Service layer already provides user-friendly messages
        if (ex is InvalidOperationException)
        {
            return ex.Message;
        }

        return $"An unexpected error occurred: {ex.Message}";
    }

    private async void ScheduleErrorDismiss()
    {
        CancelErrorDismiss();
        ErrorDismissProgress = 100;

        _errorDismissCts = new CancellationTokenSource();
        var token = _errorDismissCts.Token;
        var steps = ErrorDisplayMs / ProgressUpdateMs;
        var decrementPerStep = 100.0 / steps;

        try
        {
            for (var i = 0; i < steps && !token.IsCancellationRequested; i++)
            {
                await Task.Delay(ProgressUpdateMs, token);
                ErrorDismissProgress -= decrementPerStep;
            }

            if (!token.IsCancellationRequested)
            {
                ErrorMessage = null;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when dismissed manually or new error shown
        }
    }

    private void CancelErrorDismiss()
    {
        _errorDismissCts?.Cancel();
        _errorDismissCts?.Dispose();
        _errorDismissCts = null;
        ErrorDismissProgress = 100;
    }
}
