using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Backward-compatibility shim, prefer <see cref="SideScrollingCamera"/> for new code.
    /// </summary>
    [System.Obsolete("Use SideScrollingCamera (or another BaseCamera subclass) directly.")]
    public class Camera : SideScrollingCamera
    {
        public Camera(GraphicsDevice gd, Viewport vp) : base(gd, vp) { }
    }
}
