using BepuPhysics;
using System.Collections.Generic;

namespace VoxelShooter
{
    // Maps Bepu BodyHandles to game entities for collision event dispatch.
    // All register/unregister calls must happen on the game thread.
    public class EntityRegistry
    {
        public static readonly EntityRegistry Instance = new EntityRegistry();

        private readonly Dictionary<int, IEntity> _map = new();

        public void Register(BodyHandle handle, IEntity entity)
        {
            _map[handle.Value] = entity;
        }

        public void Unregister(BodyHandle handle)
        {
            _map.Remove(handle.Value);
        }

        public IEntity FindByHandle(BodyHandle handle)
        {
            _map.TryGetValue(handle.Value, out var entity);
            return entity;
        }
    }
}
