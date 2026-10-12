namespace QuiverLauncher.Services;

/// <summary>
/// SDL hints that must be applied before <c>SDL_Init</c> on Steam Deck.
/// Desktop Mode must not use Steam HIDAPI, which disables lizard mode and breaks Steam + X.
/// Gaming Mode must allow Steam's virtual gamepad or SDL reports zero pads.
/// SDL's own Steam Deck driver stays off in both modes, so the launcher only sees the pads it saw with SDL 2.0.14,
/// which had no such driver; opening the Deck's controls directly also turns lizard mode off.
/// </summary>
internal static class SteamDeckSdlHints
{
    public const string JoystickHidapiSteamHintName = "SDL_JOYSTICK_HIDAPI_STEAM";
    public const string DisableHidapiSteamValue = "0";

    public const string JoystickHidapiSteamDeckHintName = "SDL_JOYSTICK_HIDAPI_STEAMDECK";
    public const string DisableHidapiSteamDeckValue = "0";

    public const string AllowSteamVirtualGamepadHintName = "SDL_GAMECONTROLLER_ALLOW_STEAM_VIRTUAL_GAMEPAD";
    public const string AllowSteamVirtualGamepadValue = "1";

    /// <summary>
    /// Returns the hint value to set for <see cref="JoystickHidapiSteamHintName"/>,
    /// or null when no override is needed.
    /// </summary>
    public static string? GetHidapiSteamHintValue() =>
        GetHidapiSteamHintValue(
            OperatingSystem.IsLinux(),
            Environment.GetEnvironmentVariable,
            SteamDeckEnvironment.IsGamingMode);

    public static string? GetHidapiSteamHintValue(
        bool isLinux,
        Func<string, string?> getEnvironmentVariable,
        Func<bool, Func<string, string?>, bool> isGamingMode)
    {
        if (!isLinux)
            return null;

        if (string.IsNullOrWhiteSpace(getEnvironmentVariable("SteamDeck")))
            return null;

        if (isGamingMode(isLinux, getEnvironmentVariable))
            return null;

        return DisableHidapiSteamValue;
    }

    /// <summary>
    /// Returns the hint value to set for <see cref="JoystickHidapiSteamDeckHintName"/>,
    /// or null when no override is needed.
    /// </summary>
    public static string? GetHidapiSteamDeckHintValue() =>
        GetHidapiSteamDeckHintValue(OperatingSystem.IsLinux(), Environment.GetEnvironmentVariable);

    public static string? GetHidapiSteamDeckHintValue(bool isLinux, Func<string, string?> getEnvironmentVariable) =>
        isLinux && !string.IsNullOrWhiteSpace(getEnvironmentVariable("SteamDeck")) ? DisableHidapiSteamDeckValue : null;

    /// <summary>
    /// Returns the hint value to set for <see cref="AllowSteamVirtualGamepadHintName"/>,
    /// or null when no override is needed.
    /// </summary>
    public static string? GetAllowSteamVirtualGamepadHintValue() =>
        GetAllowSteamVirtualGamepadHintValue(
            OperatingSystem.IsLinux(),
            Environment.GetEnvironmentVariable,
            SteamDeckEnvironment.IsGamingMode);

    public static string? GetAllowSteamVirtualGamepadHintValue(
        bool isLinux,
        Func<string, string?> getEnvironmentVariable,
        Func<bool, Func<string, string?>, bool> isGamingMode)
    {
        if (!isLinux)
            return null;

        if (!isGamingMode(isLinux, getEnvironmentVariable))
            return null;

        return AllowSteamVirtualGamepadValue;
    }

    /// <summary>
    /// Applies Steam Deck SDL hints via <paramref name="setHint"/> when needed.
    /// Returns true if a hint was applied.
    /// </summary>
    public static bool ApplyBeforeInit(Action<string, string> setHint) =>
        ApplyBeforeInit(
            GetHidapiSteamHintValue(),
            GetAllowSteamVirtualGamepadHintValue(),
            GetHidapiSteamDeckHintValue(),
            setHint);

    public static bool ApplyBeforeInit(string? hidapiSteamHintValue, Action<string, string> setHint) =>
        ApplyBeforeInit(hidapiSteamHintValue, allowSteamVirtualGamepadHintValue: null, setHint);

    public static bool ApplyBeforeInit(
        string? hidapiSteamHintValue,
        string? allowSteamVirtualGamepadHintValue,
        Action<string, string> setHint) =>
        ApplyBeforeInit(hidapiSteamHintValue, allowSteamVirtualGamepadHintValue, hidapiSteamDeckHintValue: null, setHint);

    public static bool ApplyBeforeInit(
        string? hidapiSteamHintValue,
        string? allowSteamVirtualGamepadHintValue,
        string? hidapiSteamDeckHintValue,
        Action<string, string> setHint)
    {
        var applied = false;

        if (hidapiSteamHintValue != null)
        {
            setHint(JoystickHidapiSteamHintName, hidapiSteamHintValue);
            applied = true;
        }

        if (allowSteamVirtualGamepadHintValue != null)
        {
            setHint(AllowSteamVirtualGamepadHintName, allowSteamVirtualGamepadHintValue);
            applied = true;
        }

        if (hidapiSteamDeckHintValue != null)
        {
            setHint(JoystickHidapiSteamDeckHintName, hidapiSteamDeckHintValue);
            applied = true;
        }

        return applied;
    }
}
