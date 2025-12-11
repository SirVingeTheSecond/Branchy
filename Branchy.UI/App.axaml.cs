using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Branchy.Application.Repositories;
using Branchy.Infrastructure.GitCli;
using Branchy.UI.ViewModels;
using Branchy.UI.Views;

namespace Branchy.UI;

public sealed class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var gitService = new GitCliService();
            var getStatusUseCase = new GetRepositoryStatusUseCase(gitService);

            var vm = new MainWindowViewModel(getStatusUseCase, gitService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
