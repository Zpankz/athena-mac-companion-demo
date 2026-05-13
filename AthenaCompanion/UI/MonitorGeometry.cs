using Avalonia;
using Avalonia.Controls;

namespace AthenaCompanion.UI;

internal readonly record struct TrackBounds(double MinX, double MaxX, double Top);

internal static class MonitorGeometry
{
    public static TrackBounds GetTrackBounds(Window window, double spriteWidth, double spriteHeight, double sidePadding, double bottomOffset)
    {
        var workingArea = GetPrimaryWorkingAreaDip(window);
        var minX = workingArea.Left + sidePadding;
        var maxX = Math.Max(minX, workingArea.Right - spriteWidth - sidePadding);
        var top = Math.Max(workingArea.Top, workingArea.Bottom - spriteHeight + bottomOffset);
        return new TrackBounds(minX, maxX, top);
    }

    public static Rect GetPrimaryWorkingAreaDip(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        var workingArea = screen?.WorkingArea ?? new PixelRect(0, 0, 1200, 800);
        return new Rect(workingArea.X, workingArea.Y, workingArea.Width, workingArea.Height);
    }
}
