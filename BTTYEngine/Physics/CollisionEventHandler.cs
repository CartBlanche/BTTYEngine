using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using System;
using System.Collections.Concurrent;

namespace BTTYEngine
{
    public record struct CollisionEvent(BodyHandle A, BodyHandle B);

    public class CollisionEventHandler
    {
        public static readonly CollisionEventHandler Instance = new CollisionEventHandler();

        private readonly ConcurrentQueue<CollisionEvent> pending = new();

        // Called from Bepu worker threads; no MonoGame API calls here.
        public void OnContact(CollidablePair pair, int workerIndex)
        {
            if (pair.A.Mobility == CollidableMobility.Dynamic &&
                pair.B.Mobility == CollidableMobility.Dynamic)
            {
                pending.Enqueue(new CollisionEvent(pair.A.BodyHandle, pair.B.BodyHandle));
            }
        }

        // Called from game thread each frame after physicsManager.Step().
        public void ProcessPending(Action<CollisionEvent> handler)
        {
            while (pending.TryDequeue(out var evt))
                handler(evt);
        }
    }
}
