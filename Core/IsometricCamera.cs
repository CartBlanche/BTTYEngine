using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Isometric (Zaxxon-style) camera for BTTYEngine.
    ///
    /// Uses the same Y-down game-space convention as the rest of the engine:
    ///   • Positive Y = downward on screen
    ///   • "Visually above" the scene = negative Y offset from the target
    ///   • Vector3.Down as the CreateLookAt up-hint, matching SideScrollingCamera
    ///
    /// The eye is placed on a sphere at <see cref="Azimuth"/> and <see cref="Elevation"/>
    /// around the tracked position. Classic Zaxxon defaults: 45° azimuth, 30° elevation.
    ///
    /// Uses a narrow perspective FOV (20°) to approximate orthographic projection while
    /// retaining subtle depth cues that suit voxel worlds.
    /// </summary>
    public class IsometricCamera : BaseCamera
    {
        // ── Isometric angles ──────────────────────────────────────────────────────

        /// <summary>
        /// Horizontal rotation around the scene (radians).
        /// Pi (180°)  = directly behind the ship, no lateral offset.
        /// 5Pi/4 (225°) = behind and offset into -Z (toward viewer), which
        /// places the eye over the right shoulder when the ship flies in +X.
        /// </summary>
        public float Azimuth = MathHelper.Pi * 1.25f;  // 225° — right-shoulder Zaxxon

        /// <summary>
        /// Vertical tilt above the horizontal plane (radians).
        /// 45° gives the over-the-shoulder depth that matches Zaxxon's perspective.
        /// </summary>
        public float Elevation = MathHelper.PiOver4;    // 45°

        /// <summary>Distance from the tracked position to the camera eye.</summary>
        public float Distance = 150f;

        /// <summary>How quickly Position lerps toward Target each frame.</summary>
        public float MoveSpeed = 0.05f;

        /// <summary>
        /// Narrow FOV approximates orthographic while keeping voxel depth cues.
        /// </summary>
        public float FieldOfView = MathHelper.ToRadians(20f);

        // ── Constructor ───────────────────────────────────────────────────────────

        public IsometricCamera(GraphicsDevice graphicsDevice, Viewport viewport)
            : base(graphicsDevice, viewport)
        {
            WorldMatrix = Matrix.CreateWorld(Vector3.Zero, Vector3.Forward, Vector3.Up);

            ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                FieldOfView,
                viewport.AspectRatio,
                0.1f,
                600f);

            BoundingFrustum = new BoundingFrustum(Matrix.Identity);
            RebuildView();
        }

        // ── BaseCamera ────────────────────────────────────────────────────────────

        public override void Update(GameTime gameTime, VoxelWorld world)
        {
            Position = Vector3.Lerp(Position, Target, MoveSpeed);
            RebuildView();
        }

        // ── Internals ─────────────────────────────────────────────────────────────

        void RebuildView()
        {
            float cosEl = (float)System.Math.Cos(Elevation);
            float sinEl = (float)System.Math.Sin(Elevation);
            float cosAz = (float)System.Math.Cos(Azimuth);
            float sinAz = (float)System.Math.Sin(Azimuth);

            // In Y-down game space, "visually above" = negative Y.
            // The eye sits on a sphere at the given azimuth/elevation around Position.
            Vector3 eyeOffset = new Vector3(
                cosEl * cosAz,
                -sinEl,         // negative Y = above the scene in Y-down space
                cosEl * sinAz
            ) * Distance;

            // Vector3.Down as up-hint matches the side-scroller convention.
            ViewMatrix = Matrix.CreateLookAt(
                Position + eyeOffset,
                Position,
                Vector3.Down);

            BoundingFrustum.Matrix = ViewMatrix * ProjectionMatrix;
        }
    }
}
