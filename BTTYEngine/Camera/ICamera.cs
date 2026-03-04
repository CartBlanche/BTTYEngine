using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BTTYEngine
{
    /// <summary>
    /// Core camera abstraction for the BTTYEngine.
    /// All camera implementations (SideScrolling, Isometric, etc.) implement this interface,
    /// allowing the voxel world, entities, and renderers to be camera-agnostic.
    /// </summary>
    public interface ICamera
    {
        /// <summary>World transform matrix.</summary>
        Matrix WorldMatrix { get; }

        /// <summary>View (look-at) matrix.</summary>
        Matrix ViewMatrix { get; }

        /// <summary>Perspective or orthographic projection matrix.</summary>
        Matrix ProjectionMatrix { get; }

        /// <summary>Frustum used for visibility/culling tests.</summary>
        BoundingFrustum BoundingFrustum { get; }

        /// <summary>Current world-space position of the camera eye.</summary>
        Vector3 Position { get; set; }

        /// <summary>The world-space point the camera is tracking / lerping toward.</summary>
        Vector3 Target { get; set; }

        /// <summary>
        /// Update camera matrices, frustum and any movement logic each frame.
        /// </summary>
        void Update(GameTime gameTime, VoxelWorld world);

        /// <summary>
        /// Trigger a screen-shake of the given peak amplitude (world units).
        /// Amplitude decays automatically each frame; calling again before the previous
        /// shake finishes takes the maximum of the two (no cancellation).
        /// </summary>
        void TriggerShake(float amplitude);
    }
}
