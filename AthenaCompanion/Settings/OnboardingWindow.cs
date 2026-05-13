using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AthenaCompanion.Settings;

internal sealed class OnboardingWindow : Window
{
    private readonly string _musicDirectory;
    private readonly Func<Window, Task> _configureApiKey;
    private readonly TextBlock _stepText = TextBlock("#bdb4d4", 12);
    private readonly TextBlock _titleText = TextBlock("#f7f1ff", 17, FontWeight.SemiBold);
    private readonly TextBlock _bodyText = TextBlock("#ded8ed", 13);
    private readonly TextBlock _actionStatusText = TextBlock("#bdb4d4", 12);
    private readonly Button _setUpKeyButton = new() { Content = "Set up key...", MinWidth = 104, Height = 30, Margin = new Thickness(0, 14, 8, 0) };
    private readonly Button _openMusicFolderButton = new() { Content = "Open music folder", MinWidth = 130, Height = 30, Margin = new Thickness(0, 14, 8, 0) };
    private readonly Button _backButton = new() { Content = "Back", Width = 76, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _nextButton = new() { Content = "Next", Width = 76, Height = 30, IsDefault = true };
    private readonly OnboardingStep[] _steps;
    private int _stepIndex;

    public OnboardingWindow(string musicDirectory, Func<Window, Task> configureApiKey)
    {
        _musicDirectory = musicDirectory;
        _configureApiKey = configureApiKey;
        _steps =
        [
            new OnboardingStep(
                "Meet Athena",
                "Athena walks above your Dock by default.\n\nLeft-click Athena to pause for voice. Use the Chat bubble for typed chat. Right-click Athena or use the menu bar/tray icon for settings, music, and exit."),
            new OnboardingStep(
                "Privacy boundaries",
                "Athena does not listen while she is walking.\n\nThe microphone is active only while you pause for voice. Screen capture only happens after you explicitly ask about your screen or request a screen-based image."),
            new OnboardingStep(
                "Your OpenAI key",
                "Voice, text, screen inspection, and image generation use your own OpenAI API key.\n\nThe key stays on this Mac and is stored in macOS Keychain. You can skip this now and set it up later from the app menu."),
            new OnboardingStep(
                "Music folder",
                $"Athena can play local .mp3 and .m4a files through her radio-style music mode.\n\nAdd music under:\n{_musicDirectory}")
        ];

        Title = "Welcome to Athena";
        Width = 480;
        Height = 360;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = BuildContent();
        UpdateStep();
    }

    private Control BuildContent()
    {
        var root = new Grid { Background = Brush("#17151f") };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var header = new DockPanel { Margin = new Thickness(18, 16, 18, 10), LastChildFill = true };
        DockPanel.SetDock(_stepText, Dock.Right);
        _stepText.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(_stepText);
        header.Children.Add(new StackPanel
        {
            Children =
            {
                TextBlock("#f7f1ff", 19, FontWeight.SemiBold, "Athena Companion"),
                TextBlock("#bdb4d4", 12, text: "First-run setup")
            }
        });
        root.Children.Add(header);

        var card = new Border
        {
            Margin = new Thickness(18, 0),
            Padding = new Thickness(18),
            Background = Brush("#201d2b"),
            BorderBrush = Brush("#3f3855"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
        Grid.SetRow(card, 1);

        var bodyGrid = new Grid();
        bodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        bodyGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        bodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        bodyGrid.Children.Add(_titleText);
        _bodyText.Margin = new Thickness(0, 12, 0, 0);
        _bodyText.LineHeight = 20;
        _bodyText.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(_bodyText, 1);
        bodyGrid.Children.Add(_bodyText);

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal };
        _setUpKeyButton.Click += OnSetUpKey;
        _openMusicFolderButton.Click += OnOpenMusicFolder;
        _actionStatusText.VerticalAlignment = VerticalAlignment.Center;
        _actionStatusText.Margin = new Thickness(2, 14, 0, 0);
        _actionStatusText.TextWrapping = TextWrapping.Wrap;
        actionRow.Children.Add(_setUpKeyButton);
        actionRow.Children.Add(_openMusicFolderButton);
        actionRow.Children.Add(_actionStatusText);
        Grid.SetRow(actionRow, 2);
        bodyGrid.Children.Add(actionRow);

        card.Child = bodyGrid;
        root.Children.Add(card);

        var footer = new DockPanel { Margin = new Thickness(18, 12, 18, 16), LastChildFill = false };
        var skip = new Button { Content = "Skip", Width = 76, Height = 30 };
        skip.Click += (_, _) => Close(false);
        footer.Children.Add(skip);
        var nav = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(nav, Dock.Right);
        _backButton.Click += OnBack;
        _nextButton.Click += OnNext;
        nav.Children.Add(_backButton);
        nav.Children.Add(_nextButton);
        footer.Children.Add(nav);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private void OnBack(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_stepIndex <= 0)
        {
            return;
        }

        _stepIndex--;
        UpdateStep();
    }

    private void OnNext(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_stepIndex >= _steps.Length - 1)
        {
            Close(true);
            return;
        }

        _stepIndex++;
        UpdateStep();
    }

    private async void OnSetUpKey(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await _configureApiKey(this);
        _actionStatusText.Text = "Key setup closed.";
    }

    private void OnOpenMusicFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Directory.CreateDirectory(_musicDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _musicDirectory,
            UseShellExecute = true
        });
    }

    private void UpdateStep()
    {
        var step = _steps[_stepIndex];
        _stepText.Text = $"{_stepIndex + 1} of {_steps.Length}";
        _titleText.Text = step.Title;
        _bodyText.Text = step.Body;
        _backButton.IsEnabled = _stepIndex > 0;
        _nextButton.Content = _stepIndex == _steps.Length - 1 ? "Done" : "Next";
        _setUpKeyButton.IsVisible = _stepIndex == 2;
        _openMusicFolderButton.IsVisible = _stepIndex == 3;
        _actionStatusText.Text = string.Empty;
    }

    private static TextBlock TextBlock(string color, double fontSize, FontWeight fontWeight = default, string? text = null) => new()
    {
        Text = text,
        Foreground = Brush(color),
        FontSize = fontSize,
        FontWeight = fontWeight == default ? FontWeight.Normal : fontWeight,
        TextWrapping = TextWrapping.Wrap
    };

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));

    private sealed record OnboardingStep(string Title, string Body);
}
