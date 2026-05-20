using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SimpleDesktopFence.Helpers;
using SimpleDesktopFence.Models;

namespace SimpleDesktopFence.ViewModels;

public class FolderPanelViewModel : INotifyPropertyChanged, IDisposable
{
    // ── File list ────────────────────────────────────────────────────────

    private string _folderPath = string.Empty;
    private FileSystemWatcher? _watcher;
    private readonly ObservableCollection<FileItem> _items = new();
    private string _sortColumn = "Name";
    private bool _sortAscending = true;
    private string _toolbarLabel = string.Empty;

    public ICollectionView ItemsView { get; }

    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (_folderPath == value) return;
            _folderPath = value;
            OnPropertyChanged(nameof(FolderPath));
            OnPropertyChanged(nameof(FolderName));
            OnPropertyChanged(nameof(HasNoFolder));
            LoadFolder();
            SetupWatcher();
        }
    }

    public string FolderName =>
        string.IsNullOrEmpty(_folderPath)
            ? "No folder selected"
            : Path.GetFileName(_folderPath.TrimEnd(Path.DirectorySeparatorChar)) ?? _folderPath;

    public bool HasNoFolder => string.IsNullOrEmpty(_folderPath);

    // ── Settings ─────────────────────────────────────────────────────────

    private string _panelId = Guid.NewGuid().ToString("N")[..8];
    private string _backgroundColor = "#1A1A2E";
    private double _backgroundOpacity = 0.80;
    private bool _showSize = true;
    private bool _showType = true;
    private bool _showDate = true;
    private bool _showExtension = true;
    private bool _alwaysOnTop = false;
    private double _fontSize = 12;
    public double WindowOpacity => _backgroundOpacity;

    public string PanelId
    {
        get => _panelId;
        set { _panelId = value; OnPropertyChanged(nameof(PanelId)); }
    }

    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            _backgroundColor = value;
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(BackgroundBrush));
        }
    }

    public double BackgroundOpacity
    {
        get => _backgroundOpacity;
        set
        {
            _backgroundOpacity = Math.Clamp(value, 0.25, 1.0);
            OnPropertyChanged(nameof(BackgroundOpacity));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(OpacityPercent));
            OnPropertyChanged(nameof(WindowOpacity));
        }
    }

    // Display label for the opacity slider, e.g. "80%"
    public string OpacityPercent => $"{_backgroundOpacity * 100:F0}%";

    public double FontSize
    {
        get => _fontSize;
        set { _fontSize = Math.Clamp(value, 8, 22); OnPropertyChanged(nameof(FontSize)); }
    }

    public bool ShowSize
    {
        get => _showSize;
        set
        {
            _showSize = value;
            OnPropertyChanged(nameof(ShowSize));
            OnPropertyChanged(nameof(SizeColumnWidth));
        }
    }

    public bool ShowType
    {
        get => _showType;
        set
        {
            _showType = value;
            OnPropertyChanged(nameof(ShowType));
            OnPropertyChanged(nameof(TypeColumnWidth));
        }
    }

    public bool ShowDate
    {
        get => _showDate;
        set
        {
            _showDate = value;
            OnPropertyChanged(nameof(ShowDate));
            OnPropertyChanged(nameof(DateColumnWidth));
        }
    }

    public bool ShowExtension
    {
        get => _showExtension;
        set { _showExtension = value; OnPropertyChanged(nameof(ShowExtension)); }
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set { _alwaysOnTop = value; OnPropertyChanged(nameof(AlwaysOnTop)); }
    }



    public string ToolbarLabel
    {
        get => _toolbarLabel;
        set { _toolbarLabel = value; OnPropertyChanged(nameof(ToolbarLabel)); }
    }

    // ── Computed bindings ─────────────────────────────────────────────────

    // Background brush combining color + opacity, bound to the panel Border
    public SolidColorBrush BackgroundBrush
    {
        get
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_backgroundColor);
                color.A = (byte)Math.Round(_backgroundOpacity * 255);
                return new SolidColorBrush(color);
            }
            catch
            {
                // Fallback if color string is malformed
                return new SolidColorBrush(System.Windows.Media.Color.FromArgb(204, 26, 26, 46));
            }
        }
    }

    // Column widths: 0 hides the column entirely in WPF GridView
    public double SizeColumnWidth => _showSize ? 80 : 0;
    public double TypeColumnWidth => _showType ? 100 : 0;
    public double DateColumnWidth => _showDate ? 140 : 0;

    // ── Constructor ───────────────────────────────────────────────────────

    public FolderPanelViewModel()
    {
        ItemsView = CollectionViewSource.GetDefaultView(_items);
        ApplySort();
    }

    // ── File list loading ─────────────────────────────────────────────────

    public void LoadFolder()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _items.Clear();
            if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath))
                return;

            try
            {
                var dirs = Directory.GetDirectories(_folderPath).Select(d => BuildItem(d, true));
                var files = Directory.GetFiles(_folderPath).Select(f => BuildItem(f, false));
                foreach (var item in dirs.Concat(files))
                    _items.Add(item);
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        });
    }

    private static FileItem BuildItem(string path, bool isDirectory)
    {
        FileSystemInfo info = isDirectory ? new DirectoryInfo(path) : new FileInfo(path);
        long size = (!isDirectory && info is FileInfo fi) ? fi.Length : 0;
        string displaySize = size > 0 ? FormatSize(size) : string.Empty;

        return new FileItem
        {
            Name = info.Name,
            FullPath = path,
            IsDirectory = isDirectory,
            ModifiedDate = info.LastWriteTime,
            SizeBytes = size,
            DisplaySize = displaySize,
            FileType = ShellHelper.GetFileTypeName(path, isDirectory),
            Icon = ShellHelper.GetFileIcon(path, isDirectory)
        };
    }

    // ── Sorting ───────────────────────────────────────────────────────────

    public void SortBy(string column)
    {
        _sortAscending = (_sortColumn == column) ? !_sortAscending : true;
        _sortColumn = column;
        ApplySort();
    }

    private void ApplySort()
    {
        var dir = _sortAscending
            ? ListSortDirection.Ascending
            : ListSortDirection.Descending;

        string prop = _sortColumn switch
        {
            "Size" => "SizeBytes",
            "Modified" => "ModifiedDate",
            "Type" => "FileType",
            _ => "Name"
        };

        ItemsView.SortDescriptions.Clear();
        ItemsView.SortDescriptions.Add(new SortDescription("IsDirectory", ListSortDirection.Descending));
        ItemsView.SortDescriptions.Add(new SortDescription(prop, dir));
    }

    // ── FileSystemWatcher ─────────────────────────────────────────────────

    private void SetupWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
        if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath)) return;

        _watcher = new FileSystemWatcher(_folderPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, _) => LoadFolder();
        _watcher.Deleted += (_, _) => LoadFolder();
        _watcher.Renamed += (_, _) => LoadFolder();
        _watcher.Changed += (_, _) => LoadFolder();
    }

    // ── Settings serialisation helpers ────────────────────────────────────

    public PanelSettings ToSettings(double left, double top, double width,
                                     double height, bool collapsed, double expandedHeight) =>
        new()
        {
            PanelId = _panelId,
            FolderPath = _folderPath,
            Left = left,
            Top = top,
            Width = width,
            Height = collapsed ? expandedHeight : height,
            IsCollapsed = collapsed,
            BackgroundColor = _backgroundColor,
            BackgroundOpacity = _backgroundOpacity,
            FontSize = _fontSize,
            ShowSize = _showSize,
            ShowType = _showType,
            ShowDate = _showDate,
            ShowExtension = _showExtension,
            AlwaysOnTop = _alwaysOnTop,
            ToolbarLabel = _toolbarLabel,
        };

    public void ApplySettings(PanelSettings s)
    {
        _panelId = s.PanelId;
        _backgroundColor = s.BackgroundColor;
        _backgroundOpacity = s.BackgroundOpacity;
        _fontSize = s.FontSize;
        _showSize = s.ShowSize;
        _showType = s.ShowType;
        _showDate = s.ShowDate;
        _showExtension = s.ShowExtension;
        _alwaysOnTop = s.AlwaysOnTop;
        _toolbarLabel = s.ToolbarLabel;

        // Notify all computed properties at once
        OnPropertyChanged(nameof(BackgroundColor));
        OnPropertyChanged(nameof(BackgroundBrush));
        OnPropertyChanged(nameof(BackgroundOpacity));
        OnPropertyChanged(nameof(OpacityPercent));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(ShowSize));
        OnPropertyChanged(nameof(SizeColumnWidth));
        OnPropertyChanged(nameof(ShowType));
        OnPropertyChanged(nameof(TypeColumnWidth));
        OnPropertyChanged(nameof(ShowDate));
        OnPropertyChanged(nameof(DateColumnWidth));
        OnPropertyChanged(nameof(ShowExtension));
        OnPropertyChanged(nameof(AlwaysOnTop));
        OnPropertyChanged(nameof(ToolbarLabel));

        if (!string.IsNullOrEmpty(s.FolderPath))
            FolderPath = s.FolderPath;
    }

    public void ResetToDefaults()
    {
        BackgroundColor = "#1A1A2E";
        BackgroundOpacity = 0.80;
        FontSize = 12;
        ShowSize = true;
        ShowType = true;
        ShowDate = true;
        ShowExtension = true;
        AlwaysOnTop = false;
        ToolbarLabel = string.Empty;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024L => $"{bytes} B",
        < 1024L * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    // ── INotifyPropertyChanged ────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── IDisposable ───────────────────────────────────────────────────────

    public void Dispose() { _watcher?.Dispose(); _watcher = null; }
}
