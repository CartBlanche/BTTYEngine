using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Abstract base for all BTTYEngine cameras.
    /// Holds the matrices, frustum, and position/target that every camera type shares.
    /// Subclasses implement Update() to define their specific movement and projection behaviour.
    ///
    /// COORDINATE CONVENTION
    /// ─────────────────────
    /// The legacy VoxelShooter world uses Y-down game space (positive Y = down on screen).
    /// BTTYEngine normalises to the industry-standard Y-up convention at the GPU boundary:
    ///
    ///   • <see cref="WorldMatrix"/>  — always <c>Scale(1, -1, 1)</c>.  Flips geometry Y
    ///     at draw time so the GPU sees a standard Y-up world.
    ///   • <see cref="ViewMatrix"/>   — Y-up rendering view.  Computed by negating Position.Y
    ///     before passing to CreateLookAt, with <c>Vector3.Up</c> as the camera-up hint.
    ///   • <see cref="BoundingFrustum"/> — game-space culling frustum (Y-down).  Computed
    ///     from the un-negated Position with <c>Vector3.Down</c> so legacy collision and
    ///     chunk-culling code continues to work without modification.
    ///   • <see cref="Position"/> / <see cref="Target"/> — always in game space (Y-down).
    ///
    /// New systems (BEPU physics, MagicaVoxel importer) should work in world space (Y-up)
    /// and use <see cref="ToWorldSpace"/> to convert game-space vectors when needed.
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

        /// <summary>
        /// The Y-flip applied to all rendered geometry so the GPU sees standard Y-up.
        /// All camera subclasses must use this as their WorldMatrix.
        /// </summary>
        protected static readonly Matrix YFlipMatrix = Matrix.CreateScale(1f, -1f, 1f);

        // ── Constructor ──────────────────────────────────────────────────────────

        protected BaseCamera(GraphicsDevice graphicsDevice, Viewport viewport)
        {
            GraphicsDevice = graphicsDevice;
            Viewport       = viewport;

            WorldMatrix      = YFlipMatrix;
            ViewMatrix       = Matrix.Identity;
            ProjectionMatrix = Matrix.Identity;
            BoundingFrustum  = new BoundingFrustum(Matrix.Identity);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Converts a game-space position (Y-down) to Y-up world space for use in
        /// CreateLookAt and other rendering calculations.
        /// </summary>
        protected static Vector3 ToWorldSpace(Vector3 gamePos)
            => new Vector3(gamePos.X, -gamePos.Y, gamePos.Z);

        // ── Abstract contract ────────────────────────────────────────────────────

        /// <summary>
        /// Update view matrices, frustum, and any camera-specific interpolation.
        /// Called once per frame by the game loop.
        /// </summary>
        public abstract void Update(GameTime gameTime, VoxelWorld world);
    }
}
