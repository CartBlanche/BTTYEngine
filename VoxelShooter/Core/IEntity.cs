using BTTYEngine;

namespace VoxelShooter
{
    public interface IEntity
    {
        void OnCollision(IEntity other);
    }
}
