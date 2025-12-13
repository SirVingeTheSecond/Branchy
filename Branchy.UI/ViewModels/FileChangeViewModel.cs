using Branchy.Domain.Models;
using ReactiveUI;

namespace Branchy.UI.ViewModels;

public sealed class FileChangeViewModel : ReactiveObject
{
    private bool _isStaged;

    public FileChangeViewModel(FileChange change)
    {
        Path = change.Path;
        Kind = change.Kind.ToString();
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
