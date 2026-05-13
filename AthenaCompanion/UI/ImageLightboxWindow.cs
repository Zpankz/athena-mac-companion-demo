using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace AthenaCompanion.UI;

public sealed class ImageLightboxWindow : Window
{
    private readonly string _imagePath;
    private readonly Image _generatedImage = new() { Stretch = Stretch.Uniform, Margin = new Thickness(12) };
    private readonly TextBlock _pathText = new()
    {
        Foreground = Brush("#c7c0d9"),
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        MaxWidth = 560
    };

    public ImageLightboxWindow(string imagePath)
    {
        _imagePath = imagePath;
        Title = "Athena Image";
        Width = 900;
        Height = 680;
        MinWidth = 520;
        MinHeight = 420;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildContent();
        LoadImage();
    }

    private Control BuildContent()
    {
        var root = new Grid { Background = Brush("#17151f") };
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var imageBorder = new Border
        {
            Margin = new Thickness(16),
            Background = Brush("#201d2b"),
            BorderBrush = Brush("#3f3855"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = _generatedImage
        };
        root.Children.Add(imageBorder);

        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(16, 0, 16, 16) };
        DockPanel.SetDock(_pathText, Dock.Left);
        footer.Children.Add(_pathText);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(buttons, Dock.Right);
        buttons.Children.Add(Button("Copy Path", 96, OnCopy));
        buttons.Children.Add(Button("Open Folder", 104, OnOpenFolder));
        buttons.Children.Add(Button("Close", 84, (_, _) => Close()));
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);

        return root;
    }

    private void LoadImage()
    {
        if (File.Exists(_imagePath))
        {
            _generatedImage.Source = new Bitmap(_imagePath);
        }

        _pathText.Text = _imagePath;
    }

    private async void OnCopy(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var clipboard = Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(_imagePath);
        }
    }

    private void OnOpenFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!File.Exists(_imagePath))
        {
            return;
        }

        var startInfo = OperatingSystem.IsMacOS()
            ? new ProcessStartInfo("/usr/bin/open", $"-R \"{_imagePath}\"") { UseShellExecute = false }
            : new ProcessStartInfo(Path.GetDirectoryName(_imagePath) ?? ".") { UseShellExecute = true };
        Process.Start(startInfo);
    }

    private static Button Button(string text, double width, EventHandler<Avalonia.Interactivity.RoutedEventArgs> handler)
    {
        var button = new Button
        {
            Content = text,
            Width = width,
            Margin = new Thickness(0, 0, 8, 0)
        };
        button.Click += handler;
        return button;
    }

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
