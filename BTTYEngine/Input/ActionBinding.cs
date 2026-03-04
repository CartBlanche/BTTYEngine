using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System.Collections.Generic;

namespace BTTYEngine
{
    /// <summary>
    /// Per-action binding record. Multiple keys, buttons, mouse buttons, and/or a single
    /// analogue axis can be bound to one action; any active binding activates the action.
    /// </summary>
    internal class ActionBinding
    {
        public readonly List<Keys>       BoundKeys         = new List<Keys>();
        public readonly List<Buttons>    BoundButtons      = new List<Buttons>();
        public readonly List<MouseButton> BoundMouseButtons = new List<MouseButton>();
        public readonly List<GestureType> BoundGestures    = new List<GestureType>();

        // Gamepad analogue axis binding, AxisSign == 0 means no axis bound.
        public GamePadAxis BoundAxis;
        public float       AxisSign     = 0f;    // +1 or -1; 0 = unbound
        public float       AxisDeadzone = 0.15f;

        // Mouse axis binding, MouseAxisSign == 0 means no mouse axis bound.
        public MouseAxis BoundMouseAxis;
        public float     MouseAxisSign     = 0f;   // +1 or -1; 0 = unbound
        public float     MouseAxisDeadzone = 0f;   // typically 0 for scroll/delta
    }
}
