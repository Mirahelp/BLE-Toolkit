using Avalonia.Controls;
using System;

namespace MirahelpBLEToolkit.Presentation
{
    public static class ContextMenuManager
    {
        private static readonly Object Sync = new();
        private static ContextMenu? Current;

        public static void Show(ContextMenu menu, Control placementTarget)
        {
            lock (Sync)
            {
                ContextMenu? existing = Current;
                if (existing != null)
                {
                    try { existing.Close(); } catch { }
                    Current = null;
                }
                Current = menu;
            }
            menu.PlacementTarget = placementTarget;
            menu.Open(placementTarget);
        }
    }
}
