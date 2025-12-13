using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Branchy.Application.Repositories;
using Branchy.Infrastructure.GitCli;
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
            var getStatusUseCase = new GetRepositoryStatusUseCase(gitService);

            _mainViewModel = new MainWindowViewModel(getStatusUseCase, gitService, dialogService);

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
