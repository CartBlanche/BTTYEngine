using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Manages smooth transitions between two <see cref="ICamera"/> implementations.
    ///
    /// Implements <see cref="ICamera"/> itself so the game loop never needs to know
    /// whether a transition is in progress — it always reads matrices from this manager.
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
        // ── ICamera ───────────────────────────────────────────────────────────────

        public Matrix WorldMatrix      { get; private set; }
        public Matrix ViewMatrix       { get; private set; }
        public Matrix ProjectionMatrix { get; private set; }
        public BoundingFrustum BoundingFrustum { get; private set; }

        public Vector3 Position
        {
            get => _active.Position;
            set { _from.Position = value; _to.Position = value; _active.Position = value; }
        }

        public Vector3 Target
        {
            get => _active.Target;
            set { _from.Target = value; _to.Target = value; _active.Target = value; }
        }

        // ── State ─────────────────────────────────────────────────────────────────

        /// <summary>The camera currently in full control (not blending).</summary>
        public ICamera ActiveCamera => _active;

        /// <summary>True while a transition is playing.</summary>
        public bool IsTransitioning => _blendT < 1f;

        ICamera _from;
        ICamera _to;
        ICamera _active;

        float _blendT     = 1f;
        float _blendSpeed = 0f;  // 1 / (durationSeconds * 60)

        // ── Constructor ───────────────────────────────────────────────────────────

        public CameraTransitionManager(ICamera initialCamera)
        {
            _from   = initialCamera;
            _to     = initialCamera;
            _active = initialCamera;

            BoundingFrustum = new BoundingFrustum(Matrix.Identity);
            SyncFromActive();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Begin blending to <paramref name="target"/> over <paramref name="durationSeconds"/>.
        /// The incoming camera picks up the current position/target so there is no snap.
        /// </summary>
        public void TransitionTo(ICamera target, float durationSeconds = 1.5f)
        {
            if (target == _active) return;

            target.Position = _active.Position;
            target.Target   = _active.Target;

            _from       = _active;
            _to         = target;
            _blendT     = 0f;
            _blendSpeed = 1f / (durationSeconds * 60f);
        }

        // ── ICamera ───────────────────────────────────────────────────────────────

        public void Update(GameTime gameTime, VoxelWorld world)
        {
            _from.Update(gameTime, world);
            if (_from != _to)
                _to.Update(gameTime, world);

            if (IsTransitioning)
            {
                _blendT = MathHelper.Clamp(_blendT + _blendSpeed, 0f, 1f);
                float t = Smoothstep(_blendT);

                WorldMatrix      = Matrix.Lerp(_from.WorldMatrix,      _to.WorldMatrix,      t);
                ViewMatrix       = Matrix.Lerp(_from.ViewMatrix,       _to.ViewMatrix,       t);
                ProjectionMatrix = Matrix.Lerp(_from.ProjectionMatrix, _to.ProjectionMatrix, t);
                BoundingFrustum.Matrix = Matrix.Lerp(
                    _from.BoundingFrustum.Matrix,
                    _to.BoundingFrustum.Matrix, t);

                if (_blendT >= 1f)
                    _active = _to;
            }
            else
            {
                SyncFromActive();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        void SyncFromActive()
        {
            WorldMatrix             = _active.WorldMatrix;
            ViewMatrix              = _active.ViewMatrix;
            ProjectionMatrix        = _active.ProjectionMatrix;
            BoundingFrustum.Matrix  = _active.BoundingFrustum.Matrix;
        }

        /// <summary>Smoothstep easing — eliminates snap at the start and end of a lerp.</summary>
        static float Smoothstep(float t) => t * t * (3f - 2f * t);
    }
}
