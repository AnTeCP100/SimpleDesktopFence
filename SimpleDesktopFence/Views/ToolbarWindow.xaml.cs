using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SimpleDesktopFence.Models;
using SimpleDesktopFence.Services;

namespace SimpleDesktopFence.Views;

public partial class ToolbarWindow : Window
{
    // ── Win32 ─────────────────────────────────────────────────────────────

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    // ── Geometry constants ────────────────────────────────────────────────

    private const double TotalHeight = 44;   // BarHeight + TabHeight
    private const double BarHeight = 36;
    private const double TabHeight = 8;
    private const double EdgeTriggerPx = 4;    // logical-px zone that triggers expand
    private const int CollapseDelay = 800;  // ms before toolbar collapses after mouse leaves

    private bool _isDragging = false;
    private double _dragStartLeft = 0;
    private System.Windows.Point _dragAnchorScreen;

    // ── State ─────────────────────────────────────────────────────────────

    private bool _isExpanded = false;
    private int _collapseCountdown = 0;        // decremented each timer tick

    private readonly System.Windows.Threading.DispatcherTimer _mouseTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(100)
    };

    private void ToolbarBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ToolbarManager.IsPositionLocked) return;
        if (e.ClickCount > 1) return;

        _dragStartLeft = Left;
        _dragAnchorScreen = ToLogicalScreen(e.GetPosition(this));
        _isDragging = true;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void ToolbarBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = ToLogicalScreen(e.GetPosition(this));
        double delta = current.X - _dragAnchorScreen.X;
        double newLeft = _dragStartLeft + delta;

        double screenW = SystemParameters.PrimaryScreenWidth;
        Left = Math.Clamp(newLeft, 0, Math.Max(0, screenW - ActualWidth));
    }

    private void ToolbarBar_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        ToolbarManager.SetPositionLeft(Left);   // persist position
        e.Handled = true;
    }

    // Convert a point relative to this window into logical screen coordinates
    private System.Windows.Point ToLogicalScreen(System.Windows.Point clientPt)
    {
        var physPt = PointToScreen(clientPt);
        var src = PresentationSource.FromVisual(this);
        return src?.CompositionTarget?.TransformFromDevice.Transform(physPt) ?? physPt;
    }

    // ── Constructor ───────────────────────────────────────────────────────

    public ToolbarWindow()
    {
        InitializeComponent();

        _mouseTimer.Tick += OnMouseTimerTick;

        ToolbarManager.ModeChanged += OnModeChanged;
        ToolbarManager.PositionLockChanged += locked =>
            Dispatcher.Invoke(() =>
                ToolbarBorder.Cursor = locked
                ? System.Windows.Input.Cursors.Arrow
                : System.Windows.Input.Cursors.SizeAll);
        ToolbarManager.FontSizeChanged += size => Dispatcher.Invoke(() =>
        {
            foreach (Button btn in ButtonsPanel.Children)
                btn.FontSize = size;
        });


        PanelManager.PanelsChanged += RefreshButtons;

        Loaded += OnLoaded;
    }

    // ── Source initialised ────────────────────────────────────────────────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, GWL_EXSTYLE,
            GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);

        ApplyPosition();
        ApplyMode(ToolbarManager.CurrentMode);
        RefreshButtons();
    }

    // ── Layout ────────────────────────────────────────────────────────────

    private void ApplyPosition()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        bool isTop = ToolbarManager.CurrentPosition == ToolbarPosition.Top;

        if (isTop)
        {
            // Layout: [Bar (36px)][Tab (8px)]
            Grid.SetRow(ToolbarBorder, 0);
            Grid.SetRow(TabStrip, 1);
            BarRow.Height = new GridLength(BarHeight);
            TabRow.Height = new GridLength(TabHeight);
            ToolbarBorder.BorderThickness = new Thickness(0, 0, 0, 1);
            TabStrip.BorderThickness = new Thickness(0, 1, 0, 0);
            TabChevron.Text = "▼";
        }
        else
        {
            // Layout: [Tab (8px)][Bar (36px)]
            Grid.SetRow(TabStrip, 0);
            Grid.SetRow(ToolbarBorder, 1);
            BarRow.Height = new GridLength(TabHeight);
            TabRow.Height = new GridLength(BarHeight);
            ToolbarBorder.BorderThickness = new Thickness(0, 1, 0, 0);
            TabStrip.BorderThickness = new Thickness(0, 0, 0, 1);
            TabChevron.Text = "▲";
        }

        // Start in collapsed state (only tab visible)
        Top = CollapsedTop;
    }

    private double ExpandedTop => ToolbarManager.CurrentPosition == ToolbarPosition.Top
        ? 0
        : SystemParameters.PrimaryScreenHeight - TotalHeight;

    private double CollapsedTop => ToolbarManager.CurrentPosition == ToolbarPosition.Top
        ? -(BarHeight)      // slide bar off screen; tab stays visible at y=0
        : SystemParameters.PrimaryScreenHeight - TabHeight;

    private double HiddenTop => ToolbarManager.CurrentPosition == ToolbarPosition.Top
        ? -TotalHeight      // completely off screen for Mode 3
        : SystemParameters.PrimaryScreenHeight;

    // ── Mode ──────────────────────────────────────────────────────────────

    private void ApplyMode(ToolbarMode mode)
    {
        if (mode == ToolbarMode.Mode3)
        {
            _mouseTimer.Stop();
            AnimateTo(HiddenTop);   // slide completely off screen
        }
        else
        {
            AnimateTo(CollapsedTop);
            _mouseTimer.Start();
        }
    }

    private void OnModeChanged(ToolbarMode mode)
    {
        ApplyMode(mode);

        if (mode == ToolbarMode.Mode2)
        {
            Expand();
            Dispatcher.InvokeAsync(() =>
            {
                SnapAllPanelsToButtons();
                // Mode 2: panels are hidden until the user clicks a toolbar button
                foreach (var p in PanelManager.Panels)
                    p.Hide();
            }, System.Windows.Threading.DispatcherPriority.Render);
        }
        else
        {
            // Leaving Mode 2: restore all panels to visible
            foreach (var p in PanelManager.Panels)
                if (!p.IsVisible) p.Show();
        }
    }

    private void OnPositionChanged(ToolbarPosition _)
    {
        ApplyPosition();
        ApplyMode(ToolbarManager.CurrentMode);
    }

    private void SnapPanelToButton(Button btn, FolderPanelWindow panel)
    {
        var physPt = btn.PointToScreen(new Point(0, 0));
        var src = PresentationSource.FromVisual(this);
        var logPt = src?.CompositionTarget?.TransformFromDevice.Transform(physPt) ?? physPt;

        bool isTop = ToolbarManager.CurrentPosition == ToolbarPosition.Top;

        double left = logPt.X;
        double top = isTop
            ? ExpandedTop + TotalHeight
            : ExpandedTop - panel.ActualHeight;

        // Clamp so panel stays on screen (issue 3 fix)
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;
        panel.Left = Math.Clamp(left, 0, Math.Max(0, screenW - panel.ActualWidth));
        panel.Top = Math.Clamp(top, 0, Math.Max(0, screenH - panel.ActualHeight));
    }

    private void SnapAllPanelsToButtons()
    {
        var panels = PanelManager.Panels;
        for (int i = 0; i < ButtonsPanel.Children.Count && i < panels.Count; i++)
        {
            if (ButtonsPanel.Children[i] is Button btn)
                SnapPanelToButton(btn, panels[i]);
        }
    }

    // ── Auto-hide: mouse tracking ─────────────────────────────────────────

    private void OnMouseTimerTick(object? sender, EventArgs e)
    {
        GetCursorPos(out POINT raw);
        Point logical = LogicalFromPhysical(raw.X, raw.Y);

        bool nearEdge = IsNearEdge(logical);
        bool overToolbar = IsOverToolbar(logical);

        if (nearEdge || overToolbar)
        {
            _collapseCountdown = 0;
            if (!_isExpanded) Expand();
        }
        else if (_isExpanded)
        {
            // Delay before collapsing so small mouse movements don't flicker
            _collapseCountdown++;
            if (_collapseCountdown * 100 >= CollapseDelay)
            {
                _collapseCountdown = 0;
                Collapse();
            }
        }
    }

    private const double EdgeBufferX = 24.0;  // px buffer beyond each side of toolbar

    private bool IsNearEdge(Point logical)
    {
        // Y: must be within the top edge trigger zone
        if (logical.Y > EdgeTriggerPx) return false;

        // X: must be within the toolbar's horizontal extent + small buffer
        bool inRange = logical.X >= Left - EdgeBufferX
                    && logical.X <= Left + ActualWidth + EdgeBufferX;
        return inRange;
    }

    private bool IsOverToolbar(Point logical) =>
        _isExpanded
        && logical.X >= Left && logical.X <= Left + Width
        && logical.Y >= Top && logical.Y <= Top + TotalHeight;

    // Convert physical cursor coordinates to WPF logical units
    private Point LogicalFromPhysical(int px, int py)
    {
        var src = PresentationSource.FromVisual(this);
        if (src?.CompositionTarget == null) return new Point(px, py);
        return src.CompositionTarget.TransformFromDevice.Transform(new Point(px, py));
    }

    // ── Animation ─────────────────────────────────────────────────────────

    private void Expand()
    {
        _isExpanded = true;
        TabChevron.Text = ToolbarManager.CurrentPosition == ToolbarPosition.Top ? "▲" : "▼";
        AnimateTo(ExpandedTop);
    }

    private void Collapse()
    {
        _isExpanded = false;
        TabChevron.Text = ToolbarManager.CurrentPosition == ToolbarPosition.Top ? "▼" : "▲";
        AnimateTo(CollapsedTop);
    }

    private void AnimateTo(double targetTop)
    {
        var anim = new DoubleAnimation(targetTop, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(TopProperty, anim);
    }

    // ── Panel buttons ─────────────────────────────────────────────────────

    private void RefreshButtons()
    {
        Dispatcher.Invoke(() =>
        {
            ButtonsPanel.Children.Clear();
            foreach (var panel in PanelManager.Panels)
            {
                string label = panel.ToolbarDisplayName;

                var btn = new Button
                {
                    Style = (Style)Resources["PanelBtn"],
                    Content = label,
                    ToolTip = string.IsNullOrEmpty(panel.FolderPath) ? null : panel.FolderPath,
                    Tag = panel
                };
                btn.Click += PanelButton_Click;
                ButtonsPanel.Children.Add(btn);
            }

            ApplySavedLeft();
        });
    }

    private void PanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not FolderPanelWindow panel) return;

        if (ToolbarManager.CurrentMode == ToolbarMode.Mode2)
        {
            SnapPanelToButton(btn, panel);   // recalculate position
            panel.Show();
            panel.Activate();
        }
        else
        {
            if (!panel.IsVisible) panel.Show();
            panel.Activate();
            panel.Topmost = true;
            panel.Topmost = _vmAlwaysOnTop(panel);
        }

        _collapseCountdown = CollapseDelay / 100;
    }

    // Read the panel's AlwaysOnTop setting through its DataContext
    private static bool _vmAlwaysOnTop(FolderPanelWindow panel) =>
        panel.DataContext is ViewModels.FolderPanelViewModel vm && vm.AlwaysOnTop;

    //Load
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        ApplySavedLeft();

        // Apply saved horizontal position (ActualWidth is now known after layout)
        double screenW = SystemParameters.PrimaryScreenWidth;
        Left = Math.Clamp(ToolbarManager.CurrentPositionLeft, 0,
                   Math.Max(0, screenW - ActualWidth));

        // Update cursor based on lock state
        ToolbarBorder.Cursor = ToolbarManager.IsPositionLocked
            ? System.Windows.Input.Cursors.Arrow
            : System.Windows.Input.Cursors.SizeAll;

        Dispatcher.InvokeAsync(() =>
        {
            SnapAllPanelsToButtons();
            foreach (var p in PanelManager.Panels) p.Hide();
        }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void ApplySavedLeft()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        Left = Math.Clamp(ToolbarManager.CurrentPositionLeft, 0,
                   Math.Max(0, screenW - ActualWidth));
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _mouseTimer.Stop();
        ToolbarManager.ModeChanged -= OnModeChanged;
        ToolbarManager.PositionChanged -= OnPositionChanged;
        PanelManager.PanelsChanged -= RefreshButtons;
        base.OnClosed(e);
    }
}
