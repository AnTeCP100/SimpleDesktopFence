using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SimpleDesktopFence.Models;
using SimpleDesktopFence.Views;

namespace SimpleDesktopFence.Services;

public static class PanelManager
{
    public const int MaxPanels = 10;

    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimpleDesktopFence", "panels");

    private static readonly List<FolderPanelWindow> _panels = new();
    public static IReadOnlyList<FolderPanelWindow> Panels => _panels.AsReadOnly();

    // Fired whenever the panel list changes (add / delete).
    // The toolbar subscribes to rebuild its buttons.
    public static event Action? PanelsChanged;

    public static void NotifyPanelsChanged() => PanelsChanged?.Invoke();

    // ── Startup ───────────────────────────────────────────────────────────

    //Load All Panel
    public static void LoadAll()
    {
        Directory.CreateDirectory(SettingsDir);
        var files = Directory.GetFiles(SettingsDir, "panel_*.json");

        var settings = files
            .Select(f =>
            {
                try { return JsonSerializer.Deserialize<PanelSettings>(File.ReadAllText(f)); }
                catch { return null; }
            })
            .Where(s => s != null)
            .OrderBy(s => s!.SortOrder)   // restore original order
            .ToList();

        if (settings.Count == 0) { AddNewPanel(); return; }
        foreach (var s in settings) CreatePanel(s!);
    }

    // ── Panel lifecycle ───────────────────────────────────────────────────

    /// <summary>Creates a new panel with default settings.</summary>
    public static void AddNewPanel()
    {
        if (_panels.Count >= MaxPanels) return;
        CreatePanel(new PanelSettings
        {
            Left = 100 + _panels.Count * 30,
            Top = 100 + _panels.Count * 30,
            SortOrder = _panels.Count   // append to end
        });
    }

    //Show all Panel
    public static void ShowAllPanels()
    {
        foreach (var p in _panels)
        {
            if (!p.IsVisible) p.Show();
            p.Activate();
        }
    }

    //Get Panel Index
    public static int GetIndex(FolderPanelWindow panel) => _panels.IndexOf(panel);

    // ── Persistence ───────────────────────────────────────────────────────

    public static void SaveAll()
    {
        foreach (var p in _panels) p.SaveSettings();
    }

    public static string GetSettingsPath(string panelId) =>
        Path.Combine(SettingsDir, $"panel_{panelId}.json");

    public static void Save(PanelSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(
            GetSettingsPath(settings.PanelId),
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Permanently removes a panel: deletes the settings file, removes from the
    /// in-memory list, and raises PanelsChanged.
    /// The caller (FolderPanelWindow.DeleteAndClose) is responsible for calling Close().
    /// </summary>
    public static void Delete(string panelId)
    {
        var path = GetSettingsPath(panelId);
        if (File.Exists(path)) File.Delete(path);

        var panel = _panels.FirstOrDefault(p => p.PanelId == panelId);
        if (panel != null)
        {
            _panels.Remove(panel);
            PanelsChanged?.Invoke();
        }
    }

    // ── Private ───────────────────────────────────────────────────────────

    private static void CreatePanel(PanelSettings settings)
    {
        var window = new FolderPanelWindow(settings);
        _panels.Add(window);

        // If the panel is closed for reasons other than Hide/Delete (e.g. system
        // shutdown), ensure it is cleaned up from the list.
        window.Closed += (_, _) =>
        {
            if (_panels.Contains(window))
            {
                _panels.Remove(window);
                PanelsChanged?.Invoke();
            }
        };

        window.Show();
        PanelsChanged?.Invoke();
    }
}
