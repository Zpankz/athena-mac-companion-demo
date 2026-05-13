using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AthenaCompanion.TextChat;

namespace AthenaCompanion.UI;

internal sealed class TextChatWindow : Window
{
    private readonly AthenaTextChatSession _session;
    private readonly CancellationTokenSource _cts = new();
    private readonly TextBlock _statusText = TextBlock("#bdb4d4", 12, "Ready");
    private readonly ScrollViewer _messagesScroll = new();
    private readonly StackPanel _messagesPanel = new();
    private readonly TextBox _inputBox = new()
    {
        MinHeight = 38,
        MaxHeight = 96,
        Padding = new Thickness(10, 8),
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap
    };
    private readonly Button _sendButton = new()
    {
        Content = "Send",
        Width = 78,
        Height = 38,
        Margin = new Thickness(8, 0, 0, 0)
    };
    private bool _sending;

    public TextChatWindow(AthenaTextChatSession session)
    {
        _session = session;
        _session.StatusChanged += OnSessionStatusChanged;
        Title = "Athena Text";
        Width = 430;
        Height = 560;
        MinWidth = 360;
        MinHeight = 420;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Content = BuildContent();
        _sendButton.Click += OnSend;
        _inputBox.KeyDown += OnInputKeyDown;
        AppendMessage("Athena", "Text mode is ready.");
    }

    public event EventHandler<string>? StatusChanged;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _inputBox.Focus();
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts.Cancel();
        _session.StatusChanged -= OnSessionStatusChanged;
        _session.Dispose();
        _cts.Dispose();
        base.OnClosed(e);
    }

    private Control BuildContent()
    {
        var root = new Grid { Background = Brush("#17151f") };
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var header = new DockPanel { Margin = new Thickness(14, 12, 14, 10), LastChildFill = true };
        var close = new Button { Content = "Close", Width = 72, Height = 28 };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(new StackPanel
        {
            Children =
            {
                TextBlock("#f7f1ff", 17, "Athena Text", FontWeight.SemiBold),
                _statusText
            }
        });
        root.Children.Add(header);

        _messagesScroll.Padding = new Thickness(12);
        _messagesScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _messagesScroll.Content = _messagesPanel;
        var messagesBorder = new Border
        {
            Margin = new Thickness(14, 0),
            Background = Brush("#201d2b"),
            BorderBrush = Brush("#3f3855"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = _messagesScroll
        };
        Grid.SetRow(messagesBorder, 1);
        root.Children.Add(messagesBorder);

        var inputGrid = new Grid { Margin = new Thickness(14, 10, 14, 14) };
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        inputGrid.Children.Add(_inputBox);
        Grid.SetColumn(_sendButton, 1);
        inputGrid.Children.Add(_sendButton);
        Grid.SetRow(inputGrid, 2);
        root.Children.Add(inputGrid);

        return root;
    }

    private async void OnSend(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await SendCurrentMessageAsync();

    private async void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            return;
        }

        e.Handled = true;
        await SendCurrentMessageAsync();
    }

    private async Task SendCurrentMessageAsync()
    {
        if (_sending)
        {
            return;
        }

        var message = (_inputBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _inputBox.Text = string.Empty;
        AppendMessage("You", message);
        SetSendingState(true, "Thinking");

        try
        {
            var reply = await _session.SendAsync(message, _cts.Token);
            if (!string.IsNullOrWhiteSpace(reply))
            {
                AppendMessage("Athena", reply);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendMessage("Athena", $"Text mode failed: {ex.Message}");
        }
        finally
        {
            SetSendingState(false, "Ready");
        }
    }

    private void SetSendingState(bool sending, string status)
    {
        _sending = sending;
        _sendButton.IsEnabled = !sending;
        _inputBox.IsEnabled = !sending;
        _statusText.Text = status;
        StatusChanged?.Invoke(this, sending ? status : "Text ready");
        if (!sending)
        {
            _inputBox.Focus();
        }
    }

    private void AppendMessage(string author, string text)
    {
        var bubble = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 0, 0, 10),
            MaxWidth = 360,
            HorizontalAlignment = author == "You" ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = author == "You" ? Brush("#524577") : Brush("#2b263d")
        };

        bubble.Child = new StackPanel
        {
            Children =
            {
                TextBlock("#d2c6ee", 11, author, FontWeight.SemiBold),
                new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.WhiteSmoke,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 0)
                }
            }
        };
        _messagesPanel.Children.Add(bubble);
        Dispatcher.UIThread.Post(() => _messagesScroll.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void OnSessionStatusChanged(object? sender, string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _statusText.Text = status;
            StatusChanged?.Invoke(this, status);
        });
    }

    private static TextBlock TextBlock(string color, double fontSize, string? text = null, FontWeight fontWeight = default) => new()
    {
        Text = text,
        Foreground = Brush(color),
        FontSize = fontSize,
        FontWeight = fontWeight == default ? FontWeight.Normal : fontWeight
    };

    private static IBrush Brush(string value) => new SolidColorBrush(Color.Parse(value));
}
