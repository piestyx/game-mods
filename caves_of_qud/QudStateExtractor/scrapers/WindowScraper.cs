// Mods/QudStateExtractor/scrapers/WindowScraper.cs

/// <summary> 
/// Hooks in to identify the current active UI window from the `Modern UI` path
/// Then used to export data for screen exporters
/// </summary>

using UnityEngine;

namespace QudStateExtractor.Scrapers
{
    public static class WindowScraper
    {
        public static UnityEngine.Component GetCurrentWindow()
        {
            try { return Qud.UI.UIManager.instance?.currentWindow as UnityEngine.Component; }
            catch { return null; }
        }
    }
}