using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Branchy.UI.Services;
using Branchy.UI.ViewModels;
using Branchy.UI.Views;

namespace Branchy.UI;

public sealed class App : Avalonia.Application
{
    private MainWindowViewModel? _mainViewModel;
    private FileWatcherService? _fileWatcher;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var gitService = new GitCliService();
            var dialogService = new DialogService();
            _fileWatcher = new FileWatcherService();

            _mainViewModel = new MainWindowViewModel(gitService, dialogService, _fileWatcher);

            desktop.MainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };

            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _mainViewModel?.Dispose();
        _fileWatcher?.Dispose();
    }
}
