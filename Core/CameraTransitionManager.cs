using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Manages smooth transitions between two <see cref="ICamera"/> implementations.
    ///
    /// Implements <see cref="ICamera"/> itself, so the game loop never needs to know
    /// whether a transition is in progress — it always reads matrices from this manager.
    ///
    /// Usage:
    ///   1. Construct with the default active camera.
    ///   2. Call <see cref="TransitionTo"/> whenever a trigger zone is reached.
    ///   3. Replace all direct camera references in the game loop with this manager.
    ///   4. Call <see cref="Update"/> each frame as normal.
    ///
    /// Phase 2 will wire this to Tiled MapObject trigger zones so transitions fire
    /// automatically as the player scrolls through defined level sections.
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

        float _blendT       = 1f;   // 0 = fully _from, 1 = fully _to
        float _blendSpeed   = 0f;   // units per frame (1 / durationFrames)

        // ── Constructor ───────────────────────────────────────────────────────────

        public CameraTransitionManager(ICamera initialCamera)
        {
            _from   = initialCamera;
            _to     = initialCamera;
            _active = initialCamera;

            BoundingFrustum = new BoundingFrustum(Matrix.Identity);

            // Prime matrices so they're never degenerate on first frame
            SyncFromActive();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Begin blending from the current active camera to <paramref name="target"/>.
        /// The transition completes in approximately <paramref name="durationSeconds"/> seconds
        /// (assumes a 60 Hz update rate; adjust if needed).
        /// </summary>
        /// <param name="target">The camera to transition to.</param>
        /// <param name="durationSeconds">How long the blend should take in seconds.</param>
        public void TransitionTo(ICamera target, float durationSeconds = 1.5f)
        {
            if (target == _active) return;

            // Carry position/target across so the new camera picks up from the
            // same world location rather than snapping to its own defaults.
            target.Position = _active.Position;
            target.Target   = _active.Target;

            _from     = _active;
            _to       = target;
            _blendT   = 0f;
            _blendSpeed = 1f / (durationSeconds * 60f);  // normalised speed per frame
        }

        // ── ICamera ───────────────────────────────────────────────────────────────

        public void Update(GameTime gameTime, VoxelWorld world)
        {
            // Always update both cameras so they stay in sync during the blend
            _from.Update(gameTime, world);

            if (_from != _to)
                _to.Update(gameTime, world);

            if (IsTransitioning)
            {
                _blendT = MathHelper.Clamp(_blendT + _blendSpeed, 0f, 1f);
                float smooth = Smoothstep(_blendT);

                // Blend the rendering matrices
                WorldMatrix      = Matrix.Lerp(_from.WorldMatrix,      _to.WorldMatrix,      smooth);
                ViewMatrix       = Matrix.Lerp(_from.ViewMatrix,       _to.ViewMatrix,       smooth);
                ProjectionMatrix = Matrix.Lerp(_from.ProjectionMatrix, _to.ProjectionMatrix, smooth);

                // Blend the game-space culling frustum (both cameras maintain Y-down frustums)
                BoundingFrustum.Matrix = Matrix.Lerp(
                    _from.BoundingFrustum.Matrix,
                    _to.BoundingFrustum.Matrix,
                    smooth);

                // Promote _to to active once blend is complete
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
            WorldMatrix      = _active.WorldMatrix;
            ViewMatrix       = _active.ViewMatrix;
            ProjectionMatrix = _active.ProjectionMatrix;
            // Sync the game-space culling frustum directly from the active camera
            BoundingFrustum.Matrix = _active.BoundingFrustum.Matrix;
        }

        /// <summary>
        /// Smoothstep easing: eliminates the snap at the start and end of a linear
        /// lerp, giving the transition a natural ease-in / ease-out feel.
        /// </summary>
        static float Smoothstep(float t) => t * t * (3f - 2f * t);
    }
}
