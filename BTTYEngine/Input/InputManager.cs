using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Collections.Generic;

namespace BTTYEngine
{
    /// <summary>
    /// Game agnostic input manager. <typeparamref name="TAction"/> is a game-defined enum
    /// that describes logical actions (e.g. Fire, MoveLeft). Physical keys and buttons are
    /// bound to actions by the game in LoadContent, the engine never hardcodes any key.
    /// <para/>
    /// </summary>
    public class InputManager<TAction> where TAction : Enum
    {
        InputState current = new InputState();
        InputState last    = new InputState();

        readonly Dictionary<int, ActionBinding> bindings = new Dictionary<int, ActionBinding>();

        // Binding setup

        /// <summary>Bind one or more keyboard keys to an action.</summary>
        public void Bind(TAction action, params Keys[] keys)
        {
            var b = GetOrAdd(action);
            foreach (var k in keys)
                if (!b.BoundKeys.Contains(k)) b.BoundKeys.Add(k);
        }

        /// <summary>Bind one or more mouse buttons to an action.</summary>
        public void Bind(TAction action, params MouseButton[] buttons)
        {
            var b = GetOrAdd(action);
            foreach (var btn in buttons)
                if (!b.BoundMouseButtons.Contains(btn)) b.BoundMouseButtons.Add(btn);
        }

        /// <summary>Bind one or more gamepad buttons to an action.</summary>
        public void Bind(TAction action, params Buttons[] buttons)
        {
            var b = GetOrAdd(action);
            foreach (var btn in buttons)
                if (!b.BoundButtons.Contains(btn)) b.BoundButtons.Add(btn);
        }

        /// <summary>
        /// Bind a mouse analogue axis to an action.
        /// <paramref name="sign"/> is +1 for positive direction (scroll up, move right/down)
        /// or -1 for negative. Scroll wheel and mouse delta are frame deltas, not cumulative.
        /// </summary>
        public void BindMouseAxis(TAction action, MouseAxis axis, float sign = 1f, float deadzone = 0f)
        {
            var b = GetOrAdd(action);
            b.BoundMouseAxis      = axis;
            b.MouseAxisSign       = sign;
            b.MouseAxisDeadzone   = deadzone;
        }

        /// <summary>
        /// Bind a touch gesture type to an action.
        /// Automatically enables the gesture on <see cref="TouchPanel"/> so it will be recognised.
        /// </summary>
        public void BindGesture(TAction action, GestureType gesture)
        {
            GetOrAdd(action).BoundGestures.Add(gesture);
            TouchPanel.EnabledGestures |= gesture;
        }

        /// <summary>
        /// Bind a gamepad analogue axis to an action.
        /// <paramref name="sign"/> is +1 for the positive half-axis (e.g. stick right / trigger pulled)
        /// or -1 for the negative half-axis (e.g. stick left). The action is considered active when
        /// the signed axis value exceeds <paramref name="deadzone"/>.
        /// </summary>
        public void BindAxis(TAction action, GamePadAxis axis, float sign = 1f, float deadzone = 0.15f)
        {
            var b = GetOrAdd(action);
            b.BoundAxis     = axis;
            b.AxisSign      = sign;
            b.AxisDeadzone  = deadzone;
        }

        // Digital queries

        /// <summary>True while any of the action's bindings are held.</summary>
        public bool IsHeld(TAction action)
        {
            if (!bindings.TryGetValue(ToInt(action), out var b)) return false;
            return IsActiveInFull(b, current, last);
        }

        /// <summary>True only on the frame the action's binding first becomes active (rising edge).</summary>
        public bool IsPressed(TAction action)
        {
            if (!bindings.TryGetValue(ToInt(action), out var b)) return false;
            return IsActiveIn(b, current) && !IsActiveIn(b, last);
        }

        // Analogue queries

        /// <summary>
        /// Analogue value for an action in the range 0–1 (or unbounded for mouse delta).
        /// Returns the axis value if an analogue axis is bound and active,
        /// 1.0 if any bound key, button, or mouse button is held, 0.0 otherwise.
        /// </summary>
        public float GetValue(TAction action)
        {
            if (!bindings.TryGetValue(ToInt(action), out var b)) return 0f;
            if (b.AxisSign != 0f)
            {
                float v = ReadAxis(current.PadState, b.BoundAxis) * b.AxisSign;
                if (v > b.AxisDeadzone) return v;
            }
            if (b.MouseAxisSign != 0f)
            {
                float v = ReadMouseAxis(current, last, b.BoundMouseAxis) * b.MouseAxisSign;
                if (v > b.MouseAxisDeadzone) return v;
            }
            foreach (var k in b.BoundKeys)
                if (current.KeyState.IsKeyDown(k)) return 1f;
            foreach (var btn in b.BoundButtons)
                if (current.PadState.IsButtonDown(btn)) return 1f;
            foreach (var mb in b.BoundMouseButtons)
                if (IsMouseButtonDown(current.MouseState, mb)) return 1f;
            return 0f;
        }

        /// <summary>
        /// Returns a 2D movement vector by combining four directional actions.
        /// Works with both digital (key/button) and analogue (axis) bindings.
        /// </summary>
        public Vector2 GetAxis2D(TAction left, TAction right, TAction down, TAction up)
        {
            return new Vector2(
                GetValue(right) - GetValue(left),
                GetValue(up)    - GetValue(down));
        }

        // Direct mouse queries (positional, not mapped to actions)

