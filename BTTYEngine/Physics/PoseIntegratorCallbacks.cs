using BepuPhysics;
using BepuUtilities;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace BTTYEngine
{
    // Side-scroller: no world gravity. Gravity field kept for future per-entity gravity scaling.
    public struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        public Vector3 Gravity;
        private Vector3Wide _gravityWideDt;

        public PoseIntegratorCallbacks(Vector3 gravity) { Gravity = gravity; }

        public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public bool AllowSubstepsForUnconstrainedBodies => false;
        public bool IntegrateVelocityForKinematics => false;

        public void Initialize(Simulation simulation) { }

        public void PrepareForIntegration(float dt)
        {
            _gravityWideDt = Vector3Wide.Broadcast(Gravity * dt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntegrateVelocity(
            Vector<int> bodyIndices, Vector3Wide position,
            QuaternionWide orientation, BodyInertiaWide localInertia,
            Vector<int> integrationMask, int workerIndex, Vector<float> dt,
            ref BodyVelocityWide velocity)
        {
            velocity.Linear += _gravityWideDt;
        }
    }
}
