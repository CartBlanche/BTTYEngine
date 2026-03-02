using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;

namespace VoxelShooter
{
    public struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        public void Initialize(Simulation simulation) { }
        public void Dispose() { }

        // Called for top-level primitive pairs.
        public bool AllowContactGeneration(
            int workerIndex, CollidableReference a, CollidableReference b,
            ref float speculativeMargin)
        {
            return PhysicsLayers.ShouldCollide(a, b);
        }

        // Called for child pairs within compound shapes.
        public bool AllowContactGeneration(
            int workerIndex, CollidablePair pair,
            int childIndexA, int childIndexB) => true;

        // Called for top-level pairs; set material and queue collision event.
        public bool ConfigureContactManifold<TManifold>(
            int workerIndex, CollidablePair pair, ref TManifold manifold,
            out PairMaterialProperties pairMaterial)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial = new PairMaterialProperties
            {
                FrictionCoefficient = 0.2f,
                MaximumRecoveryVelocity = 2f,
                SpringSettings = new SpringSettings(30, 1)
            };

            CollisionEventHandler.Instance.OnContact(pair, workerIndex);
            return true;
        }

        // Called for child pairs within compound shapes; no special handling needed.
        public bool ConfigureContactManifold(
            int workerIndex, CollidablePair pair,
            int childIndexA, int childIndexB,
            ref ConvexContactManifold manifold) => true;
    }
}
