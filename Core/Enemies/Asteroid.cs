using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BTTYEngine;

namespace VoxelShooter
{
    class Asteroid : Enemy
    {
        Vector3 rotSpeed;

        public Asteroid(Vector3 pos, VoxelSprite sprite)
            : base(pos, sprite)
        {
            numFrames = 1;
            rotSpeed = new Vector3(Helper.RandomFloat(-0.1f, 0.1f), 0f, Helper.RandomFloat(-0.1f, 0.1f));//Helper.RandomFloat(-0.1f, 0.1f));
            Speed.X = Helper.RandomFloat(-1f, -0.3f);
            Speed.Y = Helper.RandomFloat(-0.2f, 0.2f);
            Health = 3f;
        }

        public override void OnCollision(IEntity other)
        {
            if (other is Hero hero)
            {
                // Damage scales with asteroid speed; fast rocks hurt more than slow ones.
                float damage = Speed.Length() * 20f;
                hero.DoHit(Position, damage);
                // Push the ship in the asteroid's direction of travel, proportional to speed.
                hero.ApplyKnockback(Speed * 0.5f);
                // Destroy the asteroid immediately so the collision fires exactly once.
                Health = 0f;
            }
        }

        public override void Die()
        {
            // Capture velocity before base.Die() clears physics state.
            Vector3 deathVelocity = Speed;

            base.Die();

            if (Health <= 0f)
            {
                // XP fragments drift in the asteroid's direction at 25% of its speed.
                Vector3 xpSpeed = deathVelocity * 0.25f;
                for (int i = 0; i < 3; i++)
                    PowerupController.Instance.Spawn(
                        Position + new Vector3(Helper.RandomFloat(-3f, 3f), Helper.RandomFloat(-3f, 3f), 0f),
                        xpSpeed);
            }
        }

        

        public override void DoCollide(bool x, bool y, bool z, Vector3 checkPosition, Hero gameHero, VoxelWorld gameWorld, bool withPlayer)
        {
            gameWorld.Explode(checkPosition, 5f);

            Health = 0f;
            Die();

            base.DoCollide(x, y, z, checkPosition, gameHero, gameWorld, withPlayer);
        }

        public override void Update(GameTime gameTime, VoxelWorld gameWorld, Hero gameHero)
        {
            Rotation += rotSpeed;

            if(Helper.Random.Next(10)==1) ParticleController.Instance.Spawn(new Vector3(Helper.RandomPointInCircle(new Vector2(Position.X, Position.Y), 0f, 4f), Position.Z), Vector3.Zero, 0.3f, new Color(Color.Gray.ToVector3() * Helper.RandomFloat(0.4f, 0.8f)), 1000, false);

            base.Update(gameTime, gameWorld, gameHero);
        }
    }
}
