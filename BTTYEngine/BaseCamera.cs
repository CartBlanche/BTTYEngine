using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VoxelShooter
{
    /// <summary>
    /// Abstract base for all BTTYEngine cameras.
    /// Holds the matrices, frustum, and position/target that every camera type shares.
    /// Subclasses implement Update() to define their specific movement and projection behaviour.
    /// </summary>
    public abstract class BaseCamera : ICamera
    {
        // ── ICamera ─────────────────────────────────────────────────────────────

        public Matrix WorldMatrix      { get; protected set; }
        public Matrix ViewMatrix       { get; protected set; }
        public Matrix ProjectionMatrix { get; protected set; }
        public BoundingFrustum BoundingFrustum { get; protected set; }

        public Vector3 Position { get; set; }
        public Vector3 Target   { get; set; }

        // ── Screen-shake ─────────────────────────────────────────────────────────

        static readonly System.Random _shakeRng = new System.Random();

        float _shakeAmplitude;

        /// <summary>
        /// Current per-frame XY eye offset produced by the active shake.
        /// Add this to the camera eye position inside subclass Update() / RebuildView().
        /// </summary>
        protected Vector3 ShakeOffset { get; private set; }

        /// <summary>
        /// Start (or reinforce) a screen-shake.  The new amplitude is max'd with any
        /// in-progress shake so a fresh hit never cancels an existing one.
        /// Override in a subclass to change the profile (e.g. dampen for iso, add roll).
        /// </summary>
        public virtual void TriggerShake(float amplitude)
            => _shakeAmplitude = System.Math.Max(_shakeAmplitude, amplitude);

        /// <summary>
        /// Decay the shake amplitude and refresh <see cref="ShakeOffset"/>.
        /// Call once at the top of each subclass Update().
        /// </summary>
        protected void UpdateShake()
        {
            _shakeAmplitude *= 0.85f;
            if (_shakeAmplitude > 0.01f)
            {
                float r() => (float)(_shakeRng.NextDouble() * 2.0 - 1.0) * _shakeAmplitude;
                ShakeOffset = new Vector3(r(), r(), 0f);
            }
            else
            {
                _shakeAmplitude = 0f;
                ShakeOffset = Vector3.Zero;
            }
        }

        // ── Engine internals ─────────────────────────────────────────────────────

        protected GraphicsDevice GraphicsDevice { get; }
        protected Viewport       Viewport       { get; }

        // ── Constructor ──────────────────────────────────────────────────────────

        protected BaseCamera(GraphicsDevice graphicsDevice, Viewport viewport)
        {
            GraphicsDevice = graphicsDevice;
            Viewport       = viewport;

            // Safe default – subclasses are expected to override in their constructor
            WorldMatrix      = Matrix.Identity;
            ViewMatrix       = Matrix.Identity;
            ProjectionMatrix = Matrix.Identity;
            BoundingFrustum  = new BoundingFrustum(Matrix.Identity);
        }

        // ── Abstract contract ────────────────────────────────────────────────────

        /// <summary>
        /// Update view matrices, frustum, and any camera-specific interpolation.
        /// Called once per frame by the game loop.
        /// </summary>
        public abstract void Update(GameTime gameTime, VoxelWorld world);
    }
}
