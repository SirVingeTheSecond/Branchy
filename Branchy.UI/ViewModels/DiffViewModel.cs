using System;
using System.Threading;
using System.Threading.Tasks;
using Branchy.Application.Git;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class DiffViewModel : ReactiveObject, IDisposable
{
    private readonly Func<string> _getRepositoryPath;
    private readonly IGitService _gitService;
    private readonly Action<Exception> _onError;

    private string _diffText = string.Empty;
    private bool _hasSelection;
    private CancellationTokenSource? _loadCancellation;

    public DiffViewModel(
        IGitService gitService,
        Func<string> getRepositoryPath,
        Action<Exception> onError
    )
    {
        _gitService = gitService;
        _getRepositoryPath = getRepositoryPath;
        _onError = onError;
    }

    public string DiffText
    {
        get => _diffText;
        private set => this.RaiseAndSetIfChanged(ref _diffText, value);
    }

    public bool ShowDiff => _hasSelection;

    public bool ShowEmptyDiff => !_hasSelection;

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
    }

    public void Load(FileChangeViewModel? change)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();

        if (change == null)
        {
            SetSelectionState(false);
            DiffText = string.Empty;
            return;
        }

        SetSelectionState(true);

        _loadCancellation = new CancellationTokenSource();
        _ = LoadAsync(change, _loadCancellation.Token);
    }

    public void Clear()
    {
        _loadCancellation?.Cancel();
        DiffText = string.Empty;
        SetSelectionState(false);
    }

    private void SetSelectionState(bool hasSelection)
    {
        if (_hasSelection == hasSelection)
        {
            return;
        }

        _hasSelection = hasSelection;
        this.RaisePropertyChanged(nameof(ShowDiff));
        this.RaisePropertyChanged(nameof(ShowEmptyDiff));
    }

    private async Task LoadAsync(FileChangeViewModel change, CancellationToken cancellationToken)
    {
        var repoPath = _getRepositoryPath();
        if (string.IsNullOrWhiteSpace(repoPath))
        {
            return;
        }

        try
        {
            var diff = await _gitService.GetDiffAsync(
                repoPath,
                change.Path,
                change.IsStaged,
                cancellationToken
            );

            if (!cancellationToken.IsCancellationRequested)
            {
                DiffText = diff;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when user switches selection quickly
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _onError(ex);
                DiffText = string.Empty;
            }
        }
    }
}
