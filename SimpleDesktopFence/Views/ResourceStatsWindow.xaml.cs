using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimpleDesktopFence.Services;

namespace SimpleDesktopFence.Views;

public partial class ResourceStatsWindow : Window
{
    private const int MaxPoints = 60;

    private readonly System.Windows.Threading.DispatcherTimer _uptimeTimer = new();
    private readonly System.Windows.Threading.DispatcherTimer _panelTimer = new();

    public ResourceStatsWindow()
    {
        InitializeComponent();

        ResourceMonitorService.SampleAdded += OnSampleAdded;

        // Uptime: tick every second
        _uptimeTimer.Interval = TimeSpan.FromSeconds(1);
        _uptimeTimer.Tick += (_, _) => RefreshUptime();
        _uptimeTimer.Start();

        // Panel list: refresh every 5 seconds (handles panels added/removed while open)
        _panelTimer.Interval = TimeSpan.FromSeconds(5);
        _panelTimer.Tick += (_, _) => RefreshPanelList();
        _panelTimer.Start();

        if (ResourceMonitorService.Samples.Count > 0)
            RefreshCharts();

        RefreshPanelList();
    }

    // ── Title bar ─────────────────────────────────────────────────────────

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    // ── Charts ────────────────────────────────────────────────────────────

    private void OnSampleAdded(ResourceSample sample) =>
        Dispatcher.Invoke(RefreshCharts);

    private void RefreshCharts()
    {
        var samples = ResourceMonitorService.Samples.TakeLast(MaxPoints).ToList();
        if (samples.Count == 0) return;

        var latest = samples[^1];
        CpuValueText.Text = $"{latest.CpuPercent:F1} %";
        RamValueText.Text = $"{latest.RamMb:F1} MB";

        CpuCanvas.UpdateLayout();
        RamCanvas.UpdateLayout();

        DrawChart(CpuPolyline, CpuCanvas,
                  samples.Select(s => s.CpuPercent).ToList(), 0, 100);

        double maxRam = Math.Max(samples.Max(s => s.RamMb) * 1.2, 10);
        RamChartLabel.Text = $"RAM (MB)  — peak {samples.Max(s => s.RamMb):F1} MB";
        DrawChart(RamPolyline, RamCanvas,
                  samples.Select(s => s.RamMb).ToList(), 0, maxRam);
    }

    private static void DrawChart(System.Windows.Shapes.Polyline polyline,
                                  Canvas canvas,
                                  System.Collections.Generic.List<double> values,
                                  double yMin, double yMax)
    {
        double w = canvas.ActualWidth, h = canvas.ActualHeight;
        if (w <= 0 || h <= 0 || values.Count < 2) return;

        double range = Math.Max(yMax - yMin, 1);
        var points = new PointCollection(values.Count);

        for (int i = 0; i < values.Count; i++)
        {
            double x = (double)i / (values.Count - 1) * w;
            double y = Math.Clamp(h - (values[i] - yMin) / range * h, 0, h);
            points.Add(new Point(x, y));
        }
        polyline.Points = points;
    }

    private void RefreshUptime()
    {
        UptimeText.Text = (DateTime.Now - ResourceMonitorService.SessionStart).ToString(@"hh\:mm\:ss");
    }

    // ── Panel list ────────────────────────────────────────────────────────

    private void RefreshPanelList()
    {
        var panels = PanelManager.Panels;
        PanelCountText.Text = $"{panels.Count} / {PanelManager.MaxPanels}";
        PanelListPanel.Children.Clear();

        if (panels.Count == 0)
        {
            PanelListPanel.Children.Add(new TextBlock
            {
                Text = "No active panels",
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            });
            return;
        }

        for (int i = 0; i < panels.Count; i++)
        {
            string path = string.IsNullOrEmpty(panels[i].FolderPath)
                ? "(No folder selected)"
                : panels[i].FolderPath;

            // Row: index number + path
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            var idxText = new TextBlock
            {
                Text = $"{i + 1}.",
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(idxText, 0);

            var pathText = new TextBlock
            {
                Text = path,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = path   // Show full path on hover
            };
            Grid.SetColumn(pathText, 1);

            int capturedIndex = i; // capture loop variable for closure
            var deleteBtn = new Button
            {
                Content = "🗑",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x77, 0x77)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Delete this panel permanently",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            deleteBtn.Click += (_, _) =>
            {
                panels[capturedIndex].DeleteAndClose();
                RefreshPanelList();
            };
            Grid.SetColumn(deleteBtn, 2);

            row.Children.Add(idxText);
            row.Children.Add(pathText);
            row.Children.Add(deleteBtn);
            PanelListPanel.Children.Add(row);
        }
    }

    // ── Cleanup ───────────────────────────────────────────────────────────

    protected override void OnClosed(EventArgs e)
    {
        _uptimeTimer.Stop();
        _panelTimer.Stop();
        ResourceMonitorService.SampleAdded -= OnSampleAdded;
        base.OnClosed(e);
    }
}
