using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System.Collections.Generic;

namespace BTTYEngine
{
    /// <summary>
    /// Snapshot of all input device states for a single frame.
    /// </summary>
    public class InputState
    {
        public GamePadState   PadState;
        public KeyboardState  KeyState;
        public MouseState     MouseState;
        public TouchCollection Touches;

        // Gestures are consumed when read, so they are collected into a list each frame.
        public readonly List<GestureSample> Gestures = new List<GestureSample>();

        /// <summary>Captures all device states into this snapshot.</summary>
        public void Capture()
        {
            PadState   = GamePad.GetState(PlayerIndex.One);
            KeyState   = Keyboard.GetState();
            MouseState = Mouse.GetState();
            Touches    = TouchPanel.GetState();

            Gestures.Clear();
            while (TouchPanel.IsGestureAvailable)
                Gestures.Add(TouchPanel.ReadGesture());
        }

        /// <summary>Copies state from <paramref name="other"/> into this snapshot.</summary>
        public void CopyFrom(InputState other)
        {
            PadState   = other.PadState;
            KeyState   = other.KeyState;
            MouseState = other.MouseState;
            Touches    = other.Touches;

            Gestures.Clear();
            Gestures.AddRange(other.Gestures);
        }
    }
}
