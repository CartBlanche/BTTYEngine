using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Abstract base for all BTTYEngine cameras.
    /// Holds the matrices, frustum, and position/target that every camera type shares.
    /// Subclasses implement Update() to define their specific movement and projection behaviour.
    /// </summary>
    public abstract class BaseCamera : ICamera
    {
        // ── ICamera ─────────────────────────────────────────────────────────────

        public Matrix WorldMatrix      { get; protected set; }
        public Matrix ViewMatrix       { get; protected set; }
        public Matrix ProjectionMatrix { get; protected set; }
        public BoundingFrustum BoundingFrustum { get; protected set; }

        public Vector3 Position { get; set; }
        public Vector3 Target   { get; set; }

        // ── Engine internals ─────────────────────────────────────────────────────

        protected GraphicsDevice GraphicsDevice { get; }
        protected Viewport       Viewport       { get; }

        // ── Constructor ──────────────────────────────────────────────────────────

        protected BaseCamera(GraphicsDevice graphicsDevice, Viewport viewport)
        {
            GraphicsDevice = graphicsDevice;
            Viewport       = viewport;

            // Safe default – subclasses are expected to override in their constructor
            WorldMatrix      = Matrix.Identity;
            ViewMatrix       = Matrix.Identity;
            ProjectionMatrix = Matrix.Identity;
            BoundingFrustum  = new BoundingFrustum(Matrix.Identity);
        }

        // ── Abstract contract ────────────────────────────────────────────────────

        /// <summary>
        /// Update view matrices, frustum, and any camera-specific interpolation.
        /// Called once per frame by the game loop.
        /// </summary>
        public abstract void Update(GameTime gameTime, VoxelWorld world);
    }
}
