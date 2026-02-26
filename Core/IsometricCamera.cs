using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Classic isometric (Zaxxon-style) camera for BTTYEngine.
    ///
    /// The eye is positioned at a fixed azimuth (45°) and elevation (~30°) relative
    /// to the tracked position, producing the familiar diagonal top-down voxel look.
    /// Uses a perspective projection with a very narrow FOV to closely approximate
    /// true orthographic while keeping depth cues, which suits voxel worlds better
    /// than a fully flat orthographic projection.
    ///
    /// Drop-in replacement for SideScrollingCamera — both implement ICamera, so
    /// CameraTransitionManager can blend smoothly between them.
    /// </summary>
    public class IsometricCamera : BaseCamera
    {
        // ── Isometric angles ──────────────────────────────────────────────────────

        /// <summary>Horizontal rotation around the scene (radians). 45° gives classic Zaxxon look.</summary>
        public float Azimuth = MathHelper.PiOver4;

        /// <summary>Vertical tilt above the horizontal plane (radians). ~30° is classic isometric.</summary>
        public float Elevation = MathHelper.Pi / 6f;  // 30°

        /// <summary>Distance from the tracked position to the camera eye.</summary>
        public float Distance = 120f;

        // ── Tuning ────────────────────────────────────────────────────────────────

        /// <summary>
        /// How quickly Position lerps toward Target each frame (0 = frozen, 1 = instant).
        /// </summary>
        public float MoveSpeed = 0.05f;

        /// <summary>
        /// Field of view in radians. A narrow FOV (< PiOver4) approximates orthographic
        /// while retaining subtle depth cues. Widen for more dramatic perspective.
        /// </summary>
        public float FieldOfView = MathHelper.ToRadians(20f);

        // ── Constructor ───────────────────────────────────────────────────────────

        public IsometricCamera(GraphicsDevice graphicsDevice, Viewport viewport)
            : base(graphicsDevice, viewport)
        {
            // WorldMatrix = YFlipMatrix (Y-up convention) inherited from BaseCamera

            ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                FieldOfView,
                viewport.AspectRatio,
                0.1f,
                600f);   // deeper far plane — isometric framing shows more world

            BoundingFrustum = new BoundingFrustum(Matrix.Identity);

            // Prime the view matrix with a sensible default so BoundingFrustum isn't degenerate
            RebuildView();
        }

        // ── ICamera / BaseCamera ──────────────────────────────────────────────────

        public override void Update(GameTime gameTime, VoxelWorld world)
        {
            Position = Vector3.Lerp(Position, Target, MoveSpeed);
            RebuildView();
        }

        // ── Internals ─────────────────────────────────────────────────────────────

        void RebuildView()
        {
            // Compute the spherical eye offset at the given azimuth and elevation.
            float cosEl = (float)System.Math.Cos(Elevation);
            float sinEl = (float)System.Math.Sin(Elevation);
            float cosAz = (float)System.Math.Cos(Azimuth);
            float sinAz = (float)System.Math.Sin(Azimuth);

            // Eye offset in Y-up world space: positive Y = above the target.
            Vector3 eyeOffset = new Vector3(
                cosEl * cosAz,
                sinEl,            // positive = above in Y-up world space
                cosEl * sinAz
            ) * Distance;

            // ── Rendering view (Y-up world space) ────────────────────────────────
            // Convert game-space Position to Y-up world space, then place the eye
            // on the sphere above and to the diagonal side.
            Vector3 renderPos = ToWorldSpace(Position);
            ViewMatrix = Matrix.CreateLookAt(
                renderPos + eyeOffset,
                renderPos,
                Vector3.Up);

            // ── Culling frustum (game-space Y-down) ───────────────────────────────
            // Mirror of the eye offset but in game-space Y-down convention:
            // "above the scene" means NEGATIVE Y in game space.
            Vector3 gameEyeOffset = new Vector3(
                cosEl * cosAz,
                -sinEl,           // negative Y = visually above in Y-down game space
                cosEl * sinAz
            ) * Distance;
            Matrix cullingView = Matrix.CreateLookAt(
                Position + gameEyeOffset,
                Position,
                Vector3.Down);
            BoundingFrustum.Matrix = cullingView * ProjectionMatrix;
        }
    }
}
