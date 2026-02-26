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
            // WorldMatrix = YFlipMatrix (Y-up convention) inherited from BaseCamera

            // Initial rendering view — negated Y so world space is Y-up
            ViewMatrix = Matrix.CreateLookAt(
                new Vector3(0f, 150f, -150f),   // ToWorldSpace of (0, -150, -150)
                new Vector3(0f, 0f,   100f),
                Vector3.Up);

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
            // Smooth-follow: lerp the eye position toward the scroll target (game space)
            Position = Vector3.Lerp(Position, Target, MoveSpeed);

            // ── Rendering view (Y-up world space) ────────────────────────────────
            // Convert game-space Position to Y-up world space, then place the eye
            // at Offset from that point and look back at it with Vector3.Up.
            Vector3 renderPos = ToWorldSpace(Position);
            ViewMatrix = Matrix.CreateLookAt(renderPos + Offset, renderPos, Vector3.Up);

            // ── Culling frustum (game-space Y-down) ───────────────────────────────
            // Legacy collision and chunk-visibility code uses game-space coordinates,
            // so BoundingFrustum is rebuilt from the un-negated position + Vector3.Down.
            Matrix cullingView = Matrix.CreateLookAt(Position + Offset, Position, Vector3.Down);
            BoundingFrustum.Matrix = cullingView * ProjectionMatrix;
        }
    }
}
