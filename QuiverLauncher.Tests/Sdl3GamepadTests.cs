using FluentAssertions;
using QuiverLauncher.Services;
using SDL;
using static SDL.SDL3;

namespace QuiverLauncher.Tests;

public class Sdl3GamepadTests
{
    [Theory]
    // Xbox and PlayStation pads: SDL2 and SDL3 agree, the bottom button is A.
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST)]
    // Nintendo pads: SDL2 reported the printed labels, so the right button (A) stays "A" and the bottom one "B".
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START, SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START)]
    public void Face_buttons_report_the_code_SDL2_gave_them(SDL_GamepadType type, SDL_GamepadButton pressed, SDL_GamepadButton expected)
    {
        var label = SDL_GetGamepadButtonLabelForType(type, pressed);

        InputService.GetButtonCode(pressed, label).Should().Be((int)expected);
    }

    [Fact]
    public void Saved_binding_codes_keep_their_SDL2_numbers()
    {
        // settings.json stores these numbers; SDL2's A, Back, D-pad up, touchpad and right trigger.
        ((int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH).Should().Be(0);
        ((int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK).Should().Be(4);
        ((int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP).Should().Be(11);
        ((int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_TOUCHPAD).Should().Be(20);
        ((int)SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER).Should().Be(5);
    }

    [Fact]
    public void Native_SDL3_library_is_shipped_with_the_build()
    {
        // No SDL_Init here: InputService tests init and quit SDL, and other test classes run in parallel.
        SDL_GetVersion().Should().BeGreaterThanOrEqualTo(3_002_000);
    }

    [Theory]
    [InlineData(false, "1", null)]
    [InlineData(true, null, null)]
    [InlineData(true, "1", SteamDeckSdlHints.DisableHidapiSteamDeckValue)]
    public void Steam_Deck_driver_stays_off_on_the_Deck(bool isLinux, string? steamDeck, string? expected)
    {
        SteamDeckSdlHints.GetHidapiSteamDeckHintValue(isLinux, name => name == "SteamDeck" ? steamDeck : null)
            .Should().Be(expected);
    }

    [Fact]
    public void ApplyBeforeInit_sets_the_Steam_Deck_driver_hint()
    {
        var set = new List<(string, string)>();

        SteamDeckSdlHints.ApplyBeforeInit(SteamDeckSdlHints.DisableHidapiSteamValue, null,
            SteamDeckSdlHints.DisableHidapiSteamDeckValue, (name, value) => set.Add((name, value))).Should().BeTrue();

        set.Should().Equal(
            (SteamDeckSdlHints.JoystickHidapiSteamHintName, SteamDeckSdlHints.DisableHidapiSteamValue),
            (SteamDeckSdlHints.JoystickHidapiSteamDeckHintName, SteamDeckSdlHints.DisableHidapiSteamDeckValue));
    }
}
