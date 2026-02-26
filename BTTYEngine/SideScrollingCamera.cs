using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// A side-scrolling perspective camera for VoxelShooter (and similar 2.5-D voxel games).
    ///
    /// The camera eye sits behind and above the scene (controlled by <see cref="Offset"/>),
    /// looks toward <see cref="BaseCamera.Position"/>, and smoothly lerps Position toward
    /// <see cref="BaseCamera.Target"/> each frame.  The game loop drives the Target X-coordinate
    /// to achieve horizontal auto-scrolling.
    ///
    /// Phase 2 will add an IsometricCamera that shares the same BaseCamera contract.
    /// </summary>
    public class SideScrollingCamera : BaseCamera
    {
        // ── Camera orientation ────────────────────────────────────────────────────

        public float Yaw   = MathHelper.Pi;
        public float Roll  = MathHelper.Pi;
        public float Pitch = -0.2f;

        /// <summary>
        /// Eye offset from the tracked position.
        /// Negative Z pushes the eye back (away from the scene in screen-space).
        /// </summary>
        public Vector3 Offset = new Vector3(0f, 0f, -95f);

        // ── Tuning constants ──────────────────────────────────────────────────────

        /// <summary>
        /// How quickly Position lerps toward Target each frame (0 = frozen, 1 = instant).
        /// </summary>
        public float MoveSpeed = 0.05f;

        // ── Constructor ───────────────────────────────────────────────────────────

        public SideScrollingCamera(GraphicsDevice graphicsDevice, Viewport viewport)
            : base(graphicsDevice, viewport)
        {
            WorldMatrix = Matrix.CreateWorld(Vector3.Zero, Vector3.Forward, Vector3.Up);

            // Initial view – matches the original Camera class defaults
            ViewMatrix = Matrix.CreateLookAt(
                new Vector3(0f, 0f, -150f),
                new Vector3(0f, 0f,  100f),
                Vector3.Down);

            ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.PiOver4,
                viewport.AspectRatio,
                0.1f,
                300f);

            BoundingFrustum = new BoundingFrustum(ViewMatrix * ProjectionMatrix);
        }

        // ── ICamera / BaseCamera ──────────────────────────────────────────────────

        public override void Update(GameTime gameTime, VoxelWorld world)
        {
            // Smooth-follow: lerp the eye position toward the scroll target
            Position = Vector3.Lerp(Position, Target, MoveSpeed);

            // Rebuild view: eye is at Position+Offset, looking at Position, Y-down-is-up
            // (the scene is oriented Z-forward, so Vector3.Down gives the correct screen-up)
            ViewMatrix = Matrix.CreateLookAt(Position + Offset, Position, Vector3.Down);

            BoundingFrustum.Matrix = ViewMatrix * ProjectionMatrix;
        }
    }
}
