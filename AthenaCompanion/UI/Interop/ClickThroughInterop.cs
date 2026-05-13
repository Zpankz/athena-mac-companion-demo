using Avalonia.Controls;

namespace AthenaCompanion.UI.Interop;

internal static class ClickThroughInterop
{
    public static void Apply(Window window, bool enabled)
    {
        window.IsHitTestVisible = !enabled;
    }
}
