using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BTTYEngine;

namespace VoxelShooter
{
    class Squid : Enemy
    {
        public Squid(Vector3 pos, VoxelSprite sprite)
            : base(pos, sprite)
        {
            numFrames = 4;
            Health = 25f;
            attackRate = 1000 + (double)Helper.Random.Next(2000);
        }

        

        public override void DoAttack()
        {
            base.DoAttack();

            if (Position.Z <=5f)
            {
                ProjectileController.Instance.Spawn(ProjectileType.Laser2, this, Position, Matrix.CreateRotationZ(MathHelper.Pi), new Vector3(-2f, 0f, 0f), 3f, 2000, false);
            }
        }

        public override void Die()
        {
            base.Die();

            if (Health <= 0f)
                for (int i = 0; i < 4 + Helper.Random.Next(4); i++)
                    PowerupController.Instance.Spawn(Position + new Vector3(Helper.RandomFloat(-3f, 3f), Helper.RandomFloat(-3f, 3f), 0f));
        }

        public override void DoCollide(bool x, bool y, bool z, Vector3 checkPosition, Hero gameHero, VoxelWorld gameWorld, bool withPlayer)
        {
            // Bounce off terrain and take damage; die (dropping XP) after enough impacts
            if (x) Speed.X = -Speed.X;
            if (y) Speed.Y = -Speed.Y;
            if (z) Speed.Z = -Speed.Z;

            Health -= 3f;
            if (Health <= 0f) Die();
        }

        public override void Update(GameTime gameTime, VoxelWorld gameWorld, Hero gameHero)
        {
            boundingSphere = new BoundingSphere(Position, 4f);

            base.Update(gameTime, gameWorld, gameHero);
        }
    }
}
