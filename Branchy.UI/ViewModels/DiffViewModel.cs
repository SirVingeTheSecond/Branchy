using System;
using System.Threading;
using System.Threading.Tasks;
using Branchy.UI.Services;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class DiffViewModel : ReactiveObject, IDisposable
{
    private readonly Func<string> _getRepositoryPath;
    private readonly IGitService _gitService;
    private readonly Action<Exception> _onError;

    private string _diffText = string.Empty;
    private bool _showContent;
    private bool _showEmptySelection = true;
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

    public bool ShowContent
    {
        get => _showContent;
        private set => this.RaiseAndSetIfChanged(ref _showContent, value);
    }

    public bool ShowEmptySelection
    {
        get => _showEmptySelection;
        private set => this.RaiseAndSetIfChanged(ref _showEmptySelection, value);
    }

    public void Dispose()
    {
        CancelPendingLoad();
    }

    public void Load(FileChangeViewModel? change)
    {
        CancelPendingLoad();

        if (change == null)
        {
            UpdateVisibilityState(false);
            DiffText = string.Empty;
            return;
        }

        UpdateVisibilityState(true);

        _loadCancellation = new CancellationTokenSource();
        _ = LoadAsync(change, _loadCancellation.Token);
    }

    public void Clear()
    {
        CancelPendingLoad();
        DiffText = string.Empty;
        UpdateVisibilityState(false);
    }

    private void CancelPendingLoad()
    {
        var cts = _loadCancellation;
        _loadCancellation = null;

        if (cts == null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        cts.Dispose();
    }

    private void UpdateVisibilityState(bool hasSelection)
    {
        ShowContent = hasSelection;
        ShowEmptySelection = !hasSelection;
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

            if (!IsCancelled(cancellationToken))
            {
                DiffText = diff;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when user switches selection quickly
        }
        catch (ObjectDisposedException)
        {
            // Expected when cancellation source was disposed
        }
        catch (Exception ex)
        {
            if (!IsCancelled(cancellationToken))
            {
                _onError(ex);
                DiffText = string.Empty;
            }
        }
    }

    private static bool IsCancelled(CancellationToken token)
    {
        try
        {
            return token.IsCancellationRequested;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }
}
