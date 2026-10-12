using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using static SDL.SDL3;

namespace QuiverLauncher.Services
{
    public class InputService : IDisposable, IInputBindingSource
    {
        private readonly Control _mainWindow;
        private DispatcherTimer? _gamepadTimer;
        // Keyed by SDL joystick instance ID, which SDL3 never reuses for another device.
        private readonly Dictionary<SDL_JoystickID, GamepadSample> _gamepadStates = new();
        private readonly Dictionary<SDL_JoystickID, OpenGamepad> _gameControllers = new();
        private Dictionary<GamepadAction, List<GamepadBinding>> _bindings = GamepadBindingDefaults.Create();
        private bool _disposed = false;
        private bool _isWindowActive = true;
        private bool _captureMode;
        // Set when the native SDL3 library cannot be loaded; gamepad input is then unavailable.
        private bool _sdlLibraryMissing;

        private bool SdlAvailable => PlatformCapabilities.SupportsGamepadSdl && !_sdlLibraryMissing;

        // Gamepad deadzone threshold
        private const float DeadZone = 0.3f;
        private const short AxisMax = 32767;

        private static readonly SDL_GamepadButton[] AllButtons =
            Enum.GetValues<SDL_GamepadButton>()
                .Where(b => b is not (SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID or SDL_GamepadButton.SDL_GAMEPAD_BUTTON_COUNT))
                .ToArray();

        private static readonly SDL_GamepadAxis[] AllAxes =
            Enum.GetValues<SDL_GamepadAxis>()
                .Where(a => a is not (SDL_GamepadAxis.SDL_GAMEPAD_AXIS_INVALID or SDL_GamepadAxis.SDL_GAMEPAD_AXIS_COUNT))
                .ToArray();

        // Input repeat delays (hold auto-repeat; discrete presses reset on release)
        private const int InitialRepeatDelay = 500;
        private const int RepeatDelay = 250;

        private DateTime _lastNavigationTime = DateTime.MinValue;
        private int _movesInHold;

        private DateTime _lastConfirmTime = DateTime.MinValue;
        private DateTime _lastCancelTime = DateTime.MinValue;
        private DateTime _lastOptionsTime = DateTime.MinValue;
        private const int ButtonRepeatDelay = 300;

        public event Action<NavigationDirection>? OnNavigate;
        public event Action? OnConfirm;
        public event Action? OnCancel;
        public event Action? OnOptions;
        public event Action<GamepadBinding>? OnRawInput;
        /// <summary>Raised when connected gamepad count crosses between zero and nonzero.</summary>
        public event Action<bool>? OnGamepadConnectionChanged;

        public Func<NavigationDirection, bool>? NavigationInterceptor { get; set; }

        private readonly GamepadModalDialogNavigation _modalDialogNavigation = GamepadModalDialogNavigation.Instance;
        private readonly GamepadContextMenuNavigation _contextMenuNavigation = GamepadContextMenuNavigation.Instance;
        private readonly GamepadComboBoxNavigation _comboBoxNavigation = GamepadComboBoxNavigation.Instance;
        private readonly GamepadMenuFlyoutNavigation _menuFlyoutNavigation = GamepadMenuFlyoutNavigation.Instance;

        public bool IsGamepadOverlayActive =>
            _modalDialogNavigation.HasActiveDialog ||
            _contextMenuNavigation.HasActiveContextMenu ||
            _comboBoxNavigation.HasActiveComboBox ||
            _menuFlyoutNavigation.HasActiveMenuFlyout;

        public bool ShouldKeepPollingWhenDeactivated() => IsGamepadOverlayActive;

        public bool IsCaptureMode => _captureMode;

        public InputService(Control mainWindow, AppSettings appSettings)
        {
            _mainWindow = mainWindow;
            _modalDialogNavigation.Configure(this);
            _contextMenuNavigation.Configure(this);
            ApplyBindings(appSettings.GamepadBindings);
            InitializeSDL();

            if (appSettings.EnableGamepadInput)
            {
                InitializeGamepadPolling();
            }
        }

