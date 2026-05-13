using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AthenaCompanion.Settings;

public sealed class ApiKeySetupWindow : Window
{
    private readonly TextBox _apiKeyBox = new() { PasswordChar = '*', Height = 32 };
    private readonly TextBlock _validationText = new()
    {
        Foreground = Brush("#9a3412"),
        TextWrapping = TextWrapping.Wrap
    };
    private readonly Button _saveButton = new()
    {
        Content = "Save",
        Width = 86,
        IsDefault = true,
        IsEnabled = false
    };

    public ApiKeySetupWindow()
    {
        Title = "OpenAI API Key";
        Width = 440;
        Height = 230;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = BuildContent();

        _apiKeyBox.TextChanged += (_, _) =>
        {
            _saveButton.IsEnabled = !string.IsNullOrWhiteSpace(ApiKey);
            _validationText.Text = string.Empty;
        };
        _saveButton.Click += OnSave;
    }

    public string ApiKey => (_apiKeyBox.Text ?? string.Empty).Trim();

    private Control BuildContent()
    {
        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        root.Children.Add(new TextBlock
        {
            Text = "OpenAI API Key",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold
        });

        var body = new TextBlock
        {
            Text = "Athena uses your own OpenAI API key for voice and text mode. The key is stored locally in macOS Keychain on macOS and Windows Credential Manager on Windows.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#555"),
            Margin = new Thickness(0, 8, 0, 14)
        };
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        Grid.SetRow(_apiKeyBox, 2);
        root.Children.Add(_apiKeyBox);

        _validationText.Margin = new Thickness(0, 8, 0, 0);
        Grid.SetRow(_validationText, 3);
        root.Children.Add(_validationText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 86,
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        cancelButton.Click += (_, _) => Close(false);
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(_saveButton);
        Grid.SetRow(buttons, 4);
        root.Children.Add(buttons);

        return root;
    }

    private void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            _validationText.Text = "Enter an OpenAI API key.";
            return;
        }

        Close(true);
    }

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
