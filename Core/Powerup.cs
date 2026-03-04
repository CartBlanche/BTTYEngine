using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BTTYEngine;

namespace VoxelShooter
{
    public class Powerup
    {

        public bool Active;
        public Vector3 Position;
        public Vector3 Speed;
        public float Rotation;

        public Powerup()
        {

        }

        public void Update(GameTime gameTime, VoxelWorld gameWorld, Hero gameHero, float scrollPos)
        {
            // Terrain bounce: reflect each velocity component on impact with a random
            // restitution (65–85%) so bounces diminish naturally.  A small lateral jitter
            // on floor/ceiling hits approximates an irregular surface normal.
            float checkZ = Position.Z - 1f;
            if (Speed.Y < 0f)
            {
                var check = new Vector3(Position.X, Position.Y - 2f, checkZ);
                var vox = gameWorld.GetVoxel(check);
                if (vox.Active && gameWorld.CanCollideWith(vox.Type))
                {
                    Speed.Y = Math.Abs(Speed.Y) * (0.65f + Helper.RandomFloat(0f, 0.2f));
                    Speed.X += Helper.RandomFloat(-0.03f, 0.03f);
                }
            }
            else if (Speed.Y > 0f)
            {
                var check = new Vector3(Position.X, Position.Y + 2f, checkZ);
                var vox = gameWorld.GetVoxel(check);
                if (vox.Active && gameWorld.CanCollideWith(vox.Type))
                {
                    Speed.Y = -Math.Abs(Speed.Y) * (0.65f + Helper.RandomFloat(0f, 0.2f));
                    Speed.X += Helper.RandomFloat(-0.03f, 0.03f);
                }
            }
            if (Speed.X < 0f)
            {
                var check = new Vector3(Position.X - 2f, Position.Y, checkZ);
                var vox = gameWorld.GetVoxel(check);
                if (vox.Active && gameWorld.CanCollideWith(vox.Type))
                    Speed.X = Math.Abs(Speed.X) * (0.65f + Helper.RandomFloat(0f, 0.2f));
            }
            else if (Speed.X > 0f)
            {
                var check = new Vector3(Position.X + 2f, Position.Y, checkZ);
                var vox = gameWorld.GetVoxel(check);
                if (vox.Active && gameWorld.CanCollideWith(vox.Type))
                    Speed.X = -Math.Abs(Speed.X) * (0.65f + Helper.RandomFloat(0f, 0.2f));
            }

            Position += Speed;
            Rotation += 0.04f;
            if (Rotation > MathHelper.TwoPi) Rotation -= MathHelper.TwoPi;
            if(Position.X < scrollPos - 75f) Active = false;

            if (Vector3.Distance(gameHero.Position, Position) < 25f) Position = Vector3.Lerp(Position, gameHero.Position, 0.05f);
            if (Vector3.Distance(gameHero.Position, Position) < 10f) Position = Vector3.Lerp(Position, gameHero.Position, 0.1f);


            if(Vector3.Distance(gameHero.Position, Position) < 3f)
            {
                gameHero.XP += 0.15f;
                Active = false;
            }
        }

        public void Spawn(Vector3 pos, Vector3 speed)
        {
            Active = true;
            Position = pos;
            Speed = speed;
        }


    }
}