        public void ApplyBindings(IReadOnlyDictionary<GamepadAction, List<GamepadBinding>>? bindings)
        {
            _bindings = bindings == null
                ? GamepadBindingDefaults.Create()
                : GamepadBindingDefaults.Clone(bindings);
            GamepadBindingDefaults.EnsureComplete(_bindings);
        }

        public void SetCaptureMode(bool enabled)
        {
            _captureMode = enabled;
            if (enabled)
                ResetNavigationState();
        }

        public bool HasConnectedGamepad => _gameControllers.Count > 0;

        public int ConnectedGamepadCount => _gameControllers.Count;

        /// <summary>
        /// Returns whether a connection-changed event should fire, and the new
        /// <c>hasConnected</c> value when it should. Null means no transition.
        /// </summary>
        public static bool? GetConnectionChangedSignal(int previousCount, int nextCount)
        {
            var hadConnected = previousCount > 0;
            var hasConnected = nextCount > 0;
            if (hadConnected == hasConnected)
                return null;

            return hasConnected;
        }

        public IReadOnlyList<ConnectedGamepadInfo> GetConnectedGamepads()
        {
            if (!SdlAvailable)
                return Array.Empty<ConnectedGamepadInfo>();

            RefreshConnectedControllers();

            return _gameControllers
                .OrderBy(kvp => kvp.Key)
                .Select((kvp, index) =>
                {
                    var name = kvp.Value.Name;
                    return new ConnectedGamepadInfo
                    {
                        Index = index,
                        Name = string.IsNullOrWhiteSpace(name) ? $"Controller {index + 1}" : name,
                    };
                })
                .ToList();
        }

        /// <summary>
        /// Whether an already-opened SDL handle for this joystick instance is still valid.
        /// Steam's virtual pad often drops and reconnects as a new device.
        /// </summary>
        public static bool ShouldKeepOpenController(bool alreadyOpen, bool attached) =>
            alreadyOpen && attached;

        public static bool ShouldReopenController(bool alreadyOpen, bool attached) =>
            alreadyOpen && !attached;

        public void RefreshConnectedControllers()
        {
            if (!SdlAvailable)
                return;

            var previousCount = _gameControllers.Count;
            var seen = new HashSet<SDL_JoystickID>();

            using (var gamepads = SDL_GetGamepads())
            {
                for (int i = 0; i < (gamepads?.Count ?? 0); i++)
                {
                    var id = gamepads![i];
                    seen.Add(id);
                    if (_gameControllers.TryGetValue(id, out var existing))
                    {
                        var attached = existing.IsConnected;
                        if (ShouldKeepOpenController(alreadyOpen: true, attached))
                            continue;

                        if (ShouldReopenController(alreadyOpen: true, attached))
                            CloseControllerAt(id);
                    }

                    var controller = OpenGamepad.Open(id);
                    if (controller == null)
                        continue;

                    _gameControllers[id] = controller;
                    _gamepadStates[id] = new GamepadSample();
                    System.Diagnostics.Debug.WriteLine($"Game controller {id} connected: {controller.Name}");
                }
            }

            var removed = _gameControllers.Keys.Where(index => !seen.Contains(index)).ToList();
            foreach (var index in removed)
                CloseControllerAt(index);

            var signal = GetConnectionChangedSignal(previousCount, _gameControllers.Count);
            if (signal.HasValue)
                OnGamepadConnectionChanged?.Invoke(signal.Value);
        }

        /// <summary>
        /// Drop every open SDL controller, pump joystick events, and rescan.
        /// Used after a game returns the Steam virtual pad in Gaming Mode.
        /// </summary>
        public void ReclaimGamepads(bool reinitIfEmpty = false)
        {
            if (!SdlAvailable)
                return;

            CloseAllControllers();
            CheckSDLWindowFocus();
            SDL_UpdateGamepads();
            RefreshConnectedControllers();

            if (reinitIfEmpty && _gameControllers.Count == 0)
                ReinitGameControllerSubsystem();
        }

        private void CloseControllerAt(SDL_JoystickID id)
        {
            if (_gameControllers.Remove(id, out var controller))
                controller.Close();

            _gamepadStates.Remove(id);
        }

