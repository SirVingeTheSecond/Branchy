using System;
using System.IO;
using System.Threading;

namespace Branchy.UI.Services;

public sealed class FileWatcherService : IFileWatcherService
{
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly object _lock = new();
    private bool _pendingChange;

    private const int DebounceMilliseconds = 300;

    public event Action? Changed;

    public void Watch(string path)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        _watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileSystemEvent;
        _watcher.Created += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnFileSystemEvent;

        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileSystemEvent;
            _watcher.Created -= OnFileSystemEvent;
            _watcher.Deleted -= OnFileSystemEvent;
            _watcher.Renamed -= OnFileSystemEvent;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        // Ignore .git directory internal changes (lock files etc.)
        if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}") 
            && !e.FullPath.EndsWith("HEAD") 
            && !e.FullPath.EndsWith("index"))
        {
            return;
        }

        lock (_lock)
        {
            _pendingChange = true;
            _debounceTimer?.Change(DebounceMilliseconds, Timeout.Infinite);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        bool shouldNotify;
        lock (_lock)
        {
            shouldNotify = _pendingChange;
            _pendingChange = false;
        }

        if (shouldNotify)
        {
            Changed?.Invoke();
        }
    }
}
