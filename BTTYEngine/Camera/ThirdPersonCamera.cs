using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BTTYEngine
{
    /// <summary>
    /// Third-person (Skyfox-style) forward-scrolling camera for BTTYEngine.
    ///
    /// The eye sits behind and above the tracked ship, looking at the ship itself.
    /// The scene scrolls forward (+X) so the player sees the ship from over the shoulder
    /// with the playfield stretching ahead — the classic cockpit-chase feel.
    ///
    /// Inherits screen-shake from <see cref="BaseCamera"/>.
    /// </summary>
    public class ThirdPersonCamera : BaseCamera
    {
        /// <summary>
        /// Offset from the tracked <see cref="BaseCamera.Target"/> to the camera eye.
        /// X = negative pulls the eye back (behind the ship along the scroll axis).
        /// Y = positive lifts the eye above the ship.
        /// Z = lateral offset (0 = centred).
        /// </summary>
        public Vector3 EyeOffset = new Vector3(-40f, 20f, 0f);

        /// <summary>
        /// Offset from the tracked position to the look-at point.
        /// A positive X value makes the camera look slightly ahead of the ship,
        /// giving a sense of forward momentum.
        /// </summary>
        public Vector3 LookOffset = new Vector3(12f, 4f, 0f);

        /// <summary>
        /// How quickly <see cref="BaseCamera.Position"/> lerps toward
        /// <see cref="BaseCamera.Target"/> each frame (0 = frozen, 1 = instant).
        /// </summary>
        public float MoveSpeed = 0.06f;

        /// <summary>
        /// Field of view in radians. ~62° gives a natural over-the-shoulder feel —
        /// wider than iso (20°) but narrower than first-person (75°).
        /// </summary>
        public float FieldOfView = MathHelper.ToRadians(65f);

        // Constructor

        public ThirdPersonCamera(GraphicsDevice graphicsDevice, Viewport viewport)
            : base(graphicsDevice, viewport)
        {
            WorldMatrix = Matrix.CreateWorld(Vector3.Zero, Vector3.Forward, Vector3.Up);

            ProjectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                FieldOfView,
                viewport.AspectRatio,
                0.1f,
                400f);

            BoundingFrustum = new BoundingFrustum(Matrix.Identity);
            RebuildView();
        }

        // BaseCamera

        public override void Update(GameTime gameTime, VoxelWorld world)
        {
            UpdateShake();
            Position = Vector3.Lerp(Position, Target, MoveSpeed);
            RebuildView();
        }

        // Internals

        void RebuildView()
        {
            Vector3 eye    = Position + EyeOffset + ShakeOffset;
            Vector3 lookAt = Position + LookOffset;

            ViewMatrix = Matrix.CreateLookAt(eye, lookAt, Vector3.Up);
            BoundingFrustum.Matrix = ViewMatrix * ProjectionMatrix;
        }
    }
}
