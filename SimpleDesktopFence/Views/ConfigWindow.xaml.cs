using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SimpleDesktopFence.Models;
using SimpleDesktopFence.Services;
using SimpleDesktopFence.ViewModels;

namespace SimpleDesktopFence.Views;

public partial class ConfigWindow : Window
{
    private readonly FolderPanelWindow _panel;
    private readonly FolderPanelViewModel _vm;


    public ConfigWindow(FolderPanelWindow panel, FolderPanelViewModel vm)
    {
        InitializeComponent();
        _panel = panel;
        _vm = vm;

        DataContext = vm;

        // Keep the config window above the panel it belongs to
        Owner = panel;

        ToolbarManager.ModeChanged += OnToolbarModeChanged;
        UpdateAlwaysOnTopState();

        UpdateColorPreview();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FolderPanelViewModel.BackgroundColor)
                               or nameof(FolderPanelViewModel.BackgroundBrush))
                UpdateColorPreview();
        };
    }

    // ── Title bar ─────────────────────────────────────────────────────────

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        _panel.SaveSettings();
        Close();
    }

    // ── Folder ────────────────────────────────────────────────────────────

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select folder to monitor" };
        if (dlg.ShowDialog(this) == true)
            _vm.FolderPath = dlg.FolderName;
    }

    // ── Color picker ──────────────────────────────────────────────────────

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        // Windows Forms ColorDialog is the native Windows colour picker
        var dlg = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = ParseWinFormsColor(_vm.BackgroundColor)
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var c = dlg.Color;
            // Store as hex without alpha; opacity is handled separately
            _vm.BackgroundColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }

    private void OnToolbarModeChanged(ToolbarMode _) =>
    Dispatcher.Invoke(UpdateAlwaysOnTopState);

    private void UpdateAlwaysOnTopState()
    {
        bool enabled = ToolbarManager.CurrentMode != ToolbarMode.Mode2;
        AlwaysOnTopCheckBox.IsEnabled = enabled;
        AlwaysOnTopCheckBox.Opacity = enabled ? 1.0 : 0.45;
    }

    private void UpdateColorPreview()
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_vm.BackgroundColor);
            ColorPreviewBtn.Background = new SolidColorBrush(color);
        }
        catch { /* Ignore invalid color strings */ }
    }

    private void ResetDefault_Click(object sender, RoutedEventArgs e)
    {
        _vm.ResetToDefaults();
    }

    private static System.Drawing.Color ParseWinFormsColor(string hex)
    {
        try
        {
            var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        }
        catch
        {
            return System.Drawing.Color.FromArgb(26, 26, 46);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        ToolbarManager.ModeChanged -= OnToolbarModeChanged;
        base.OnClosed(e);
    }
}
