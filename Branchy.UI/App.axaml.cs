using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Branchy.UI.Services;
using Branchy.UI.ViewModels;
using Branchy.UI.Views;

namespace Branchy.UI;

public sealed class App : Avalonia.Application
{
    private MainWindowViewModel? _mainViewModel;

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

            _mainViewModel = new MainWindowViewModel(gitService, dialogService);

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
    }
}
