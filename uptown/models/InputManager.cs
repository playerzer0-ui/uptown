using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace NodeTesting.models
{
    /// <summary>
    /// A named game action (e.g. "Jump", "MoveLeft").
    /// Holds one optional keyboard key and one optional gamepad button binding.
    /// </summary>
    public class InputAction
    {
        public string Name { get; }
        public Keys? Key { get; set; }
        public Buttons? Button { get; set; }

        public InputAction(string name, Keys? key = null, Buttons? button = null)
        {
            Name = name;
            Key = key;
            Button = button;
        }
    }

    /// <summary>
    /// Centralised input manager. Handles keyboard and gamepad state,
    /// old-state tracking, and named rebindable actions.
    ///
    /// <para>Call <see cref="Update"/> once at the top of your game's Update loop.</para>
    ///
    /// <para>Then query actions with:
    /// <list type="bullet">
    ///   <item><see cref="IsPressed"/>  – held down this frame</item>
    ///   <item><see cref="JustPressed"/> – first frame the input went down</item>
    ///   <item><see cref="JustReleased"/> – first frame the input came back up</item>
    /// </list>
    /// </para>
    /// </summary>
    public class InputManager
    {
        // ── State ──────────────────────────────────────────────────────────────
        private KeyboardState currentKeys;
        private KeyboardState previousKeys;
        private GamePadState currentPad;
        private GamePadState previousPad;

        private readonly PlayerIndex playerIndex;
        private readonly Dictionary<string, InputAction> actions = new();

        // ── Constructor ────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new InputManager for the given player index (default Player One).
        /// </summary>
        public InputManager(PlayerIndex playerIndex = PlayerIndex.One)
        {
            this.playerIndex = playerIndex;
        }

        // ── Registration ───────────────────────────────────────────────────────

        /// <summary>
        /// Registers a new named action with optional keyboard and gamepad bindings.
        /// </summary>
        /// <example>
        /// <code>
        /// input.Register("Jump",     Keys.Space,  Buttons.A);
        /// input.Register("MoveLeft", Keys.A,      Buttons.DPadLeft);
        /// input.Register("Pause",    Keys.Escape, Buttons.Start);
        /// </code>
        /// </example>
        public void Register(string name, Keys? key = null, Buttons? button = null)
        {
            actions[name] = new InputAction(name, key, button);
        }

        // ── Rebinding ──────────────────────────────────────────────────────────

        /// <summary>Rebinds the keyboard key for an existing action.</summary>
        public void RebindKey(string name, Keys newKey)
        {
            if (actions.TryGetValue(name, out var action))
                action.Key = newKey;
        }

        /// <summary>Rebinds the gamepad button for an existing action.</summary>
        public void RebindButton(string name, Buttons newButton)
        {
            if (actions.TryGetValue(name, out var action))
                action.Button = newButton;
        }

        /// <summary>Clears the keyboard binding for an action.</summary>
        public void ClearKey(string name)
        {
            if (actions.TryGetValue(name, out var action))
                action.Key = null;
        }

        /// <summary>Clears the gamepad binding for an action.</summary>
        public void ClearButton(string name)
        {
            if (actions.TryGetValue(name, out var action))
                action.Button = null;
        }

        /// <summary>
        /// Returns the current bindings for an action, or null if not found.
        /// </summary>
        public InputAction GetAction(string name)
        {
            actions.TryGetValue(name, out var action);
            return action;
        }

        // ── Update ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Must be called once at the top of your game's Update loop.
        /// Snapshots current and previous input states.
        /// </summary>
        public void Update()
        {
            previousKeys = currentKeys;
            previousPad = currentPad;
            currentKeys = Keyboard.GetState();
            currentPad = GamePad.GetState(playerIndex);
        }

        // ── Action queries ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns true while the action's key or button is held down.
        /// </summary>
        public bool IsPressed(string name)
        {
            if (!actions.TryGetValue(name, out var action)) return false;
            return KeyDown(action.Key) || ButtonDown(action.Button);
        }

        /// <summary>
        /// Returns true only on the first frame the action's key or button goes down.
        /// </summary>
        public bool JustPressed(string name)
        {
            if (!actions.TryGetValue(name, out var action)) return false;
            return KeyJustDown(action.Key) || ButtonJustDown(action.Button);
        }

        /// <summary>
        /// Returns true only on the first frame the action's key or button comes back up.
        /// </summary>
        public bool JustReleased(string name)
        {
            if (!actions.TryGetValue(name, out var action)) return false;
            return KeyJustUp(action.Key) || ButtonJustUp(action.Button);
        }

        // ── Raw keyboard queries ───────────────────────────────────────────────

        /// <summary>Raw check: key is held this frame.</summary>
        public bool KeyDown(Keys? key) =>
            key.HasValue && currentKeys.IsKeyDown(key.Value);

        /// <summary>Raw check: key was just pressed this frame.</summary>
        public bool KeyJustDown(Keys? key) =>
            key.HasValue &&
            currentKeys.IsKeyDown(key.Value) &&
            previousKeys.IsKeyUp(key.Value);

        /// <summary>Raw check: key was just released this frame.</summary>
        public bool KeyJustUp(Keys? key) =>
            key.HasValue &&
            currentKeys.IsKeyUp(key.Value) &&
            previousKeys.IsKeyDown(key.Value);

        // ── Raw gamepad queries ────────────────────────────────────────────────

        /// <summary>Raw check: button is held this frame.</summary>
        public bool ButtonDown(Buttons? button) =>
            button.HasValue && currentPad.IsButtonDown(button.Value);

        /// <summary>Raw check: button was just pressed this frame.</summary>
        public bool ButtonJustDown(Buttons? button) =>
            button.HasValue &&
            currentPad.IsButtonDown(button.Value) &&
            previousPad.IsButtonUp(button.Value);

        /// <summary>Raw check: button was just released this frame.</summary>
        public bool ButtonJustUp(Buttons? button) =>
            button.HasValue &&
            currentPad.IsButtonUp(button.Value) &&
            previousPad.IsButtonDown(button.Value);

        // ── Analog sticks ──────────────────────────────────────────────────────

        /// <summary>Returns the left thumbstick vector (-1 to 1 on each axis).</summary>
        public Vector2 LeftStick => currentPad.ThumbSticks.Left;

        /// <summary>Returns the right thumbstick vector (-1 to 1 on each axis).</summary>
        public Vector2 RightStick => currentPad.ThumbSticks.Right;

        /// <summary>Returns the left trigger value (0 to 1).</summary>
        public float LeftTrigger => currentPad.Triggers.Left;

        /// <summary>Returns the right trigger value (0 to 1).</summary>
        public float RightTrigger => currentPad.Triggers.Right;

        /// <summary>Returns true if a gamepad is connected for this player.</summary>
        public bool IsGamePadConnected => currentPad.IsConnected;
    }
}