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

        // Cooldown (ms) that prevents the same enemy repeatedly bumping the powerup every frame.
        float bumpCooldown;

        public Powerup()
        {

        }

        public void Update(GameTime gameTime, VoxelWorld gameWorld, Hero gameHero, float scrollPos)
        {
            if (bumpCooldown > 0f) bumpCooldown -= (float)gameTime.ElapsedGameTime.TotalMilliseconds;

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

            float heroDist = Vector3.Distance(gameHero.Position, Position);
            if (heroDist < 25f)
            {
                // Bleed off momentum so the lerp wins and the powerup isn't dragged in the wake.
                float dampen = MathHelper.Lerp(0.70f, 0.92f, heroDist / 25f);
                Speed.X *= dampen;
                Speed.Y *= dampen;
                Position = Vector3.Lerp(Position, gameHero.Position, 0.05f);
            }
            if (heroDist < 10f) Position = Vector3.Lerp(Position, gameHero.Position, 0.1f);

            if (heroDist < 5f)
            {
                gameHero.XP += 0.15f;
                Active = false;
            }
        }

        public void Spawn(Vector3 pos, Vector3 speed)
        {
            Active       = true;
            Position     = pos;
            Speed        = speed;
            bumpCooldown = 0f;
        }

        /// <summary>
        /// Called when an enemy overlaps the powerup.  Applies an impulse away from the enemy
        /// centre, scaled by the enemy's current speed and size, with an upward bias so the
        /// powerup arcs visibly.  A 400 ms cooldown prevents repeated micro-bumps.
        /// </summary>
        public void ApplyEnemyBump(Enemy enemy)
        {
            if (bumpCooldown > 0f) return;
            bumpCooldown = 400f;

            // Direction from enemy centre to powerup, clamped to the XY play plane.
            Vector3 pushDir = Position - enemy.Position;
            pushDir.Z = 0f;
            if (pushDir.LengthSquared() < 0.0001f) pushDir = new Vector3(1f, 0.5f, 0f);
            pushDir = Vector3.Normalize(pushDir);

            // Kick magnitude: guaranteed minimum + enemy momentum + size bonus.
            // Speed values are per-frame units (~0.1–0.5), so multiplying by 2 gives a
            // noticeably larger kick than the powerup's normal terrain bounce.
            float kick = MathHelper.Clamp(0.3f + enemy.Speed.Length() * 2.0f + (enemy.Scale - 1f) * 0.15f, 0.3f, 0.8f);

            // Blend a little of the existing velocity in (elastic feel) then add the kick.
            Speed.X = Speed.X * 0.15f + pushDir.X * kick;
            Speed.Y = Speed.Y * 0.15f + pushDir.Y * kick + 0.15f;  // upward bias for a fun arc

            Speed.X = MathHelper.Clamp(Speed.X, -0.8f, 0.8f);
            Speed.Y = MathHelper.Clamp(Speed.Y, -0.8f, 0.8f);
        }


    }
}
