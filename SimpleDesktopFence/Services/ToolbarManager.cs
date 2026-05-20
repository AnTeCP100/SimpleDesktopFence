using System;
using System.IO;
using System.Text.Json;
using SimpleDesktopFence.Models;

namespace SimpleDesktopFence.Services;

/// <summary>
/// Central service for toolbar state.  All components that need to react to
/// mode or position changes subscribe to ModeChanged / PositionChanged.
/// </summary>
public static class ToolbarManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimpleDesktopFence", "toolbar.json");

    // ── Public state ──────────────────────────────────────────────────────

    public static ToolbarMode CurrentMode { get; private set; } = ToolbarMode.Mode1;
    public static ToolbarPosition CurrentPosition { get; private set; } = ToolbarPosition.Top;
    public static string BackgroundColor { get; private set; } = "#1A1A2E";
    public static double BackgroundOpacity { get; private set; } = 0.92;

    public static double FontSize { get; private set; } = 12;

    public static double CurrentPositionLeft { get; private set; } = 0;
    public static bool IsPositionLocked { get; private set; } = false;

    // ── Events ────────────────────────────────────────────────────────────

    public static event Action<ToolbarMode>? ModeChanged;
    public static event Action<ToolbarPosition>? PositionChanged;
    public static event Action<bool>? PositionLockChanged;

    // ── Mutators ──────────────────────────────────────────────────────────

    public static void SetMode(ToolbarMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        Save();
        ModeChanged?.Invoke(mode);
    }

    public static void SetPosition(ToolbarPosition pos)
    {
        if (CurrentPosition == pos) return;
        CurrentPosition = pos;
        Save();
        PositionChanged?.Invoke(pos);
    }

    public static void SetFontSize(double size)
    {
        FontSize = Math.Clamp(size, 8, 22);
        Save();
        FontSizeChanged?.Invoke(FontSize);
    }
    public static event Action<double>? FontSizeChanged;

    public static void SetPositionLeft(double left)
    {
        CurrentPositionLeft = left;
        Save();
    }

    public static void SetPositionLocked(bool locked)
    {
        IsPositionLocked = locked;
        Save();
        PositionLockChanged?.Invoke(locked);
    }

    // ── Persistence ───────────────────────────────────────────────────────

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var s = JsonSerializer.Deserialize<ToolbarSettings>(
                File.ReadAllText(SettingsPath));
            if (s == null) return;
            CurrentMode = s.Mode;
            CurrentPosition = s.Position;
            BackgroundColor = s.BackgroundColor;
            BackgroundOpacity = s.BackgroundOpacity;
            CurrentPositionLeft = s.PositionLeft;
            IsPositionLocked = s.IsPositionLocked;
            FontSize = s.FontSize;
        }
        catch { /* Use defaults on error */ }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var s = new ToolbarSettings
            {
                Mode = CurrentMode,
                Position = CurrentPosition,
                BackgroundColor = BackgroundColor,
                BackgroundOpacity = BackgroundOpacity,
                PositionLeft = CurrentPositionLeft,  
                IsPositionLocked = IsPositionLocked,   
                FontSize = FontSize
            };
            File.WriteAllText(SettingsPath,
                JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* Silently ignore */ }
    }
}
