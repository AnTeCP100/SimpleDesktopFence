namespace SimpleDesktopFence.Models;

public enum ToolbarMode
{
    Mode1,  // toolbar visible; click button → bring panel to front
    Mode2,  // toolbar visible; panel position locked (no drag, right/bottom resize only)
    Mode3   // toolbar hidden; panels behave like Mode 1
}

public enum ToolbarPosition { Top, Bottom }

public class ToolbarSettings
{

    public ToolbarMode Mode { get; set; } = ToolbarMode.Mode1;
    public ToolbarPosition Position { get; set; } = ToolbarPosition.Top;
    public string BackgroundColor { get; set; } = "#1A1A2E";
    public double BackgroundOpacity { get; set; } = 0.92;

    public double FontSize { get; set; } = 12;

    public double PositionLeft { get; set; } = 0;     // horizontal position
    public bool IsPositionLocked { get; set; } = false;
}
