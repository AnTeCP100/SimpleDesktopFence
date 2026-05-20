using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using SimpleDesktopFence.Models;
using SimpleDesktopFence.Services;
using SimpleDesktopFence.Views;

namespace SimpleDesktopFence;

public partial class App : System.Windows.Application
{
    private NotifyIcon? _trayIcon;
    private ToolbarWindow? _toolbar;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ResourceMonitorService.Start();
        ToolbarManager.Load();

        SetupTrayIcon();
        PanelManager.LoadAll();

        _toolbar = new ToolbarWindow();
        _toolbar.Show();
    }

    // ¢w¢w Tray icon ¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w

    private void SetupTrayIcon()
    {
        var root = new ContextMenuStrip();

        // Add / Show All
        root.Items.Add("Add Panel", null, (_, _) => Dispatch(PanelManager.AddNewPanel));
        root.Items.Add("Show All Panels", null, (_, _) => Dispatch(PanelManager.ShowAllPanels));
        root.Items.Add(new ToolStripSeparator());

        // Toolbar mode submenu
        var modeMenu = new ToolStripMenuItem("Toolbar Mode");
        var m1 = new ToolStripMenuItem("Mode 1 ¡X Bring to front");
        var m2 = new ToolStripMenuItem("Mode 2 ¡X Lock positions");
        var m3 = new ToolStripMenuItem("Mode 3 ¡X Hide toolbar");
        m1.Click += (_, _) => Dispatch(() => ToolbarManager.SetMode(ToolbarMode.Mode1));
        m2.Click += (_, _) => Dispatch(() => ToolbarManager.SetMode(ToolbarMode.Mode2));
        m3.Click += (_, _) => Dispatch(() => ToolbarManager.SetMode(ToolbarMode.Mode3));
        modeMenu.DropDownItems.AddRange(new ToolStripItem[] { m1, m2, m3 });
        root.Items.Add(modeMenu);

        // Lock Toolbar Position
        var lockItem = new ToolStripMenuItem("Lock Toolbar Position");
        lockItem.Click += (_, _) =>
            Dispatch(() => ToolbarManager.SetPositionLocked(!ToolbarManager.IsPositionLocked));
        root.Items.Add(lockItem);

        var fontMenu = new ToolStripMenuItem("Toolbar Font Size");
        foreach (var (label, size) in new[] { ("Small", 11.0), ("Medium", 12.0), ("Large", 14.0) })
        {
            double capturedSize = size;
            var item = new ToolStripMenuItem(label);
            item.Click += (_, _) => Dispatch(() => ToolbarManager.SetFontSize(capturedSize));
            fontMenu.DropDownItems.Add(item);
        }
        root.Items.Add(fontMenu);

        root.Items.Add(new ToolStripSeparator());
        root.Items.Add("Resource Stats", null, (_, _) => Dispatch(OpenResourceStats));
        root.Items.Add(new ToolStripSeparator());
        root.Items.Add("Exit", null, (_, _) => Dispatch(ExitApp));

        // Sync checkmarks when menu opens
        root.Opening += (_, _) =>
        {
            m1.Checked = ToolbarManager.CurrentMode == ToolbarMode.Mode1;
            m2.Checked = ToolbarManager.CurrentMode == ToolbarMode.Mode2;
            m3.Checked = ToolbarManager.CurrentMode == ToolbarMode.Mode3;

            lockItem.Checked = ToolbarManager.IsPositionLocked;

            foreach (ToolStripMenuItem item in fontMenu.DropDownItems)
                item.Checked = item.Text switch
                {
                    "Small" => ToolbarManager.FontSize == 11,
                    "Large" => ToolbarManager.FontSize == 14,
                    _ => ToolbarManager.FontSize == 12
                };
        };

        _trayIcon = new NotifyIcon
        {
            Icon = new System.Drawing.Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App.ico")),
            Text = "SimpleDesktopFence",
            ContextMenuStrip = root,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatch(PanelManager.AddNewPanel);
    }

    // ¢w¢w Resource Stats ¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w

    private static ResourceStatsWindow? _statsWindow;

    private static void OpenResourceStats()
    {
        if (_statsWindow is { IsVisible: true }) { _statsWindow.Activate(); return; }
        _statsWindow = new ResourceStatsWindow();
        _statsWindow.Show();
    }



    // ¢w¢w Exit ¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w¢w

    private void ExitApp()
    {
        PanelManager.SaveAll();
        ToolbarManager.Save();
        ResourceMonitorService.Stop();
        _toolbar?.Close();

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        Shutdown();
    }

    // Marshal WinForms tray events back to the WPF UI thread
    private static void Dispatch(Action action) =>
        Current.Dispatcher.Invoke(action);

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        ResourceMonitorService.Stop();
        base.OnExit(e);
    }
}
