using Avalonia;
using Avalonia.Controls;
using AthenaCompanion.Settings;

namespace AthenaCompanion.UI;

internal sealed record TrayMenuStateProvider(
    Func<AthenaInteractionMode> GetInteractionMode,
    Func<bool> GetClickThrough,
    Func<string> GetVoiceStatus,
    Func<string> GetCurrentVoice,
    Func<string> GetKeyStatus);

internal sealed class TrayMenuController : IDisposable
{
    private readonly TrayMenuStateProvider _state;
    private TrayIcon? _trayIcon;

    public TrayMenuController(TrayMenuStateProvider state)
    {
        _state = state;
    }

    public event EventHandler? PauseRequested;
    public event EventHandler? ClickThroughToggled;
    public event EventHandler? TextModeRequested;
    public event EventHandler? MusicRequested;
    public event EventHandler<string>? VoiceChanged;
    public event EventHandler? ConfigureApiKeyRequested;
    public event EventHandler? RemoveApiKeyRequested;
    public event EventHandler? OnboardingRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        if (_trayIcon is not null)
        {
            return;
        }

        _trayIcon = new TrayIcon
        {
            Icon = IconLoader.LoadWindowIcon(),
            ToolTipText = "Athena Companion",
            Menu = BuildNativeMenu()
        };
        _trayIcon.Clicked += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
        if (Application.Current is not null)
        {
            TrayIcon.SetIcons(Application.Current, new TrayIcons { _trayIcon });
        }
        Refresh();
    }

    public void ShowContextMenu(Control target)
    {
        var menu = BuildContextMenu();
        menu.Open(target);
    }

    public void ShowBalloonTip(string title, string message) =>
        Console.Error.WriteLine($"{title}: {message}");

    public void Refresh()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Menu = BuildNativeMenu();
        }
    }

    public void Dispose()
    {
        if (_trayIcon is null)
        {
            return;
        }

        if (Application.Current is not null)
        {
            TrayIcon.SetIcons(Application.Current, null);
        }
        _trayIcon.Dispose();
        _trayIcon = null;
    }

    private NativeMenu BuildNativeMenu()
    {
        var menu = new NativeMenu();
        menu.Items.Add(NativeItem(PauseLabel(), PauseRequested));
        menu.Items.Add(NativeItem(TextLabel(), TextModeRequested));
        menu.Items.Add(NativeItem(MusicLabel(), MusicRequested));
        menu.Items.Add(NativeItem("Click-through", ClickThroughToggled));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem(VoiceStatusLabel()) { IsEnabled = false });

        var voiceMenu = new NativeMenuItem("Voice") { Menu = new NativeMenu() };
        foreach (var voice in RealtimeVoiceOptions.BuiltIn)
        {
            voiceMenu.Menu!.Items.Add(NativeItem(ToTitleCase(voice), () => VoiceChanged?.Invoke(this, voice)));
        }

        menu.Items.Add(voiceMenu);
        menu.Items.Add(NativeItem("OpenAI API Key...", ConfigureApiKeyRequested));
        menu.Items.Add(NativeItem("Remove saved OpenAI API Key", RemoveApiKeyRequested, _state.GetKeyStatus() is "Credential Manager" or "macOS Keychain"));
        menu.Items.Add(NativeItem("Onboarding...", OnboardingRequested));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(NativeItem("Exit", ExitRequested));
        return menu;
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(ContextItem(PauseLabel(), PauseRequested));
        menu.Items.Add(ContextItem(TextLabel(), TextModeRequested));
        menu.Items.Add(ContextItem(MusicLabel(), MusicRequested));
        menu.Items.Add(ContextItem("Click-through", ClickThroughToggled));
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = VoiceStatusLabel(), IsEnabled = false });

        var voiceMenu = new MenuItem { Header = "Voice" };
        foreach (var voice in RealtimeVoiceOptions.BuiltIn)
        {
            voiceMenu.Items.Add(ContextItem(ToTitleCase(voice), () => VoiceChanged?.Invoke(this, voice)));
        }

        menu.Items.Add(voiceMenu);
        menu.Items.Add(ContextItem("OpenAI API Key...", ConfigureApiKeyRequested));
        menu.Items.Add(ContextItem("Remove saved OpenAI API Key", RemoveApiKeyRequested, _state.GetKeyStatus() is "Credential Manager" or "macOS Keychain"));
        menu.Items.Add(ContextItem("Onboarding...", OnboardingRequested));
        menu.Items.Add(new Separator());
        menu.Items.Add(ContextItem("Exit", ExitRequested));
        return menu;
    }

    private NativeMenuItem NativeItem(string header, EventHandler? handler, bool enabled = true)
    {
        var item = new NativeMenuItem(header) { IsEnabled = enabled };
        if (handler is not null)
        {
            item.Click += handler;
        }

        return item;
    }

    private NativeMenuItem NativeItem(string header, Action action)
    {
        var item = new NativeMenuItem(header);
        item.Click += (_, _) => action();
        return item;
    }

    private MenuItem ContextItem(string header, EventHandler? handler, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        if (handler is not null)
        {
            item.Click += (_, _) => handler(this, EventArgs.Empty);
        }

        return item;
    }

    private MenuItem ContextItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private string PauseLabel() => _state.GetInteractionMode() == AthenaInteractionMode.Voice ? "Resume walking" : "Pause for voice";

    private string TextLabel() => _state.GetInteractionMode() == AthenaInteractionMode.Text ? "Focus text chat" : "Text chat";

    private string MusicLabel() => _state.GetInteractionMode() == AthenaInteractionMode.Music ? "Focus music player" : "Music player";

    private string VoiceStatusLabel() => $"Voice: {_state.GetVoiceStatus()}, {_state.GetCurrentVoice()} ({_state.GetKeyStatus()})";

    private static string ToTitleCase(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
}
