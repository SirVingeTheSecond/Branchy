using System.Threading.Tasks;

namespace Branchy.UI.Services;

public interface IDialogService
{
    Task<string?> ShowFolderPickerAsync();
}
