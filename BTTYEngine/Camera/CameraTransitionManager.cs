using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BTTYEngine
{
    /// <summary>
    /// Manages smooth transitions between two <see cref="ICamera"/> implementations.
    ///
    /// Implements <see cref="ICamera"/> itself so the game loop never needs to know
    /// whether a transition is in progress, it always reads matrices from this manager.
    ///
    /// Usage:
    ///   1. Construct with the default active camera.
    ///   2. Call <see cref="TransitionTo"/> when a trigger zone is reached.
    ///   3. Replace the direct camera field in the game loop with this manager.
    ///   4. Call <see cref="Update"/> each frame as normal.
    ///
    /// The blend uses a smoothstep curve for natural ease-in / ease-out.
    /// Once a blend completes, the target camera becomes the new active camera.
    /// </summary>
    public class CameraTransitionManager : ICamera
    {
        // ICamera

        public Matrix WorldMatrix      { get; private set; }
        public Matrix ViewMatrix       { get; private set; }
        public Matrix ProjectionMatrix { get; private set; }
        public BoundingFrustum BoundingFrustum { get; private set; }

        public Vector3 Position
        {
            get => active.Position;
            set { from.Position = value; to.Position = value; active.Position = value; }
        }

        public Vector3 Target
        {
            get => active.Target;
            set { from.Target = value; to.Target = value; active.Target = value; }
        }

        // State

        /// <summary>The camera currently in full control (not blending).</summary>
        public ICamera ActiveCamera => active;

        /// <summary>True while a transition is playing.</summary>
        public bool IsTransitioning => blendT < 1f;

        ICamera from;
        ICamera to;
        ICamera active;

        float blendT     = 1f;
        float blendSpeed = 0f;  // 1 / (durationSeconds * 60)

        // Constructor

        /// <summary>Creates a manager with <paramref name="initialCamera"/> as the active camera. No transition plays immediately.</summary>
        public CameraTransitionManager(ICamera initialCamera)
        {
            from   = initialCamera;
            to     = initialCamera;
            active = initialCamera;

            BoundingFrustum = new BoundingFrustum(Matrix.Identity);
            SyncFromActive();
        }

        // Public API

        /// <summary>
        /// Trigger a screen-shake on all active cameras.  Passes to both <c>from</c>
        /// and <c>to</c> so the blended output stays shaken during a transition.
        /// </summary>
        public void TriggerShake(float amplitude)
        {
            from.TriggerShake(amplitude);
            if (to != from) to.TriggerShake(amplitude);
        }

        /// <summary>
        /// Begin blending to <paramref name="target"/> over <paramref name="durationSeconds"/>.
        /// The incoming camera picks up the current position/target so there is no snap.
        /// </summary>
        public void TransitionTo(ICamera target, float durationSeconds = 1.5f)
        {
            if (target == active) return;

            target.Position = active.Position;
            target.Target   = active.Target;

            from       = active;
            to         = target;
            blendT     = 0f;
            blendSpeed = 1f / (durationSeconds * 60f);
        }

        // ICamera

        /// <summary>
        /// Updates both cameras and blends their matrices while a transition is active.
        /// Call once per frame in place of a direct camera update.
        /// </summary>
        public void Update(GameTime gameTime, VoxelWorld world)
        {
            from.Update(gameTime, world);
            if (from != to)
                to.Update(gameTime, world);

            if (IsTransitioning)
            {
                blendT = MathHelper.Clamp(blendT + blendSpeed, 0f, 1f);
                float t = Smoothstep(blendT);

                WorldMatrix      = Matrix.Lerp(from.WorldMatrix,      to.WorldMatrix,      t);
                ViewMatrix       = Matrix.Lerp(from.ViewMatrix,       to.ViewMatrix,       t);
                ProjectionMatrix = Matrix.Lerp(from.ProjectionMatrix, to.ProjectionMatrix, t);
                BoundingFrustum.Matrix = Matrix.Lerp(
                    from.BoundingFrustum.Matrix,
                    to.BoundingFrustum.Matrix, t);

                if (blendT >= 1f)
                    active = to;
            }
            else
            {
                SyncFromActive();
            }
        }

        // Helpers

        void SyncFromActive()
        {
            WorldMatrix             = active.WorldMatrix;
            ViewMatrix              = active.ViewMatrix;
            ProjectionMatrix        = active.ProjectionMatrix;
            BoundingFrustum.Matrix  = active.BoundingFrustum.Matrix;
        }

        /// <summary>Smoothstep easing, eliminates snap at the start and end of a lerp.</summary>
        static float Smoothstep(float t) => t * t * (3f - 2f * t);
    }
}
