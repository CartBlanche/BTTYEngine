using BepuPhysics;
using BepuUtilities.Memory;
using System;
using System.Numerics;

namespace BTTYEngine
{
    /// <summary>
    /// Wraps a BepuPhysics <see cref="BepuPhysics.Simulation"/> and exposes a simple
    /// single-threaded game-loop interface. Access the live simulation via
    /// <see cref="Instance"/> from anywhere that needs to add or remove bodies.
    /// </summary>
    public class PhysicsManager : IDisposable
    {
        /// <summary>The most recently created PhysicsManager. Set during <see cref="Initialize"/>.</summary>
        public static PhysicsManager Instance { get; private set; }

        /// <summary>The underlying BepuPhysics simulation. Use this to add bodies, constraints, etc.</summary>
        public Simulation Simulation { get; private set; }

        private BufferPool bufferPool;

        /// <summary>
        /// Creates the BepuPhysics simulation with zero gravity and registers this instance
        /// as <see cref="Instance"/>. Call once, before the first <see cref="Step"/>.
        /// </summary>
        public void Initialize()
        {
            Instance = this;
            bufferPool = new BufferPool();

            Simulation = Simulation.Create(
                bufferPool,
                new NarrowPhaseCallbacks(),
                new PoseIntegratorCallbacks(gravity: Vector3.Zero),
                new SolveDescription(velocityIterationCount: 4, substepCount: 1));
        }

        // Single-threaded step; sufficient for a small side-scroller.
        // Guard against zero dt on the first frame (Bepu requires dt > 0).
        /// <summary>Advance the simulation by <paramref name="deltaTimeSeconds"/>. Call once per game loop frame.</summary>
        public void Step(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f) return;
            Simulation.Timestep(deltaTimeSeconds);
        }

        /// <summary>Disposes the simulation and frees the buffer pool. Call from <c>Game.UnloadContent</c>.</summary>
        public void Dispose()
        {
            Simulation?.Dispose();
            bufferPool?.Clear();
        }
    }
}
