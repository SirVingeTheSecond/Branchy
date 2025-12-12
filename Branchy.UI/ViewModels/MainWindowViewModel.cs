using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Branchy.Application.Git;
using Branchy.Application.Repositories;
using Branchy.Domain.Models;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class MainWindowViewModel : ReactiveObject
{
    private readonly GetRepositoryStatusUseCase _getStatusUseCase;
    private readonly IGitService _gitService;

    private string _repositoryPath = string.Empty;
    private string _branchDisplay = string.Empty;
    private FileChangeViewModel? _selectedChange;
    private string _diffText = string.Empty;
    private string _commitMessage = string.Empty;


    public MainWindowViewModel(
        GetRepositoryStatusUseCase getStatusUseCase,
        IGitService gitService
    )
    {
        _getStatusUseCase = getStatusUseCase;
        _gitService = gitService;

        var hasRepository = this
            .WhenAnyValue(x => x.RepositoryPath, path => !string.IsNullOrWhiteSpace(path));

        var canCommit = this.WhenAnyValue(
            x => x.RepositoryPath,
            x => x.CommitMessage,
            (path, message) => !string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(message)
        );

        BrowseRepositoryCommand = ReactiveCommand.CreateFromTask(BrowseRepositoryAsync);
        ReloadStatusCommand = ReactiveCommand.CreateFromTask(ReloadStatusAsync);
        CommitCommand = ReactiveCommand.CreateFromTask(CommitAsync);
        ReloadStatusCommand = ReactiveCommand.CreateFromTask(ReloadStatusAsync, hasRepository);
        StageChangeCommand = ReactiveCommand.CreateFromTask<FileChangeViewModel?>(StageChangeAsync, hasRepository);
        UnstageChangeCommand = ReactiveCommand.CreateFromTask<FileChangeViewModel?>(UnstageChangeAsync, hasRepository);
        CommitCommand = ReactiveCommand.CreateFromTask(CommitAsync, canCommit);
    }

    public string RepositoryPath
    {
        get => _repositoryPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _repositoryPath, value);
            this.RaisePropertyChanged(nameof(HasRepository));
        }
    }
    
    public bool HasRepository => !string.IsNullOrWhiteSpace(RepositoryPath);

    public string BranchDisplay
    {
        get => _branchDisplay;
        private set => this.RaiseAndSetIfChanged(ref _branchDisplay, value);
    }

    public ObservableCollection<FileChangeViewModel> Changes { get; } = new();

    public FileChangeViewModel? SelectedChange
    {
        get => _selectedChange;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedChange, value);
            this.RaisePropertyChanged(nameof(HasSelection));

            if (value == null)
            {
                DiffText = string.Empty;
                return;
            }

            _ = LoadDiffAsync(value);
        }
    }
    
    public bool HasSelection => SelectedChange != null;

    public string DiffText
    {
        get => _diffText;
        private set => this.RaiseAndSetIfChanged(ref _diffText, value);
    }

    public string CommitMessage
    {
        get => _commitMessage;
        set => this.RaiseAndSetIfChanged(ref _commitMessage, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseRepositoryCommand { get; }

    public ReactiveCommand<Unit, Unit> ReloadStatusCommand { get; }

    public ReactiveCommand<FileChangeViewModel?, Unit> StageChangeCommand { get; }

    public ReactiveCommand<FileChangeViewModel?, Unit> UnstageChangeCommand { get; }

    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

    private async Task BrowseRepositoryAsync()
    {
        var window = GetCurrentWindow();
        if (window == null)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel == null)
        {
            return;
        }

        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = false
        };

        var results = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        var folder = results.FirstOrDefault();
        if (folder == null)
        {
            return;
        }

        var localPath = folder.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        RepositoryPath = localPath;
        await ReloadStatusAsync();
    }
    
    private Task ReloadStatusAsync()
    {
        return ReloadStatusAsync(null, true);
    }

    private async Task ReloadStatusAsync(string? preservedSelectionPath, bool resetCommitMessage)
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath))
        {
            return;
        }

        var status = await _getStatusUseCase.ExecuteAsync(RepositoryPath);

        BranchDisplay = $"{status.Branch.Name} ↑{status.Branch.AheadBy} ↓{status.Branch.BehindBy}";

        var selectionPath = preservedSelectionPath ?? SelectedChange?.Path;
        Changes.Clear();
        foreach (var change in status.Changes)
        {
            Changes.Add(new FileChangeViewModel(change));
        }

        DiffText = string.Empty;
        CommitMessage = string.Empty;
        SelectedChange = selectionPath == null
            ? null
            : Changes.FirstOrDefault(x => x.Path == selectionPath);

        if (SelectedChange == null)
        {
            DiffText = string.Empty;
        }

        if (resetCommitMessage)
        {
            CommitMessage = string.Empty;
        }
    }

    private async Task CommitAsync()
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            return;
        }

        await _gitService.CommitAsync(RepositoryPath, CommitMessage);
        await ReloadStatusAsync(null, true);
    }
    
    private async Task StageChangeAsync(FileChangeViewModel? change)
    {
        if (change == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RepositoryPath))
        {
            return;
        }

        await _gitService.StageFileAsync(RepositoryPath, change.Path);
        change.UpdateStagedState(true);

        await ReloadStatusAsync(change.Path, false);
    }

    private async Task UnstageChangeAsync(FileChangeViewModel? change)
    {
        if (change == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RepositoryPath))
        {
            return;
        }

        await _gitService.UnstageFileAsync(RepositoryPath, change.Path);
        change.UpdateStagedState(false);

        await ReloadStatusAsync(change.Path, false);
    }

    private async Task LoadDiffAsync(FileChangeViewModel change)
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath))
        {
            return;
        }

        var diff = await _gitService.GetDiffAsync(
            RepositoryPath,
            change.Path,
            change.IsStaged
        );

        DiffText = diff;
    }

    private static Window? GetCurrentWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}

public sealed class FileChangeViewModel : ReactiveObject
{
    private bool _isStaged;

    public FileChangeViewModel(FileChange change)
    {
        Path = change.Path;
        Kind = change.Kind.ToString();
        IsStaged = change.IsStaged;
        _isStaged = change.IsStaged;
    }

    public string Path { get; }

    public string Kind { get; }

    public bool IsStaged
    {
        get => _isStaged;
        private set => this.RaiseAndSetIfChanged(ref _isStaged, value);
    }

    public void UpdateStagedState(bool isStaged)
    {
        IsStaged = isStaged;
    }
}
