using System;

namespace Branchy.UI.Services;

public interface IFileWatcherService : IDisposable
{
    event Action? Changed;
    void Watch(string path);
    void Stop();
}
