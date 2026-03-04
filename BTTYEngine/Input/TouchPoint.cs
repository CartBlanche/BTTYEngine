using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;

namespace BTTYEngine
{
    /// <summary>
    /// A single touch contact point for the current frame.
    /// </summary>
    public readonly struct TouchPoint
    {
        /// <summary>Unique identifier for this touch contact.</summary>
        public readonly int Id;
        /// <summary>Screen-space position of the touch.</summary>
        public readonly Vector2 Position;
        /// <summary>Movement delta since the previous frame.</summary>
        public readonly Vector2 Delta;
        /// <summary>MonoGame touch state (Pressed, Moved, Released, Invalid).</summary>
        public readonly TouchLocationState State;

        public TouchPoint(int id, Vector2 position, Vector2 delta, TouchLocationState state)
        {
            Id       = id;
            Position = position;
            Delta    = delta;
            State    = state;
        }
    }
}
