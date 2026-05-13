using Avalonia.Controls;

namespace AthenaCompanion.UI;

internal static class IconLoader
{
    public static WindowIcon? LoadWindowIcon()
    {
        foreach (var relative in new[]
        {
            Path.Combine("Assets", "Icons", "athena-icon.png"),
            Path.Combine("Assets", "Icons", "puppy-icon.png"),
            Path.Combine("Assets", "Icons", "athena.ico")
        })
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, relative);
            if (File.Exists(iconPath))
            {
                return new WindowIcon(iconPath);
            }
        }

        return null;
    }
}
