using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BTTYEngine
{
    /// <summary>
    /// First-person camera: eye positioned at the hero with a fixed look-ahead along the
    /// scroll direction (+X). Pitch and yaw are locked; no mouse look needed.
    /// Inherits screen-shake from <see cref="BaseCamera"/>.
    /// </summary>
    public class FirstPersonCamera : BaseCamera
    {
        /// <summary>
        /// Offset from the tracked position to the camera eye (hero cockpit offset).
        /// X = forward along scroll, Y = slightly above centre, Z = slight pullback.
        /// </summary>
        public Vector3 EyeOffset = new Vector3(5f, 2f, 0f);

        /// <summary>How far ahead the look-at point is placed (world units).</summary>
        public float LookAheadDistance = 30f;

        public float MoveSpeed = 0.08f;

        public FirstPersonCamera(GraphicsDevice graphicsDevice, Viewport viewport)
            : base(graphicsDevice, viewport)
        {
            WorldMatrix = Matrix.CreateWorld(Vector3.Zero, Vector3.Forward, Vector3.Up);

            ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(75f),   // wider FOV for immersion
                viewport.AspectRatio,
                0.1f,
                300f);

            BoundingFrustum = new BoundingFrustum(Matrix.Identity);
            RebuildView();
        }

        public override void Update(GameTime gameTime, VoxelWorld world)
        {
            UpdateShake();
            Position = Vector3.Lerp(Position, Target, MoveSpeed);
            RebuildView();
        }

        void RebuildView()
        {
            Vector3 eye    = Position + EyeOffset + ShakeOffset;
            Vector3 lookAt = Position + new Vector3(LookAheadDistance, 0f, 0f);
            ViewMatrix = Matrix.CreateLookAt(eye, lookAt, Vector3.Up);
            BoundingFrustum.Matrix = ViewMatrix * ProjectionMatrix;
        }
    }
}