        /// <summary>Mouse cursor position in screen pixels this frame.</summary>
        public Point GetMousePosition() => current.MouseState.Position;

        /// <summary>
        /// Mouse movement delta in pixels since the previous frame.
        /// Positive X = moved right; positive Y = moved down (screen space).
        /// </summary>
        public Vector2 GetMouseDelta() =>
            new Vector2(
                current.MouseState.X - last.MouseState.X,
                current.MouseState.Y - last.MouseState.Y);

        /// <summary>
        /// Scroll wheel delta this frame (+ve = scrolled up, -ve = scrolled down).
        /// </summary>
        public int GetMouseScrollDelta() =>
            current.MouseState.ScrollWheelValue - last.MouseState.ScrollWheelValue;

        // Direct touch queries

        /// <summary>
        /// All active touch contact points this frame as a read-only snapshot.
        /// Use this for virtual joystick / touch UI logic that doesn't fit into action bindings.
        /// </summary>
        public IReadOnlyList<TouchPoint> GetTouches()
        {
            var result = new List<TouchPoint>(current.Touches.Count);
            foreach (var t in current.Touches)
            {
                // Compute delta against the matching contact in the previous frame.
                Vector2 delta = Vector2.Zero;
                foreach (var prev in last.Touches)
                    if (prev.Id == t.Id) { delta = t.Position - prev.Position; break; }
                result.Add(new TouchPoint(t.Id, t.Position, delta, t.State));
            }
            return result;
        }

        // Raw state (escape hatch)

        /// <summary>Raw input state captured at the start of this frame.</summary>
        public InputState CurrentState => current;
        /// <summary>Raw input state from the previous frame.</summary>
        public InputState LastState    => last;

        // Internal helpers

        static float ReadMouseAxis(InputState state, InputState prev, MouseAxis axis)
        {
            switch (axis)
            {
                case MouseAxis.DeltaX:     return state.MouseState.X - prev.MouseState.X;
                case MouseAxis.DeltaY:     return state.MouseState.Y - prev.MouseState.Y;
                case MouseAxis.ScrollWheel: return state.MouseState.ScrollWheelValue - prev.MouseState.ScrollWheelValue;
                default:                   return 0f;
            }
        }

        static bool IsMouseButtonDown(MouseState ms, MouseButton btn)
        {
            switch (btn)
            {
                case MouseButton.Left:   return ms.LeftButton   == ButtonState.Pressed;
                case MouseButton.Right:  return ms.RightButton  == ButtonState.Pressed;
                case MouseButton.Middle: return ms.MiddleButton == ButtonState.Pressed;
                case MouseButton.X1:     return ms.XButton1     == ButtonState.Pressed;
                case MouseButton.X2:     return ms.XButton2     == ButtonState.Pressed;
                default:                 return false;
            }
        }

        static int ToInt(TAction a) => (int)(object)a;

        ActionBinding GetOrAdd(TAction action)
        {
            int key = ToInt(action);
            if (!bindings.TryGetValue(key, out var b))
                bindings[key] = b = new ActionBinding();
            return b;
        }

        static float ReadAxis(GamePadState pad, GamePadAxis axis) => axis switch
        {
            GamePadAxis.LeftStickX   => pad.ThumbSticks.Left.X,
            GamePadAxis.LeftStickY   => pad.ThumbSticks.Left.Y,
            GamePadAxis.RightStickX  => pad.ThumbSticks.Right.X,
            GamePadAxis.RightStickY  => pad.ThumbSticks.Right.Y,
            GamePadAxis.LeftTrigger  => pad.Triggers.Left,
            GamePadAxis.RightTrigger => pad.Triggers.Right,
            _                        => 0f,
        };

        bool IsActiveInFull(ActionBinding b, InputState state, InputState prev)
        {
            if (b.AxisSign != 0f && ReadAxis(state.PadState, b.BoundAxis) * b.AxisSign > b.AxisDeadzone)
                return true;
            if (b.MouseAxisSign != 0f && ReadMouseAxis(state, prev, b.BoundMouseAxis) * b.MouseAxisSign > b.MouseAxisDeadzone)
                return true;
            foreach (var k in b.BoundKeys)
                if (state.KeyState.IsKeyDown(k)) return true;
            foreach (var btn in b.BoundButtons)
                if (state.PadState.IsButtonDown(btn)) return true;
            foreach (var mb in b.BoundMouseButtons)
                if (IsMouseButtonDown(state.MouseState, mb)) return true;
            foreach (var g in b.BoundGestures)
                foreach (var gs in state.Gestures)
                    if (gs.GestureType == g) return true;
            return false;
        }

        static bool IsActiveIn(ActionBinding b, InputState state)
        {
            // Used for edge detection, mouse delta axes are intentionally excluded here
            // (delta is always relative to the previous frame; comparing two snapshots
            // from the same frame would always yield zero).
            if (b.AxisSign != 0f && ReadAxis(state.PadState, b.BoundAxis) * b.AxisSign > b.AxisDeadzone)
                return true;
            foreach (var k in b.BoundKeys)
                if (state.KeyState.IsKeyDown(k)) return true;
            foreach (var btn in b.BoundButtons)
                if (state.PadState.IsButtonDown(btn)) return true;
            foreach (var mb in b.BoundMouseButtons)
                if (IsMouseButtonDown(state.MouseState, mb)) return true;
            return false;
        }

        public void Update()
        {
            last.CopyFrom(current);
            current.Capture();
        }
    }
}
