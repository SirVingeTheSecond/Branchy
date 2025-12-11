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

    public MainWindowViewModel()
        : this(null!, null!)
    {
    }

    public MainWindowViewModel(
        GetRepositoryStatusUseCase getStatusUseCase,
        IGitService gitService
    )
    {
        _getStatusUseCase = getStatusUseCase;
        _gitService = gitService;

        BrowseRepositoryCommand = ReactiveCommand.CreateFromTask(BrowseRepositoryAsync);
        ReloadStatusCommand = ReactiveCommand.CreateFromTask(ReloadStatusAsync);
        CommitCommand = ReactiveCommand.CreateFromTask(CommitAsync);
    }

    public string RepositoryPath
    {
        get => _repositoryPath;
        set => this.RaiseAndSetIfChanged(ref _repositoryPath, value);
    }

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

            if (value == null)
            {
                DiffText = string.Empty;
                return;
            }

            _ = LoadDiffAsync(value);
        }
    }

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

    private async Task ReloadStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath))
        {
            return;
        }

        var status = await _getStatusUseCase.ExecuteAsync(RepositoryPath);

        BranchDisplay = $"{status.Branch.Name} ↑{status.Branch.AheadBy} ↓{status.Branch.BehindBy}";

        Changes.Clear();
        foreach (var change in status.Changes)
        {
            Changes.Add(new FileChangeViewModel(change));
        }

        DiffText = string.Empty;
        CommitMessage = string.Empty;
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
        await ReloadStatusAsync();
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

public sealed class FileChangeViewModel
{
    public FileChangeViewModel(FileChange change)
    {
        Path = change.Path;
        Kind = change.Kind.ToString();
        IsStaged = change.IsStaged;
    }

    public string Path { get; }

    public string Kind { get; }

    public bool IsStaged { get; }
}

