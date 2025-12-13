using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Branchy.UI.Services;

public sealed class DialogService : IDialogService
{
    public async Task<string?> ShowFolderPickerAsync()
    {
        var window = GetCurrentWindow();
        if (window == null)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel == null)
        {
            return null;
        }

        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = false
        };

        var results = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        var folder = results.FirstOrDefault();

        return folder?.TryGetLocalPath();
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
