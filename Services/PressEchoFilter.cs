using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace QuiverLauncher.Services;

public enum PressSource
{
    Gamepad,
    Keyboard,
}

/// <summary>
/// Keeps one press of a controller button to one action when it reaches the launcher twice. On a Steam Deck,
/// Steam's desktop layout and the Deck's own lizard mode type Enter for A and Escape for B while SDL reports
/// the button too, so a checkbox would toggle back, a dropdown close again and a text field leave edit.
/// Whichever copy arrives first runs; the other source's copy of the same action is dropped if it follows
/// within <see cref="EchoWindow"/>, along with the key's repeats while it is held.
/// </summary>
public sealed class PressEchoFilter
{
    /// <summary>Same as the controller's own button repeat guard in <see cref="InputService"/>.</summary>
    public static readonly TimeSpan EchoWindow = TimeSpan.FromMilliseconds(300);

    // A dropped key keeps being dropped while it repeats. Longer than the usual 500-660 ms repeat delay.
    private static readonly TimeSpan HeldKeyTimeout = TimeSpan.FromSeconds(1);

    private static bool _keyboardHookInstalled;

    private readonly Dictionary<GamepadAction, (PressSource Source, DateTime At)> _lastAccepted = new();
    private readonly Dictionary<Key, DateTime> _droppedHeldKeys = new();

    public static PressEchoFilter Instance { get; } = new(() => DateTime.Now);

    public PressEchoFilter(Func<DateTime> clock) => Clock = clock;

    public Func<DateTime> Clock { get; set; }

    /// <summary>Maps a key to the action the user bound it to. Set by the active <see cref="LauncherInputController"/>.</summary>
    public Func<Key, KeyModifiers, GamepadAction?>? ResolveKeyboardAction { get; set; }

    /// <summary>
    /// Records a press and returns true when it should run. False when it is the other source's copy of a
    /// press that already ran inside <see cref="EchoWindow"/>.
    /// </summary>
    public bool TryAccept(GamepadAction action, PressSource source)
    {
        var now = Clock();
        if (_lastAccepted.TryGetValue(action, out var last) && last.Source != source && now - last.At < EchoWindow)
        {
            GamepadDebugLog.Write($"echo dropped action={action} source={source} after={last.Source} ms={(now - last.At).TotalMilliseconds:0}");
            return false;
        }

        _lastAccepted[action] = (source, now);
        return true;
    }

    public void Reset()
    {
        _lastAccepted.Clear();
        _droppedHeldKeys.Clear();
    }

    /// <summary>
    /// Filters keys in every window and popup before any handler sees them, including the controls' own
    /// Enter and Space handling. Installed once for the process.
    /// </summary>
    public static void InstallKeyboardHook()
    {
        if (_keyboardHookInstalled)
            return;

        _keyboardHookInstalled = true;
        InputElement.KeyDownEvent.AddClassHandler<TopLevel>((_, e) => Instance.OnKeyDown(e), RoutingStrategies.Tunnel, handledEventsToo: true);
        InputElement.KeyUpEvent.AddClassHandler<TopLevel>((_, e) => Instance.OnKeyUp(e), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    internal void OnKeyDown(KeyEventArgs e)
    {
        if (ResolvePressAction(e.Key, e.KeyModifiers) is not { } action)
            return;

        var now = Clock();
        if (_droppedHeldKeys.TryGetValue(e.Key, out var lastSeen) && now - lastSeen < HeldKeyTimeout)
        {
            _droppedHeldKeys[e.Key] = now;
            e.Handled = true;
            return;
        }

        _droppedHeldKeys.Remove(e.Key);
        if (TryAccept(action, PressSource.Keyboard))
            return;

        _droppedHeldKeys[e.Key] = now;
        e.Handled = true;
    }

    internal void OnKeyUp(KeyEventArgs e)
    {
        // Button and CheckBox act on Space when it is released, so the dropped press's release goes too.
        if (_droppedHeldKeys.Remove(e.Key))
            e.Handled = true;
    }

    /// <summary>
    /// The press action a key stands for. Enter and Escape always count as Confirm and Cancel: they are the
    /// keys Steam types for A and B, and text fields and dialogs treat them that way whatever the bindings say.
    /// Navigation keys are left alone; the controller and keyboard already share one repeat timer for those.
    /// </summary>
    private GamepadAction? ResolvePressAction(Key key, KeyModifiers modifiers)
    {
        var action = ResolveKeyboardAction?.Invoke(key, modifiers);
        if (action is GamepadAction.Confirm or GamepadAction.Cancel or GamepadAction.Options)
            return action;

        return key switch
        {
            Key.Enter => GamepadAction.Confirm,
            Key.Escape => GamepadAction.Cancel,
            _ => null,
        };
    }
}
