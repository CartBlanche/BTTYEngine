using BepuPhysics;
using BepuUtilities.Memory;
using System;
using System.Numerics;

namespace VoxelShooter
{
    public class PhysicsManager : IDisposable
    {
        public static PhysicsManager Instance { get; private set; }

        public Simulation Simulation { get; private set; }

        private BufferPool _bufferPool;

        public void Initialize()
        {
            Instance = this;
            _bufferPool = new BufferPool();

            Simulation = Simulation.Create(
                _bufferPool,
                new NarrowPhaseCallbacks(),
                new PoseIntegratorCallbacks(gravity: Vector3.Zero),
                new SolveDescription(velocityIterationCount: 4, substepCount: 1));
        }

        // Single-threaded step — sufficient for a small side-scroller.
        // Guard against zero dt on the first frame (Bepu requires dt > 0).
        public void Step(float deltaTimeSeconds)
        {
            if (deltaTimeSeconds <= 0f) return;
            Simulation.Timestep(deltaTimeSeconds);
        }

        public void Dispose()
        {
            Simulation?.Dispose();
            _bufferPool?.Clear();
        }
    }
}
