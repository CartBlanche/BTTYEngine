using BepuPhysics.Collidables;

namespace BTTYEngine
{
    public enum PhysicsGroup : byte
    {
        Hero       = 0,
        Enemy      = 1,
        Projectile = 2,
        Powerup    = 3,
        Terrain    = 4,
        Particle   = 5,
    }

    public static class PhysicsLayers
    {
        // Expand with filtering logic as entity types are registered.
        public static bool ShouldCollide(CollidableReference a, CollidableReference b) => true;
    }
}
