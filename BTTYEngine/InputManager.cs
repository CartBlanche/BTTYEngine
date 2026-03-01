using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace VoxelShooter
{
    /// <summary>
    /// Snapshot of keyboard and gamepad state for a single frame.
    /// </summary>
    public class InputState
    {
        public GamePadState PadState;
        public KeyboardState KeyState;

        public void Capture()
        {
            PadState = GamePad.GetState(PlayerIndex.One);
            KeyState = Keyboard.GetState();
        }

        public void CopyFrom(InputState other)
        {
            PadState = other.PadState;
            KeyState = other.KeyState;
        }
    }

    /// <summary>
    /// Centralised input manager. Call BeginInputProcessing() at the top of Update
    /// and EndInputProcessing() at the bottom. All input queries go through here.
    /// Keyboard and gamepad are treated as equal, whichever is active wins.
    /// </summary>
    public class InputManager
    {
        InputState current = new InputState();
        InputState last    = new InputState();

        public InputState CurrentState => current;
        public InputState LastState    => last;

        /// <summary>Call at the start of Update to latch this frame's input.</summary>
        public void BeginInputProcessing()
        {
            current.Capture();
        }

        /// <summary>Call at the end of Update to roll current into last.</summary>
        public void EndInputProcessing()
        {
            last.CopyFrom(current);
        }

        // ── Keyboard helpers ─────────────────────────────────────────────────

        /// <summary>Key is held this frame.</summary>
        public bool IsKeyDown(Keys key) => current.KeyState.IsKeyDown(key);

        /// <summary>Key went down this frame (was up last frame).</summary>
        public bool IsKeyPressed(Keys key) =>
            current.KeyState.IsKeyDown(key) && last.KeyState.IsKeyUp(key);

        // ── Gamepad button helpers ────────────────────────────────────────────

        /// <summary>Button is held this frame.</summary>
        public bool IsButtonDown(Buttons button) =>
            current.PadState.IsButtonDown(button);

        /// <summary>Button was pressed this frame (was up last frame).</summary>
        public bool IsButtonPressed(Buttons button) =>
            current.PadState.IsButtonDown(button) &&
            last.PadState.IsButtonUp(button);

        // ── Analogue ──────────────────────────────────────────────────────────

        /// <summary>Left thumbstick position (zero if no gamepad connected).</summary>
        public Vector2 LeftStick => current.PadState.ThumbSticks.Left;

        /// <summary>Right trigger value 0–1.</summary>
        public float RightTrigger => current.PadState.Triggers.Right;

        // ── Game-level queries ────────────────────────────────────────────────

        /// <summary>
        /// Movement direction combining keyboard (WASD / arrow keys) and gamepad
        /// left stick. Keyboard takes priority if both are active.
        /// Y-up convention: W/up = positive Y, S/down = negative Y.
        /// </summary>
        public Vector2 MoveDirection()
        {
            var dir = Vector2.Zero;

            // Keyboard
            if (current.KeyState.IsKeyDown(Keys.W) || current.KeyState.IsKeyDown(Keys.Up))    dir.Y =  1;
            if (current.KeyState.IsKeyDown(Keys.S) || current.KeyState.IsKeyDown(Keys.Down))  dir.Y = -1;
            if (current.KeyState.IsKeyDown(Keys.A) || current.KeyState.IsKeyDown(Keys.Left))  dir.X = -1;
            if (current.KeyState.IsKeyDown(Keys.D) || current.KeyState.IsKeyDown(Keys.Right)) dir.X =  1;

            // Gamepad left stick (only if keyboard is idle)
            if (dir == Vector2.Zero)
            {
                var stick = current.PadState.ThumbSticks.Left;
                if (stick.LengthSquared() > 0.01f)
                {
                    dir.X = stick.X;
                    dir.Y = stick.Y; // ThumbSticks.Left.Y is already +1 when pushed up
                }
            }

            return dir;
        }

        /// <summary>
        /// True while the player is holding the fire input,
        /// keyboard Z, gamepad right trigger (>10%), or gamepad A button.
        /// </summary>
        public bool IsFiring() =>
            current.KeyState.IsKeyDown(Keys.Z) ||
            current.PadState.Triggers.Right > 0.1f ||
            current.PadState.IsButtonDown(Buttons.A);

        /// <summary>
        /// True if the player wants to quit,
        /// keyboard Escape, or gamepad Back button.
        /// </summary>
        public bool IsExiting() =>
            current.KeyState.IsKeyDown(Keys.Escape) ||
            current.PadState.Buttons.Back == ButtonState.Pressed;

        // ── Camera selection (keyboard 1-4, gamepad shoulder buttons) ────────────

        /// <summary>Direct select: keyboard 1–4 selects the corresponding camera.</summary>
        public bool IsCameraSelectPressed(int cameraIndex)
        {
            Keys key = cameraIndex switch
            {
                1 => Keys.D1,
                2 => Keys.D2,
                3 => Keys.D3,
                4 => Keys.D4,
                _ => Keys.None,
            };
            return key != Keys.None && IsKeyPressed(key);
        }

        /// <summary>Cycle to next camera: gamepad RightShoulder (RB/R1).</summary>
        public bool IsCameraNextPressed() => IsButtonPressed(Buttons.RightShoulder);

        /// <summary>Cycle to previous camera: gamepad LeftShoulder (LB/L1).</summary>
        public bool IsCameraPrevPressed() => IsButtonPressed(Buttons.LeftShoulder);
    }
}
