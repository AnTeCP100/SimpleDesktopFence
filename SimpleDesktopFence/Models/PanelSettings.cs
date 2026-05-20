namespace SimpleDesktopFence.Models;

/// <summary>
/// JSON
/// </summary>
public class PanelSettings
{
    // --- Identity ---
    public string PanelId { get; set; } = Guid.NewGuid().ToString("N")[..8];

    // --- Position & size ---
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 500;
    public double Height { get; set; } = 380;
    public bool IsCollapsed { get; set; } = false;

    // --- Content ---
    public string FolderPath { get; set; } = string.Empty;

    // --- Appearance ---
    // Hex color string without alpha, e.g. "#1A1A2E"
    public string BackgroundColor { get; set; } = "#1A1A2E";
    // 0.0 (invisible) to 1.0 (opaque)
    public double BackgroundOpacity { get; set; } = 0.80;
    // "Small" = 11, "Medium" = 12, "Large" = 14
    public double FontSize { get; set; } = 12;

    // --- Column visibility ---
    public bool ShowSize { get; set; } = true;
    public bool ShowType { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public bool ShowExtension { get; set; } = true;

    // --- Behavior ---
    public bool AlwaysOnTop { get; set; } = false;

    // Column widths (persisted so user adjustments survive restarts)
    public double NameColumnWidth { get; set; } = 200;
    public double SizeColumnWidth { get; set; } = 80;
    public double TypeColumnWidth { get; set; } = 100;
    public double DateColumnWidth { get; set; } = 140;

    public string ToolbarLabel { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
}