        private void CloseAllControllers()
        {
            foreach (var id in _gameControllers.Keys.ToList())
                CloseControllerAt(id);
        }

        private void ReinitGameControllerSubsystem()
        {
            SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_GAMEPAD);
            SteamDeckSdlHints.ApplyBeforeInit((name, value) => SDL_SetHint(name, value));
            if (!SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_GAMEPAD))
            {
                System.Diagnostics.Debug.WriteLine($"SDL gamepad reinit failed: {SDL_GetError()}");
                return;
            }

            RefreshConnectedControllers();
        }

        private void InitializeSDL()
        {
            if (!PlatformCapabilities.SupportsGamepadSdl)
                return;

            try
            {
                // Desktop Mode: avoid Steam HIDAPI so lizard mode / Steam+X keep working.
                // Gaming Mode: allow Steam Virtual Gamepad so SDL sees the Deck pad.
                SteamDeckSdlHints.ApplyBeforeInit((name, value) => SDL_SetHint(name, value));

                if (!SDL_Init(SDL_InitFlags.SDL_INIT_GAMEPAD))
                {
                    System.Diagnostics.Debug.WriteLine($"SDL initialization failed: {SDL_GetError()}");
                    return;
                }
            }
            catch (DllNotFoundException ex)
            {
                // No native SDL3 for this platform/architecture: start without gamepad
                // input rather than failing to open the launcher.
                _sdlLibraryMissing = true;
                System.Diagnostics.Debug.WriteLine($"SDL3 unavailable, gamepad input disabled: {ex.Message}");
                return;
            }

            RefreshConnectedControllers();
        }

        public void SetGamepadEnabled(bool enabled)
        {
            if (enabled && _gamepadTimer == null)
            {
                InitializeGamepadPolling();
            }
            else if (!enabled && _gamepadTimer != null)
            {
                _gamepadTimer.Stop();
                _gamepadTimer = null;
            }
        }

        private void InitializeGamepadPolling()
        {
            _gamepadTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _gamepadTimer.Tick += GamepadTimer_Tick;
            _gamepadTimer.Start();
        }

        private void GamepadTimer_Tick(object? sender, EventArgs e)
        {
            if (!SdlAvailable)
                return;

            CheckSDLWindowFocus();
            SDL_UpdateGamepads();
            RefreshConnectedControllers();

            var overlayActive = IsGamepadOverlayActive;

            if (!_isWindowActive && !overlayActive && !_captureMode)
            {
                ResetNavigationState();
                _lastConfirmTime = DateTime.MinValue;
                _lastCancelTime = DateTime.MinValue;
                return;
            }

            NavigationDirection? heldDirection = null;

            foreach (var kvp in _gameControllers)
            {
                var id = kvp.Key;
                var controller = kvp.Value;

                if (!_gamepadStates.ContainsKey(id))
                    _gamepadStates[id] = new GamepadSample();

                var currentState = _gamepadStates[id];
                var previousState = currentState.Clone();
                SampleController(controller, currentState);

                if (_captureMode)
                {
                    var rising = FindRisingEdgeBinding(previousState, currentState);
                    if (rising != null)
                        OnRawInput?.Invoke(rising);
                    continue;
                }

                if (!heldDirection.HasValue)
                {
                    var direction = GetHeldNavigationDirection(currentState);
                    if (direction.HasValue)
                        heldDirection = direction;
                }

                if (IsActionRisingEdge(GamepadAction.Confirm, previousState, currentState))
                {
                    var now = DateTime.Now;
                    if ((now - _lastConfirmTime).TotalMilliseconds > ButtonRepeatDelay)
                    {
                        _lastConfirmTime = now;
                        if (!TryHandleOverlayConfirm())
                            OnConfirm?.Invoke();
                    }
                }

                if (IsActionRisingEdge(GamepadAction.Cancel, previousState, currentState))
                {
                    var now = DateTime.Now;
                    if ((now - _lastCancelTime).TotalMilliseconds > ButtonRepeatDelay)
                    {
                        _lastCancelTime = now;
                        if (!TryHandleOverlayCancel())
                            OnCancel?.Invoke();
                    }
                }

                if (IsActionRisingEdge(GamepadAction.Options, previousState, currentState))
                {
                    var now = DateTime.Now;
                    if ((now - _lastOptionsTime).TotalMilliseconds > ButtonRepeatDelay)
                    {
                        _lastOptionsTime = now;
                        OnOptions?.Invoke();
                    }
                }
            }

            if (_captureMode)
                return;

            if (heldDirection.HasValue)
                HandleNavigation(heldDirection.Value);
            else
                ResetNavigationHold();
        }

        private static void SampleController(OpenGamepad controller, GamepadSample state)
        {
            state.Pressed.Clear();

            for (int i = 0; i < AllButtons.Length; i++)
            {
                if (controller.GetButton(AllButtons[i]))
                    state.Pressed.Add(new GamepadBinding(GamepadInputKind.Button, controller.ButtonCodes[i]));
            }

            foreach (var axis in AllAxes)
            {
                short raw = controller.GetAxis(axis);
                float value = raw / (float)AxisMax;

                // Triggers are 0..1; sticks are -1..1.
                bool isTrigger = axis is SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER
                    or SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER;

                if (isTrigger)
                {
                    if (value >= DeadZone)
                        state.Pressed.Add(GamepadBinding.AxisPositive(axis));
                    continue;
                }

                if (value >= DeadZone)
                    state.Pressed.Add(GamepadBinding.AxisPositive(axis));
                else if (value <= -DeadZone)
                    state.Pressed.Add(GamepadBinding.AxisNegative(axis));
            }
        }

        private static GamepadBinding? FindRisingEdgeBinding(GamepadSample previous, GamepadSample current)
        {
            foreach (var binding in current.Pressed)
            {
                if (!previous.Pressed.Contains(binding))
                    return binding;
            }

            return null;
        }

        private bool IsActionPressed(GamepadAction action, GamepadSample state)
        {
            if (!_bindings.TryGetValue(action, out var list) || list == null)
                return false;

            foreach (var binding in list)
            {
                if (state.Pressed.Contains(binding))
                    return true;
            }

            return false;
        }

        private bool IsActionRisingEdge(GamepadAction action, GamepadSample previous, GamepadSample current) =>
            IsActionPressed(action, current) && !IsActionPressed(action, previous);

        private NavigationDirection? GetHeldNavigationDirection(GamepadSample state)
        {
            if (IsActionPressed(GamepadAction.NavUp, state))
                return NavigationDirection.Up;
            if (IsActionPressed(GamepadAction.NavDown, state))
                return NavigationDirection.Down;
            if (IsActionPressed(GamepadAction.NavLeft, state))
                return NavigationDirection.Left;
            if (IsActionPressed(GamepadAction.NavRight, state))
                return NavigationDirection.Right;

            return null;
        }
        private unsafe void CheckSDLWindowFocus()
        {
            SDL_Event sdlEvent;

            // Poll all pending SDL events to check for window focus changes
            while (SDL_PollEvent(&sdlEvent))
            {
                switch (sdlEvent.Type)
                {
                    case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_GAINED:
                        _isWindowActive = true;
                        System.Diagnostics.Debug.WriteLine("SDL: Window gained focus");
                        break;

                    case SDL_EventType.SDL_EVENT_WINDOW_FOCUS_LOST:
                        _isWindowActive = false;
                        System.Diagnostics.Debug.WriteLine("SDL: Window lost focus");
                        break;
                }
            }
        }

        public void HandleNavigation(NavigationDirection direction)
        {
            var now = DateTime.Now;
            var timeSinceLastNav = _lastNavigationTime == DateTime.MinValue
                ? 0
                : (now - _lastNavigationTime).TotalMilliseconds;

            if (!GamepadNavigationRepeat.ShouldAllowNavigationMove(
                    _movesInHold,
                    timeSinceLastNav,
                    InitialRepeatDelay,
                    RepeatDelay))
            {
                return;
            }

            _lastNavigationTime = now;
            _movesInHold++;

            if (_comboBoxNavigation.TryHandleNavigation(direction))
            {
                OnNavigate?.Invoke(direction);
                return;
            }

            if (_menuFlyoutNavigation.TryHandleNavigation(direction))
            {
                OnNavigate?.Invoke(direction);
                return;
            }

            if (_contextMenuNavigation.TryHandleNavigation(direction))
            {
                OnNavigate?.Invoke(direction);
                return;
            }

            if (_modalDialogNavigation.TryHandleNavigation(direction))
            {
                OnNavigate?.Invoke(direction);
                return;
            }

            // When MainWindow owns zone navigation, never fall through to FocusFirstElement
            // (that would re-focus Continue while a library card still has IsGamepadFocused).
            if (NavigationInterceptor != null)
            {
                if (NavigationInterceptor.Invoke(direction))
                    OnNavigate?.Invoke(direction);
                return;
            }

            if (!XyFocusNavigation.TryMove(_mainWindow, direction))
            {
                var focused = TopLevel.GetTopLevel(_mainWindow)?.FocusManager?.GetFocusedElement() as Control;
                if (focused == null)
                    FocusFirstElement();
            }

            OnNavigate?.Invoke(direction);
        }

        public void ResetNavigationTimer() => ResetNavigationHold();

        public void ResetNavigationHold()
        {
            _movesInHold = 0;
            _lastNavigationTime = DateTime.MinValue;
        }

        public void ResetNavigationState() => ResetNavigationHold();

        public bool TryHandleContextMenuConfirm() => _contextMenuNavigation.TryHandleConfirm();

        public bool TryHandleContextMenuCancel() => _contextMenuNavigation.TryHandleCancel();

        public bool TryHandleContextMenuOptionsDismiss() => _contextMenuNavigation.TryHandleOptionsDismiss();

        public bool TryHandleComboBoxConfirm() => _comboBoxNavigation.TryHandleConfirm();

        public bool TryHandleComboBoxCancel() => _comboBoxNavigation.TryHandleCancel();

        public bool TryHandleMenuFlyoutConfirm() => _menuFlyoutNavigation.TryHandleConfirm();

        public bool TryHandleMenuFlyoutCancel() => _menuFlyoutNavigation.TryHandleCancel();

        public bool TryHandleModalConfirm() => _modalDialogNavigation.TryHandleConfirm();

        public bool TryHandleModalCancel() => _modalDialogNavigation.TryHandleCancel();

        private bool TryHandleOverlayConfirm()
        {
            if (_comboBoxNavigation.TryHandleConfirm())
                return true;

            if (_contextMenuNavigation.TryHandleConfirm())
                return true;

            if (_modalDialogNavigation.TryHandleConfirm())
                return true;

            return false;
        }

        private bool TryHandleOverlayCancel()
        {
            if (_comboBoxNavigation.TryHandleCancel())
                return true;

            if (_contextMenuNavigation.TryHandleCancel())
                return true;

            if (_modalDialogNavigation.TryHandleCancel())
                return true;

            return false;
        }

        private Control? GetNextControl(Control current, NavigationDirection direction)
        {
            var allControls = GetFocusableControls(_mainWindow);

            if (!allControls.Any())
                return null;

            var currentIndex = allControls.IndexOf(current);
            if (currentIndex == -1)
                return allControls.FirstOrDefault();

            var currentCenter = GetControlCenter(current);
            if (!currentCenter.HasValue)
                return GetNextControlSimple(allControls, currentIndex, direction);

            Control? bestCandidate = null;
            double bestScore = double.MaxValue;

            foreach (var candidate in allControls)
            {
                if (candidate == current)
                    continue;

                var candidateCenter = GetControlCenter(candidate);
                if (!candidateCenter.HasValue)
                    continue;

                var score = CalculateNavigationScore(currentCenter.Value, candidateCenter.Value, direction);
                if (score.HasValue && score.Value < bestScore)
                {
                    bestScore = score.Value;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate ?? GetNextControlWithWrapping(allControls, currentIndex, direction, currentCenter.Value);
        }

        private Avalonia.Point? GetControlCenter(Control control)
        {
            var topLeft = control.TranslatePoint(new Avalonia.Point(0, 0), _mainWindow);
            if (!topLeft.HasValue)
                return null;

            var bounds = control.Bounds;
            return new Avalonia.Point(
                topLeft.Value.X + bounds.Width / 2,
                topLeft.Value.Y + bounds.Height / 2
            );
        }

        private double? CalculateNavigationScore(Avalonia.Point current, Avalonia.Point candidate, NavigationDirection direction)
        {
            double dx = candidate.X - current.X;
            double dy = candidate.Y - current.Y;

            bool isInDirection;
            double primaryDistance;
            double secondaryDistance;

            switch (direction)
            {
                case NavigationDirection.Up:
                    if (dy >= -1) return null;
                    isInDirection = true;
                    primaryDistance = Math.Abs(dy);
                    secondaryDistance = Math.Abs(dx);
                    break;
                case NavigationDirection.Down:
                    if (dy <= 1) return null;
                    isInDirection = true;
                    primaryDistance = Math.Abs(dy);
                    secondaryDistance = Math.Abs(dx);
                    break;
                case NavigationDirection.Left:
                    if (dx >= -1) return null;
                    isInDirection = true;
                    primaryDistance = Math.Abs(dx);
                    secondaryDistance = Math.Abs(dy);
                    break;
                case NavigationDirection.Right:
                    if (dx <= 1) return null;
                    isInDirection = true;
                    primaryDistance = Math.Abs(dx);
                    secondaryDistance = Math.Abs(dy);
                    break;
                default:
                    return null;
            }

            if (!isInDirection)
                return null;

            double offAxisPenalty = secondaryDistance > 10 ? secondaryDistance * 2.5 : 0;
            return primaryDistance + (secondaryDistance * 0.3) + offAxisPenalty;
        }

        private Control? GetNextControlWithWrapping(List<Control> allControls, int currentIndex, NavigationDirection direction, Avalonia.Point currentCenter)
        {
            // Try to wrap around in a smart way based on position
            Control? wrapCandidate = null;
            double bestWrapScore = double.MaxValue;

            foreach (var candidate in allControls)
            {
                var candidateTopLeft = candidate.TranslatePoint(new Avalonia.Point(0, 0), _mainWindow);
                if (!candidateTopLeft.HasValue)
                    continue;

                var candidateBounds = candidate.Bounds;
                var candidateCenter = new Avalonia.Point(
                    candidateTopLeft.Value.X + candidateBounds.Width / 2,
                    candidateTopLeft.Value.Y + candidateBounds.Height / 2
                );

                double score = 0;

                switch (direction)
                {
                    case NavigationDirection.Up:
                        // Wrap to bottom-most control with similar X position
                        score = Math.Abs(candidateCenter.X - currentCenter.X) + (10000 - candidateCenter.Y);
                        break;
                    case NavigationDirection.Down:
                        // Wrap to top-most control with similar X position
                        score = Math.Abs(candidateCenter.X - currentCenter.X) + candidateCenter.Y;
                        break;
                    case NavigationDirection.Left:
                        // Wrap to right-most control with similar Y position
                        score = Math.Abs(candidateCenter.Y - currentCenter.Y) + (10000 - candidateCenter.X);
                        break;
                    case NavigationDirection.Right:
                        // Wrap to left-most control with similar Y position
                        score = Math.Abs(candidateCenter.Y - currentCenter.Y) + candidateCenter.X;
                        break;
                }

                if (score < bestWrapScore)
                {
                    bestWrapScore = score;
                    wrapCandidate = candidate;
                }
            }

            // If wrap candidate is still null or not good, fall back to simple navigation
            if (wrapCandidate == null)
            {
                return GetNextControlSimple(allControls, currentIndex, direction);
            }

            return wrapCandidate;
        }

        private Control? GetNextControlSimple(List<Control> allControls, int currentIndex, NavigationDirection direction)
        {
            switch (direction)
            {
                case NavigationDirection.Down:
                case NavigationDirection.Right:
                    return allControls[(currentIndex + 1) % allControls.Count];

                case NavigationDirection.Up:
                case NavigationDirection.Left:
                    return allControls[(currentIndex - 1 + allControls.Count) % allControls.Count];

                default:
                    return null;
            }
        }

        private List<Control> GetFocusableControls(Control parent)
        {
            var controls = new List<Control>();

            if (parent.IsVisible && parent.IsEnabled && parent.Focusable)
            {
                controls.Add(parent);
            }

            // Check for open context menus
            if (parent is Button button && button.ContextMenu?.IsOpen == true)
            {
                foreach (var menuItem in button.ContextMenu.Items.OfType<MenuItem>())
                {
                    if (menuItem.IsVisible && menuItem.IsEnabled)
                    {
                        controls.Add(menuItem);
                    }
                }
                return controls; // Only return menu items when context menu is open
            }

            foreach (var child in parent.GetVisualChildren().OfType<Control>())
            {
                controls.AddRange(GetFocusableControls(child));
            }

            return controls;
        }

        private void FocusFirstElement()
        {
            var focusable = GetFocusableControls(_mainWindow).FirstOrDefault();
            if (focusable != null)
            {
                focusable.Focus();

                // Special handling for MenuItems
                if (focusable is MenuItem menuItem)
                {
                    menuItem.IsSubMenuOpen = false; // Ensure submenu isn't open
                }
            }
        }

        public void SetWindowActive(bool isActive)
        {
            _isWindowActive = isActive;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _modalDialogNavigation.Unconfigure(this);
                _contextMenuNavigation.Unconfigure(this);
                NavigationInterceptor = null;
                _gamepadTimer?.Stop();
                _gamepadTimer = null;

                foreach (var id in _gameControllers.Keys.ToList())
                    CloseControllerAt(id);

                if (SdlAvailable)
                    SDL_Quit();
                _disposed = true;
            }
        }

        /// <summary>
        /// Saved bindings use SDL2's button numbers, where the four face buttons follow their printed label on
        /// Nintendo-style pads (A is the right button). SDL3 numbers them by position (South, East, West, North),
        /// so each face button is reported as the code of its label to keep existing bindings and on-screen hints
        /// meaning the same button.
        /// </summary>
        internal static int GetButtonCode(SDL_GamepadButton button, SDL_GamepadButtonLabel label) => label switch
        {
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_A or SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_CROSS
                => (int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH,
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_B or SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_CIRCLE
                => (int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST,
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_X or SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_SQUARE
                => (int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST,
            SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_Y or SDL_GamepadButtonLabel.SDL_GAMEPAD_BUTTON_LABEL_TRIANGLE
                => (int)SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH,
            _ => (int)button,
        };

        /// <summary>An open SDL3 gamepad, with the binding code each of <see cref="AllButtons"/> reports.</summary>
        private sealed unsafe class OpenGamepad
        {
            private SDL_Gamepad* _handle;

            private OpenGamepad(SDL_Gamepad* handle)
            {
                _handle = handle;
                Name = SDL_GetGamepadName(handle);
                ButtonCodes = AllButtons.Select(b => GetButtonCode(b, SDL_GetGamepadButtonLabel(handle, b))).ToArray();
            }

            public string? Name { get; }

            public int[] ButtonCodes { get; }

            public bool IsConnected => _handle != null && SDL_GamepadConnected(_handle);

            public static OpenGamepad? Open(SDL_JoystickID id)
            {
                var handle = SDL_OpenGamepad(id);
                return handle == null ? null : new OpenGamepad(handle);
            }

            public bool GetButton(SDL_GamepadButton button) => SDL_GetGamepadButton(_handle, button);

            public short GetAxis(SDL_GamepadAxis axis) => SDL_GetGamepadAxis(_handle, axis);

            public void Close()
            {
                if (_handle == null)
                    return;

                SDL_CloseGamepad(_handle);
                _handle = null;
            }
        }

        private class GamepadSample
        {
            public HashSet<GamepadBinding> Pressed { get; } = new();

            public GamepadSample Clone()
            {
                var clone = new GamepadSample();
                foreach (var binding in Pressed)
                    clone.Pressed.Add(binding);
                return clone;
            }
        }
    }

    public enum NavigationDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
