using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SDL;

namespace QuiverLauncher.Services;

public enum GamepadAction
{
    Confirm,
    Cancel,
    Options,
    NavUp,
    NavDown,
    NavLeft,
    NavRight,
}

public enum GamepadInputKind
{
    Button,
    AxisPositive,
    AxisNegative,
}

public sealed class GamepadBinding : IEquatable<GamepadBinding>
{
    public GamepadInputKind Kind { get; set; }

    /// <summary>
    /// The button or axis number, saved in settings. Buttons keep SDL2's numbering: 0-3 are the face buttons
    /// labelled A, B, X and Y (InputService maps SDL3's positional South/East/West/North onto them).
    /// </summary>
    public int SdlCode { get; set; }

    public GamepadBinding()
    {
    }

    public GamepadBinding(GamepadInputKind kind, int sdlCode)
    {
        Kind = kind;
        SdlCode = sdlCode;
    }

    public static GamepadBinding Button(SDL_GamepadButton button) =>
        new(GamepadInputKind.Button, (int)button);

    public static GamepadBinding AxisPositive(SDL_GamepadAxis axis) =>
        new(GamepadInputKind.AxisPositive, (int)axis);

    public static GamepadBinding AxisNegative(SDL_GamepadAxis axis) =>
        new(GamepadInputKind.AxisNegative, (int)axis);

    public bool Equals(GamepadBinding? other) =>
        other != null && Kind == other.Kind && SdlCode == other.SdlCode;

    public override bool Equals(object? obj) => obj is GamepadBinding other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Kind, SdlCode);
}

public sealed class ConnectedGamepadInfo
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
}

public static class GamepadBindingDefaults
{
    public static Dictionary<GamepadAction, List<GamepadBinding>> Create() => new()
    {
        [GamepadAction.Confirm] =
        [
            GamepadBinding.Button(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH),
        ],
        [GamepadAction.Cancel] =
        [
            GamepadBinding.Button(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST),
        ],
        [GamepadAction.Options] =
        [
            GamepadBinding.Button(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH),
        ],
        [GamepadAction.NavUp] =
        [
            GamepadBinding.Button(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP),
            GamepadBinding.AxisNegative(SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY),
        ],
        [GamepadAction.NavDown] =
        [
            GamepadBinding.Button(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN),
            GamepadBinding.AxisPositive(SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY),
        ],
        [GamepadAction.NavLeft] =
        [
            GamepadBinding.Button(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT),
            GamepadBinding.AxisNegative(SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX),
        ],
        [GamepadAction.NavRight] =
        [
            GamepadBinding.Button(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT),
            GamepadBinding.AxisPositive(SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX),
        ],
    };

    public static Dictionary<GamepadAction, List<GamepadBinding>> Clone(
        IReadOnlyDictionary<GamepadAction, List<GamepadBinding>> source)
    {
        var clone = new Dictionary<GamepadAction, List<GamepadBinding>>();
        foreach (var action in Enum.GetValues<GamepadAction>())
        {
            if (source.TryGetValue(action, out var list) && list != null)
            {
                clone[action] = list
                    .Select(b => new GamepadBinding(b.Kind, b.SdlCode))
                    .ToList();
            }
            else
            {
                clone[action] = Create()[action]
                    .Select(b => new GamepadBinding(b.Kind, b.SdlCode))
                    .ToList();
            }
        }

        return clone;
    }

    public static void EnsureComplete(Dictionary<GamepadAction, List<GamepadBinding>>? bindings)
    {
        if (bindings == null)
            return;

        var defaults = Create();
        foreach (var action in Enum.GetValues<GamepadAction>())
        {
            // An empty list is an intentional unbind after exclusive reassignment.
            if (!bindings.TryGetValue(action, out var list) || list == null)
                bindings[action] = defaults[action]
                    .Select(b => new GamepadBinding(b.Kind, b.SdlCode))
                    .ToList();
        }
    }

