namespace BTTYEngine
{
    /// <summary>Mouse button identifiers for input binding.</summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        X1,
        X2,
    }

    /// <summary>
    /// Mouse analogue axis sources that can be bound to actions.
    /// DeltaX/DeltaY are the mouse movement delta this frame.
    /// ScrollWheel is the scroll wheel delta this frame.
    /// </summary>
    public enum MouseAxis
    {
        DeltaX,
        DeltaY,
        ScrollWheel,
    }
}
