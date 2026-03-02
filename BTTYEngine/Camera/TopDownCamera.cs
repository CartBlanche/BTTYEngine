using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Pure top-down camera: eye directly above the hero looking straight down.
    /// The up-hint is <see cref="ForwardHint"/> (+X = scroll direction) so that
    /// the scroll direction maps to the top of the screen; natural for a horizontal shooter.
    /// Inherits screen-shake from <see cref="BaseCamera"/>.
    /// </summary>
    public class TopDownCamera : BaseCamera
    {
        /// <summary>Height of the eye above the scene (world units).</summary>
        public float Height = 100f;

        /// <summary>
        /// Forward hint for CreateLookAt. With eye directly above and looking down,
        /// the up-hint must NOT be Vector3.Up (parallel to look direction).
        /// Use Vector3.Right (+X = scroll direction) so the screen top = scroll direction.
        /// </summary>
        public Vector3 ForwardHint = Vector3.Right;   // +X = scroll direction = "up" on screen

        public float MoveSpeed = 0.05f;

        public TopDownCamera(GraphicsDevice graphicsDevice, Viewport viewport)
            : base(graphicsDevice, viewport)
        {
            WorldMatrix = Matrix.CreateWorld(Vector3.Zero, Vector3.Forward, Vector3.Up);

            ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45f),
                viewport.AspectRatio,
                1f,
                600f);

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
            Vector3 eye = Position + new Vector3(0f, Height, 0f) + ShakeOffset;
            ViewMatrix = Matrix.CreateLookAt(eye, Position, ForwardHint);
            BoundingFrustum.Matrix = ViewMatrix * ProjectionMatrix;
        }
    }
}
