using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SimpleDesktopFence.Helpers;
using SimpleDesktopFence.Models;
using SimpleDesktopFence.Services;
using SimpleDesktopFence.ViewModels;

namespace SimpleDesktopFence.Views;

public partial class FolderPanelWindow : Window
{
    // ── Win32 ─────────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    // WM_NCHITTEST return values (used for Mode 2 resize restriction)
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTRIGHT = 11;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMRIGHT = 17;
    private const double ResizeEdge = 6.0;

    // ── State ─────────────────────────────────────────────────────────────

    private readonly FolderPanelViewModel _vm;
    private bool _isCollapsed = false;
    private double _expandedHeight = 380;
    private bool _isPermanentlyDeleted = false;

    private ConfigWindow? _configWindow;

    private readonly System.Collections.Generic.List<(string Key, GridViewColumn Col)>
        _columnOrder = new();

    // ── Public accessors ──────────────────────────────────────────────────

    public string FolderPath => _vm.FolderPath;
    public string PanelId => _vm.PanelId;

    public string ToolbarDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_vm.ToolbarLabel))
                return _vm.ToolbarLabel;
            return string.IsNullOrEmpty(_vm.FolderPath)
                ? "(no folder)"
                : System.IO.Path.GetFileName(
                      _vm.FolderPath.TrimEnd(System.IO.Path.DirectorySeparatorChar))
                  ?? _vm.FolderPath;
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────

    public FolderPanelWindow(PanelSettings settings)
    {
        InitializeComponent();
        _vm = new FolderPanelViewModel();


        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FolderPanelViewModel.FolderName)
                               or nameof(FolderPanelViewModel.ToolbarLabel))
                PanelManager.NotifyPanelsChanged();
        };

        DataContext = _vm;
        InitColumnTracking(settings);
        ApplySettings(settings);

        // React to toolbar mode changes (enables / disables Mode 2 restrictions)
        ToolbarManager.ModeChanged += OnToolbarModeChanged;
    }

    // ── Source initialised ────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);  // WindowChrome hooks itself here; call base first

        var hwnd = new WindowInteropHelper(this).Handle;

        // Remove from taskbar completely (ShowInTaskbar="False" alone is unreliable
        // with WindowStyle=None; WS_EX_TOOLWINDOW is the definitive solution)
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        // Hook WM_NCHITTEST so we can restrict resize directions in Mode 2
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    // ── WM_NCHITTEST: Mode 2 resize restriction ───────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_NCHITTEST) return IntPtr.Zero;
        if (ToolbarManager.CurrentMode != ToolbarMode.Mode2) return IntPtr.Zero;

        int sx = unchecked((short)(long)lParam);
        int sy = unchecked((short)((long)lParam >> 16));
        Point pt = PointFromScreen(new Point(sx, sy));

        double w = ActualWidth, h = ActualHeight;
        bool onL = pt.X <= ResizeEdge;
        bool onT = pt.Y <= ResizeEdge;
        bool onR = pt.X >= w - ResizeEdge;
        bool onB = pt.Y >= h - ResizeEdge;

        // Block all left and top edge resizes; let WindowChrome handle right/bottom
        if ((onL || onT) && !(onR || onB))
        {
            handled = true;
            return (IntPtr)HTCLIENT;
        }
        return IntPtr.Zero;
    }

    // ── Mode change handler ───────────────────────────────────────────────

    private void OnToolbarModeChanged(ToolbarMode _)
    {
        // No visual change needed; WndProc reads ToolbarManager.CurrentMode live
    }

    // ── Settings application ──────────────────────────────────────────────

    private void ApplySettings(PanelSettings s)
    {
        _vm.ApplySettings(s);

        double vL = SystemParameters.VirtualScreenLeft;
        double vT = SystemParameters.VirtualScreenTop;
        double vW = SystemParameters.VirtualScreenWidth;
        double vH = SystemParameters.VirtualScreenHeight;

        Left = Math.Clamp(s.Left, vL, vL + vW - 100);
        Top = Math.Clamp(s.Top, vT, vT + vH - 50);
        Width = Math.Max(s.Width, 280);
        Height = Math.Max(s.Height, 80);

        if (s.IsCollapsed) ToggleCollapse();
    }

    // ── Title bar ─────────────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleCollapse(); return; }

        // Mode 2: position is locked; dragging is disabled
        if (ToolbarManager.CurrentMode == ToolbarMode.Mode2) return;

        DragMove();
    }

    // ── Collapse ──────────────────────────────────────────────────────────

    private void CollapseBtn_Click(object sender, RoutedEventArgs e) => ToggleCollapse();

    private void ToggleCollapse()
    {
        if (_isCollapsed)
        {
            ContentRow.Height = new GridLength(1, GridUnitType.Star);
            MinHeight = 100;
            Height = _expandedHeight;
            CollapseBtn.Content = "—";
            CollapseBtn.ToolTip = "Collapse";
            _isCollapsed = false;
        }
        else
        {
            _expandedHeight = Height;
            ContentRow.Height = new GridLength(0);
            MinHeight = 0;
            Height = 46;
            CollapseBtn.Content = "▲";
            CollapseBtn.ToolTip = "Expand";
            _isCollapsed = true;
        }
    }

    // ── Close (HIDE — panel still exists, reappears on Show All Panels) ───

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Hide();         // Window is hidden but remains in PanelManager._panels
    }

    // ── Delete (PERMANENT — removes settings file and closes) ─────────────

    private void DeleteBtn_Click(object sender, RoutedEventArgs e) => DeleteAndClose();

    /// <summary>Called by title bar delete button and ResourceStatsWindow delete button.</summary>
    public void DeleteAndClose()
    {
        _isPermanentlyDeleted = true;
        PanelManager.Delete(_vm.PanelId);   // Removes from list + deletes JSON
        Close();
    }

    // ── Quick folder select ───────────────────────────────────────────────

    private void FolderSelect_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select folder to monitor" };
        if (dlg.ShowDialog(this) == true)
        {
            _vm.FolderPath = dlg.FolderName;
            SaveSettings();
        }
    }

    // ── Config window ─────────────────────────────────────────────────────

    private void ConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_configWindow is { IsVisible: true }) { _configWindow.Activate(); return; }
        _configWindow = new ConfigWindow(this, _vm);
        _configWindow.Closed += (_, _) => _configWindow = null;
        _configWindow.Left = Left + Width + 8;
        _configWindow.Top = Top;
        _configWindow.Show();
    }

    // ── Double-click open ─────────────────────────────────────────────────

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? hit = e.OriginalSource as DependencyObject;
        while (hit != null && hit is not ListViewItem)
            hit = VisualTreeHelper.GetParent(hit);
        if (hit is not ListViewItem) return;
        if (FileListView.SelectedItem is not FileItem item) return;
        OpenItem(item);
    }

    private void OpenItem(FileItem item)
    {
        try
        {
            if (item.IsDirectory)
                Process.Start("explorer.exe", item.FullPath);
            else
                Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Cannot open \"{item.Name}\"\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ── Right-click: native Shell context menu ────────────────────────────

    private void FileListView_PreviewRightDown(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? hit = e.OriginalSource as DependencyObject;
        while (hit != null && hit is not ListViewItem)
            hit = VisualTreeHelper.GetParent(hit);

        if (hit is ListViewItem lvi && !lvi.IsSelected)
        {
            FileListView.SelectedItems.Clear();
            lvi.IsSelected = true;
        }
    }

    private void FileListView_RightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var selected = FileListView.SelectedItems.Cast<FileItem>().ToArray();
        if (selected.Length == 0) return;

        var paths = selected.Select(i => i.FullPath).ToArray();
        var screenPt = PointToScreen(e.GetPosition(this));

        try { NativeContextMenu.Show(this, paths, screenPt); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Context menu error: {ex.Message}");
        }
    }

    // ── Column header sort ────────────────────────────────────────────────

    private void ColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader h) return;
        if (h.Role == GridViewColumnHeaderRole.Padding) return;

        string? key = h.Content?.ToString() switch
        {
            "Name" => "Name",
            "Size" => "Size",
            "Type" => "Type",
            "Date" => "Modified",
            _ => null
        };
        if (key != null) _vm.SortBy(key);
    }

    // ── Column tracking ───────────────────────────────────────────────────

    private void InitColumnTracking(PanelSettings settings)
    {
        var gv = (GridView)FileListView.View;
        foreach (var col in gv.Columns)
            _columnOrder.Add((col.Header?.ToString() ?? "", col));

        foreach (var (key, col) in _columnOrder)
        {
            col.Width = key switch
            {
                "Name" => settings.NameColumnWidth,
                "Size" => settings.SizeColumnWidth,
                "Type" => settings.TypeColumnWidth,
                "Date" => settings.DateColumnWidth,
                _ => col.Width
            };
        }

        _vm.PropertyChanged += (_, ev) =>
        {
            switch (ev.PropertyName)
            {
                case nameof(FolderPanelViewModel.ShowSize): SetColumnVisible("Size", _vm.ShowSize); break;
                case nameof(FolderPanelViewModel.ShowType): SetColumnVisible("Type", _vm.ShowType); break;
                case nameof(FolderPanelViewModel.ShowDate): SetColumnVisible("Date", _vm.ShowDate); break;
            }
        };

        SetColumnVisible("Size", _vm.ShowSize);
        SetColumnVisible("Type", _vm.ShowType);
        SetColumnVisible("Date", _vm.ShowDate);
    }

    private void SetColumnVisible(string key, bool visible)
    {
        var gv = (GridView)FileListView.View;
        var entry = _columnOrder.FirstOrDefault(c => c.Key == key);
        if (entry.Col == null) return;

        bool isIn = gv.Columns.Contains(entry.Col);
        if (visible && !isIn)
        {
            int insertAt = 0;
            foreach (var (k, col) in _columnOrder)
            {
                if (k == key) break;
                if (gv.Columns.Contains(col)) insertAt++;
            }
            gv.Columns.Insert(Math.Min(insertAt, gv.Columns.Count), entry.Col);
        }
        else if (!visible && isIn)
        {
            gv.Columns.Remove(entry.Col);
        }
    }

    // ── Settings I/O ──────────────────────────────────────────────────────

    public void SaveSettings()
    {
        try
        {
            var s = _vm.ToSettings(Left, Top, Width, Height, _isCollapsed, _expandedHeight);

            int idx = PanelManager.GetIndex(this);
            s.SortOrder = idx >= 0 ? idx : 0;

            foreach (var (key, col) in _columnOrder)
            {
                double w = double.IsNaN(col.Width) ? col.ActualWidth : col.Width;
                if (w <= 0) continue;
                switch (key)
                {
                    case "Name": s.NameColumnWidth = w; break;
                    case "Size": s.SizeColumnWidth = w; break;
                    case "Type": s.TypeColumnWidth = w; break;
                    case "Date": s.DateColumnWidth = w; break;
                }
            }
            PanelManager.Save(s);
        }
        catch { /* Silently ignore */ }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (ToolbarManager.CurrentMode != ToolbarMode.Mode2) return;

        // Don't hide if focus moved to this panel's config window
        var foreground = GetForegroundWindow();
        if (_configWindow != null)
        {
            var configHwnd = new WindowInteropHelper(_configWindow).Handle;
            if (foreground == configHwnd) return;
        }

        Dispatcher.InvokeAsync(() => Hide(),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        ToolbarManager.ModeChanged -= OnToolbarModeChanged;

        // Skip save when the panel was permanently deleted via the 🗑 button
        if (!_isPermanentlyDeleted)
            SaveSettings();

        _configWindow?.Close();
        _vm.Dispose();
        base.OnClosed(e);
    }
}
