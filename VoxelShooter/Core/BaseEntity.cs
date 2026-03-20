using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;

using BTTYEngine;

namespace VoxelShooter
{
    /// <summary>
    /// Common physics/collision boilerplate shared by Hero and Enemy.
    /// Owns the Bepu BodyHandle and exposes InitPhysics / DestroyPhysics /
    /// SyncPhysicsToPosition so subclasses never duplicate that wiring.
    /// </summary>
    public abstract class BaseEntity : IEntity
    {
        public Vector3 Position;
        public Vector3 Speed;
        public float   Health  = 100f;
        public bool    Active  = true;
        public float   hitAlpha = 0f;

        protected BodyHandle _physicsBody;
        protected bool       _physicsInitialized;

        // Subclasses provide their own sphere radius for physics registration.
        protected virtual float PhysicsRadius => 3f;

        public virtual void InitPhysics(PhysicsManager physics)
        {
            var sphere  = new Sphere(PhysicsRadius);
            var inertia = sphere.ComputeInertia(1f);
            inertia.InverseInertiaTensor = default; // lock rotation — no tumbling
            var shapeIndex = physics.Simulation.Shapes.Add(sphere);
            _physicsBody = physics.Simulation.Bodies.Add(
                BodyDescription.CreateDynamic(
                    new RigidPose(new System.Numerics.Vector3(Position.X, Position.Y, Position.Z)),
                    new BodyVelocity(),
                    inertia,
                    new CollidableDescription(shapeIndex, 0.1f),
                    new BodyActivityDescription(0.01f)));
            EntityRegistry.Instance.Register(_physicsBody, this);
            _physicsInitialized = true;
        }

        public void DestroyPhysics(PhysicsManager physics)
        {
            if (!_physicsInitialized) return;
            EntityRegistry.Instance.Unregister(_physicsBody);
            physics.Simulation.Bodies.Remove(_physicsBody);
            _physicsInitialized = false;
        }

        /// <summary>
        /// Writes the current <see cref="Position"/> back into the Bepu body pose.
        /// Call after any code that moves Position directly (e.g. Wave formation updates).
        /// </summary>
        public void SyncPhysicsToPosition()
        {
            if (!_physicsInitialized) return;
            if (float.IsNaN(Position.X) || float.IsNaN(Position.Y) || float.IsNaN(Position.Z)) return;
            var body = PhysicsManager.Instance.Simulation.Bodies.GetBodyReference(_physicsBody);
            body.Pose.Position = new System.Numerics.Vector3(Position.X, Position.Y, Position.Z);
            body.Awake = true;
        }

        public abstract void OnCollision(IEntity other);
    }
}
