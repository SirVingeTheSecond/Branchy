using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Branchy.UI.Services;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class CommitViewModel : ReactiveObject, IDisposable
{
    private readonly Func<string> _getRepositoryPath;
    private readonly IGitService _gitService;
    private readonly Func<Task> _onOperationCompleted;

    private string _commitMessage = string.Empty;

    public CommitViewModel(
        IGitService gitService,
        Func<string> getRepositoryPath,
        IObservable<bool> hasRepositoryChanged,
        Func<Task> onOperationCompleted
    )
    {
        _gitService = gitService;
        _getRepositoryPath = getRepositoryPath;
        _onOperationCompleted = onOperationCompleted;

        var canCommit = hasRepositoryChanged
            .CombineLatest(
                this.WhenAnyValue(x => x.CommitMessage),
                (hasRepo, message) => hasRepo && !string.IsNullOrWhiteSpace(message));

        CommitCommand = ReactiveCommand.CreateFromTask(CommitAsync, canCommit);
    }

    public string CommitMessage
    {
        get => _commitMessage;
        set => this.RaiseAndSetIfChanged(ref _commitMessage, value);
    }

    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

    public void Dispose()
    {
        CommitCommand.Dispose();
    }

    public void Clear()
    {
        CommitMessage = string.Empty;
    }

    private async Task CommitAsync()
    {
        var repoPath = _getRepositoryPath();
        if (string.IsNullOrWhiteSpace(repoPath) || string.IsNullOrWhiteSpace(CommitMessage))
        {
            return;
        }

        await _gitService.CommitAsync(repoPath, CommitMessage);
        await _onOperationCompleted();
    }
}
