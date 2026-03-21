using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

using BTTYEngine;

namespace VoxelShooter
{
    public class Projectile
    {
        const float  GRAVITY = 0.03f;
        const double ROCKET_LAUNCH_DELAY_MS = 400.0;

        const float ROCKET_TOP_SPEED = 1.5f;
        const float ROCKET_STEER_STRENGTH   = 0.12f;   // how quickly it turns toward the target

        public ProjectileType Type;

        public object Owner;

        public bool Active;
        public Vector3 Position;
        public Vector3 Speed;
        public float Damage;
        public double Life;
        public double Time;
        public Matrix Rotation;
        public Vector3 rotSpeed;
        public bool affectedByGravity;

        public bool Deflected = false;

        Enemy target;

        public Projectile()
        {

        }

        public void Update(GameTime gameTime, Hero gameHero, VoxelWorld gameWorld, float scrollPos)
        {
            Time += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (affectedByGravity) Speed.Z += GRAVITY;

            // Skip collision detection while a rocket is still in its launch-attachment phase.
            if (!(Type == ProjectileType.Rocket && Time < ROCKET_LAUNCH_DELAY_MS))
                CheckCollisions(gameHero, gameWorld);

            Position += Speed;

            if (Owner is Hero && Position.X > scrollPos + 75f) Active = false;

            switch (Type)
            {
                case ProjectileType.Rocket:

                    // ── Warmup phase: stay attached to the ship ───────────────
                    if (Time < ROCKET_LAUNCH_DELAY_MS)
                    {
                        if (Owner is Hero launchOwner)
                        {
                            Speed    = Vector3.Zero;
                            Position = launchOwner.Position + new Vector3(0f, 3f, 0f);
                        }
                        break;
                    }

                    // First frame after warmup — give an initial forward kick.
                    if (Time - gameTime.ElapsedGameTime.TotalMilliseconds < ROCKET_LAUNCH_DELAY_MS)
                        Speed = new Vector3(0.5f, 0f, 0f);

                    // ── Homing ───────────────────────────────────────────────
                    // Only re-acquire if we don't already have a live target (lock-on behaviour).
                    if (target == null || !target.Active)
                    {
                        Enemy nearest = null;
                        float nearestDistSq = float.MaxValue;
                        var enemies = EnemyController.Instance.Enemies;
                        for (int ei = 0; ei < enemies.Count; ei++)
                        {
                            Enemy e = enemies[ei];
                            if (!e.Active || e.Position.X < scrollPos - 75f)
                                continue;
                            if (e.Position.X < Position.X)
                                continue; // forward filter — ignore enemies behind the rocket
                            float dx = e.Position.X - Position.X;
                            float dy = e.Position.Y - Position.Y;
                            float distSq = dx * dx + dy * dy;
                            if (distSq < nearestDistSq) { nearestDistSq = distSq; nearest = e; }
                        }
                        target = nearest;
                    }
                    
                    if (target != null)
                    {
                        // Direction from rocket to target, normalised.
                        Vector2 toTarget = new Vector2(target.Position.X - Position.X,
                                                       target.Position.Y - Position.Y);
                        float dist = toTarget.Length();
                        if (dist > 0.001f) toTarget /= dist;

                        // Steer current velocity toward the target direction.
                        Vector2 vel2 = new Vector2(Speed.X, Speed.Y);
                        vel2 += toTarget * ROCKET_STEER_STRENGTH;

                        // Maintain constant speed (normalise then scale) so steering
                        // doesn't slow the rocket when turning sharply.
                        float currentSpeed = vel2.Length();
                        if (currentSpeed > 0.001f)
                            vel2 = vel2 / currentSpeed * Math.Min(currentSpeed, ROCKET_TOP_SPEED);

                        Speed.X = vel2.X;
                        Speed.Y = vel2.Y;
                        Rotation = Matrix.CreateRotationZ(Helper.V2ToAngle(vel2));
                    }

                    // Only trail particles once actually flying.
                    if (Helper.Random.Next(5) == 1)
                        ParticleController.Instance.Spawn(Position + new Vector3(Helper.RandomFloat(-0.1f,1f),Helper.RandomFloat(-0.1f,1f),0f) ,
                                                      Vector3.Zero,
                                                      0.3f,
                                                      new Color(new Vector3(1f, Helper.RandomFloat(0f, 1.0f), 0f) * Helper.RandomFloat(0.5f, 1.0f)),
                                                      1000,
                                                      false);

                    break;
               
            }

            if (Time >= Life)
            {
                if (Type == ProjectileType.Rocket)
                {
                    ParticleController.Instance.SpawnExplosion(Position);
                }
                Active = false;
            }

            
        }

        void CheckCollisions(Hero gameHero, VoxelWorld gameWorld)
        {
            Vector3 worldSpace; 
            switch (Type)
            {
                case ProjectileType.Laser1:
                case ProjectileType.Laser2:
                case ProjectileType.Laser3:
                case ProjectileType.Laser4:
                case ProjectileType.Rocket:
                    for (float d = 0f; d < 1f; d += 0.1f)
                    {
                        if (!Active) continue;

                        worldSpace = gameWorld.FromScreenSpace(Position + (d * ((Position + Speed) - Position)));
                        Voxel v = gameWorld.GetVoxel(Position + (d * ((Position + Speed) - Position)));

                        if (v.Active && Active)
                        {
                            if (v.Destructable >= 1 && Owner is Hero)
                            {
                                gameWorld.Explode(Position + (d * ((Position + Speed) - Position)), Type!= ProjectileType.Rocket?3f:5f);
                                gameWorld.Explode((Position + (d * ((Position + Speed) - Position))) + new Vector3(0f, 0f, -3f), Type != ProjectileType.Rocket ? 3f : 5f);
                                gameWorld.Explode((Position + (d * ((Position + Speed) - Position))) + new Vector3(0f, 0f, 3f), Type != ProjectileType.Rocket ? 3f : 5f);

                                //gameWorld.SetVoxelActive((int)worldSpace.X, (int)worldSpace.Y, (int)worldSpace.Z, false);
                                //for (int i = 0; i < 4; i++) ParticleController.Instance.Spawn(Position, new Vector3(-0.05f + ((float)Helper.Random.NextDouble() * 0.1f), -0.05f + ((float)Helper.Random.NextDouble() * 0.1f), -((float)Helper.Random.NextDouble() * 0.5f)), 0.25f, new Color(v.SR, v.SG, v.SB), 1000, true);
                               
                            }
                            Active = false;
                        }
                       
                        if(Owner is Enemy)
                            if (!gameHero.Dead && gameHero.CollisionBox.Contains(Position + (d * ((Position + Speed) - Position))) == ContainmentType.Contains)
                            {
                                gameHero.DoHit(Position + (d * ((Position + Speed) - Position)), this);
                                Active = false;
                                if(Type== ProjectileType.Rocket) ParticleController.Instance.SpawnExplosion(Position);
                            }

                        if (Owner is Hero)
                        {
                            var enList = EnemyController.Instance.Enemies;
                            Vector3 checkPoint = Position + (d * ((Position + Speed) - Position));
                            for (int ei = 0; ei < enList.Count; ei++)
                            {
                                Enemy e = enList[ei];
                                if (!e.Active) continue;
                                if (e.boundingSphere.Contains(checkPoint) == ContainmentType.Contains)
                                {
                                    e.DoHit(checkPoint, Speed, Damage);
                                    Active = false;
                                    if (Type == ProjectileType.Rocket) ParticleController.Instance.SpawnExplosion(Position);
                                }
                            }
                        }

                        
                    }
                    break;
            }
        }

        
    }
}