    /// <summary>
    /// Assigns <paramref name="binding"/> to <paramref name="action"/> (replacing that action's
    /// bindings with a single entry) and removes the same binding from every other action.
    /// </summary>
    public static void AssignExclusive(
        Dictionary<GamepadAction, List<GamepadBinding>> bindings,
        GamepadAction action,
        GamepadBinding binding)
    {
        foreach (var other in Enum.GetValues<GamepadAction>())
        {
            if (other == action)
                continue;

            if (!bindings.TryGetValue(other, out var list) || list == null)
                continue;

            list.RemoveAll(b => b.Equals(binding));
        }

        bindings[action] = [new GamepadBinding(binding.Kind, binding.SdlCode)];
    }
}

public static class GamepadBindingLabels
{
    public static string Format(GamepadBinding binding) => binding.Kind switch
    {
        GamepadInputKind.Button => FormatButton((SDL_GamepadButton)binding.SdlCode),
        GamepadInputKind.AxisPositive => FormatAxis((SDL_GamepadAxis)binding.SdlCode, positive: true),
        GamepadInputKind.AxisNegative => FormatAxis((SDL_GamepadAxis)binding.SdlCode, positive: false),
        _ => "?",
    };

    public static string FormatActionBindings(
        IReadOnlyDictionary<GamepadAction, List<GamepadBinding>> bindings,
        GamepadAction action)
    {
        if (!bindings.TryGetValue(action, out var list) || list == null || list.Count == 0)
            return "—";

        return string.Join(" / ", list.Select(Format));
    }

    public static string FormatHints(IReadOnlyDictionary<GamepadAction, List<GamepadBinding>> bindings, string optionsLabel = "Options")
    {
        var confirm = FormatActionBindings(bindings, GamepadAction.Confirm);
        var options = FormatActionBindings(bindings, GamepadAction.Options);
        var cancel = FormatActionBindings(bindings, GamepadAction.Cancel);
        var nav = FormatNavigationHint(bindings);
        return $"{confirm} (Select) · {options} ({optionsLabel}) · {cancel} (Back) · {nav} (Navigate)";
    }

    private static string FormatNavigationHint(
        IReadOnlyDictionary<GamepadAction, List<GamepadBinding>> bindings)
    {
        var labels = new[]
            {
                GamepadAction.NavUp,
                GamepadAction.NavDown,
                GamepadAction.NavLeft,
                GamepadAction.NavRight,
            }
            .SelectMany(a => bindings.TryGetValue(a, out var list) && list != null
                ? list.Select(Format)
                : Enumerable.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (labels.Count == 0)
            return "D-pad";

        // Prefer a short summary when defaults are present.
        var hasDpad = labels.Any(l => l is "↑" or "↓" or "←" or "→");
        var hasStick = labels.Any(l => l.StartsWith("LS", StringComparison.Ordinal));
        if (hasDpad && hasStick)
            return "D-pad";
        if (hasDpad)
            return "D-pad";
        if (hasStick)
            return "LS";

        return string.Join("/", labels.Take(4));
    }

    private static string FormatButton(SDL_GamepadButton button) => button switch
    {
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => "A",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => "B",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => "X",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => "Y",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => "Back",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => "Guide",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => "Start",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => "L3",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => "R3",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => "LB",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => "RB",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => "↑",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => "↓",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => "←",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => "→",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1 => "Misc",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE1 => "P1",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE1 => "P2",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE2 => "P3",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE2 => "P4",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_TOUCHPAD => "Touch",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC2 => "Misc 2",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC3 => "Misc 3",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC4 => "Misc 4",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC5 => "Misc 5",
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC6 => "Misc 6",
        _ => $"Btn{(int)button}",
    };

    private static string FormatAxis(SDL_GamepadAxis axis, bool positive) => axis switch
    {
        SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX => positive ? "LS→" : "LS←",
        SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY => positive ? "LS↓" : "LS↑",
        SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX => positive ? "RS→" : "RS←",
        SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY => positive ? "RS↓" : "RS↑",
        SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER => "LT",
        SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER => "RT",
        _ => $"Axis{(int)axis}{(positive ? "+" : "-")}",
    };
}
