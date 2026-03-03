using BepuPhysics;
using BepuPhysics.Collidables;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VoxelShooter
{
    public class Hero : IEntity
    {
        public Vector3 Position;
        public Vector3 Speed;

        public float Health = 100f;
        public float XP = 0f;
        public bool Dead = false;

        public BoundingBox CollisionBox;
        Vector3 collisionBoxSize;

        Vector3 tempSpeed;

        BasicEffect drawEffect;
        VoxelSprite shipSprite;

        float moveSpeed = 0.5f;

        // Visual banking angle, smoothly follows vertical speed for a natural tilt
        float bankAngle = 0f;

        double fireCooldown = 0;
        double rocketCooldown = 0;
        public float hitAlpha = 0f;
        double _hitImmunityMs = 0;
        Vector3 _knockback;

        int powerupLevel = 0;

        bool orbActive;
        float orbAngle = -MathHelper.PiOver2;

        // [SFX-LASER]   SoundEffect _sfxLaser;
        // [SFX-HIT]     SoundEffect _sfxHit;
        // [SFX-POWERUP] SoundEffect _sfxPowerup;
        Vector3 orbPosition;
        Vector3 orbRotation;

        public float[] xpLevels = new float[] { 6f, 18f, 40f, 68f, 100f };
        //public float[] xpLevels = new float[] { 1f, 3f, 5f, 8f, 10f };

        BodyHandle _physicsBody;

        public Hero()
        {
            Position = new Vector3(-150f, -45f, 5f);
        }

        public void LoadContent(ContentManager content, GraphicsDevice gd)
        {
            shipSprite = new VoxelSprite(15, 15, 15);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "ship.bvx"), ref shipSprite);

            drawEffect = new BasicEffect(gd)
            {
                VertexColorEnabled = true
            };

            collisionBoxSize = new Vector3(10, 4f, 2f);
            CollisionBox = new BoundingBox();

            // [SFX-LASER]   _sfxLaser   = content.Load<SoundEffect>("Sound/laser");
            // [SFX-HIT]     _sfxHit     = content.Load<SoundEffect>("Sound/hit");
            // [SFX-POWERUP] _sfxPowerup = content.Load<SoundEffect>("Sound/powerup");
        }

        public void InitPhysics(PhysicsManager physics)
        {
            var sphere = new Sphere(3f);
            var inertia = sphere.ComputeInertia(1f);
            inertia.InverseInertiaTensor = default; // zero = locked rotation (no tumbling)
            var shapeIndex = physics.Simulation.Shapes.Add(sphere);
            _physicsBody = physics.Simulation.Bodies.Add(
                BodyDescription.CreateDynamic(
                    new RigidPose(new System.Numerics.Vector3(Position.X, Position.Y, Position.Z)),
                    new BodyVelocity(),
                    inertia,
                    new CollidableDescription(shapeIndex, 0.1f),
                    new BodyActivityDescription(0.01f)));
            EntityRegistry.Instance.Register(_physicsBody, this);
        }

        public void DestroyPhysics(PhysicsManager physics)
        {
            EntityRegistry.Instance.Unregister(_physicsBody);
            physics.Simulation.Bodies.Remove(_physicsBody);
        }

        // Damage is applied by the enemy's OnCollision calling hero.DoHit.
        public void OnCollision(IEntity other) { }

        public void Update(GameTime gameTime, ICamera gameCamera, VoxelWorld gameWorld, float scrollSpeed)
        {
            // Read authoritative position from Bepu (already stepped this frame in VoxelShooter.Update).
            var body = PhysicsManager.Instance.Simulation.Bodies.GetBodyReference(_physicsBody);
            var bpos = body.Pose.Position;
            Position = new Microsoft.Xna.Framework.Vector3(bpos.X, bpos.Y, bpos.Z);

            Vector2 v2Pos= new Vector2(Position.X,Position.Y);

            tempSpeed = Speed + _knockback;
            tempSpeed.X += scrollSpeed;
            _knockback *= 0.82f; // decay knockback each frame (~10 frames to reach 10%)

            CollisionBox.Min = Position - (collisionBoxSize/2);
            CollisionBox.Max = Position + (collisionBoxSize/2);

            CheckCollisions(gameWorld, gameCamera);

            // Write clamped velocity to Bepu for next frame's physics step.
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt > 0f)
            {
                var vel = tempSpeed / dt;
                body.Velocity.Linear = new System.Numerics.Vector3(vel.X, vel.Y, vel.Z);
                body.Awake = true;
            }

            if(Helper.Random.Next(3)==1)
                ParticleController.Instance.Spawn(Position + new Vector3(-4f, 0f, 0f),
                                              new Vector3(Helper.RandomFloat(-0.5f, -0.3f), Helper.RandomFloat(-0.05f, 0.05f), 0f),
                                              0.3f,
                                              new Color(new Vector3(1f, Helper.RandomFloat(0f, 1.0f), 0f) * Helper.RandomFloat(0.5f, 1.0f)),
                                              1000,
                                              false);

            fireCooldown -= gameTime.ElapsedGameTime.TotalMilliseconds;
            rocketCooldown -= gameTime.ElapsedGameTime.TotalMilliseconds;
            _hitImmunityMs -= gameTime.ElapsedGameTime.TotalMilliseconds;

            if (hitAlpha > 0f) hitAlpha -= 0.1f;

            if (orbActive)
            {

                orbAngle += 0.1f;
                orbPosition = new Vector3(Helper.PointOnCircle(ref v2Pos, 15f, orbAngle), Position.Z);
                orbRotation += new Vector3(0.1f, -0.01f, 0.025f);

                foreach(Enemy e in EnemyController.Instance.Enemies)
                {
                    if (e.boundingSphere.Contains(new BoundingSphere(orbPosition,1.5f)) == ContainmentType.Intersects)
                    {
                        e.DoHit(orbPosition, Vector3.Zero, 3f);
                    }
                }
            }

            CheckLevelUp();

            drawEffect.Projection = gameCamera.ProjectionMatrix;
            drawEffect.View = gameCamera.ViewMatrix;

            // Bank into vertical movement: positive Y = down the screen, so nose dips down → positive Z rotation
            float targetBank = Speed.Y * 0.4f;
            bankAngle = MathHelper.Lerp(bankAngle, targetBank, 0.12f);
        }

        public void DoHit(Vector3 pos, Projectile proj)
        {
            if (_hitImmunityMs > 0) return;
            hitAlpha = 1f;
            _hitImmunityMs = 500;
            // [SFX-HIT] _sfxHit.Play(0.8f, 0f, 0f);

            if (proj != null) // A null projectile means an enemy collided with the player
                Health -= proj.Damage/2f;
            else
                Health -= 0.1f;
        }

        // Damage overload used by velocity-scaled impacts (e.g. asteroids).
        public void DoHit(Vector3 pos, float damage)
        {
            if (_hitImmunityMs > 0) return;
            hitAlpha = 1f;
            _hitImmunityMs = 500;
            // [SFX-HIT] _sfxHit.Play(0.8f, 0f, 0f);
            Health -= damage;
        }

        // Push the ship in the given direction for a few frames (decays at 18%/frame).
        public void ApplyKnockback(Vector3 impulse) => _knockback += impulse;

        public void Draw(GraphicsDevice gd)
        {
            drawEffect.World = Matrix.CreateRotationZ(bankAngle) * Matrix.CreateTranslation(Position);

            drawEffect.DiffuseColor = new Vector3(1f, 1f - hitAlpha, 1f - hitAlpha);
            foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                AnimChunk c = shipSprite.AnimChunks[0];
                if (c == null) continue;

                if (c == null || c.VertexArray == null || c.VertexArray.Length == 0) continue;
                gd.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, c.VertexArray, 0, c.VertexArray.Length, c.IndexArray, 0, c.VertexArray.Length / 2);

               
            }

            
            drawEffect.World = Matrix.CreateScale(0.75f) * (Matrix.CreateRotationX(orbRotation.X) * Matrix.CreateRotationY(orbRotation.Y) * Matrix.CreateRotationZ(orbRotation.Z)) * Matrix.CreateTranslation(orbPosition);

            drawEffect.DiffuseColor = Color.White.ToVector3();

            foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                AnimChunk c = shipSprite.AnimChunks[1];
                if (c == null) continue;

                if (c == null || c.VertexArray == null || c.VertexArray.Length == 0) continue;
                gd.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, c.VertexArray, 0, c.VertexArray.Length, c.IndexArray, 0, c.VertexArray.Length / 2);
            }
        }

        public void Move(Vector2 virtualJoystick)
        {
            //if (Dead) return;
            //if (exitReached) return;

            Vector3 dir = new Vector3(virtualJoystick, 0f);

            Speed = dir * moveSpeed;
        }

        int fireswitch = 0;
        public void Fire()
        {
            switch(powerupLevel)
            {
                case 0:
                    if (fireCooldown <= 0)
                    {
                        fireCooldown = 300;
                        ProjectileController.Instance.Spawn(ProjectileType.Laser1, this, Position+ new Vector3(3f,0f,0f), Matrix.Identity, new Vector3(2f, 0f, 0f), 2f, 2000, false);
                        // [SFX-LASER] _sfxLaser.Play(0.4f, 0f, 0f);
                    }
                    break;
                case 1:
                case 2:
                    if (fireCooldown <= 0)
                    {
                        fireCooldown = 200;
                        ProjectileController.Instance.Spawn(ProjectileType.Laser2, this, Position + new Vector3(3f, -1f + (2f*fireswitch),0f), Matrix.Identity, new Vector3(2f, 0f, 0f), 3f, 2000, false);
                        fireswitch = 1 - fireswitch;
                        // [SFX-LASER] _sfxLaser.Play(0.4f, 0.1f, 0f);
                    }
                    break;
                case 3:
                case 4:
                case 5:
                    if (fireCooldown <= 0)
                    {
                        Vector2 v2Pos= new Vector2(Position.X,Position.Y);

                        fireCooldown = 150;
                        if (fireswitch == 0)
                        {
                            ProjectileController.Instance.Spawn(ProjectileType.Laser3, this, new Vector3(Helper.PointOnCircle(ref v2Pos, 3f, 0f),Position.Z), Matrix.Identity, new Vector3(Helper.AngleToVector(0f,3f), 0f), 4f, 2000, false);
                            ProjectileController.Instance.Spawn(ProjectileType.Laser3, this, new Vector3(Helper.PointOnCircle(ref v2Pos, 3f, -0.4f), Position.Z), Matrix.CreateRotationZ(-0.4f), new Vector3(Helper.AngleToVector(-0.4f, 3f), 0f), 4f, 2000, false);
                            ProjectileController.Instance.Spawn(ProjectileType.Laser3, this, new Vector3(Helper.PointOnCircle(ref v2Pos, 3f, 0.4f), Position.Z), Matrix.CreateRotationZ(0.4f), new Vector3(Helper.AngleToVector(0.4f, 3f), 0f), 4f, 2000, false);
                            
                        }
                        else
                        {
                            ProjectileController.Instance.Spawn(ProjectileType.Laser3, this, new Vector3(Helper.PointOnCircle(ref v2Pos, 3f, -0.2f), Position.Z), Matrix.CreateRotationZ(-0.2f), new Vector3(Helper.AngleToVector(-0.2f, 3f), 0f), 4f, 2000, false);
                            ProjectileController.Instance.Spawn(ProjectileType.Laser3, this, new Vector3(Helper.PointOnCircle(ref v2Pos, 3f, 0.2f), Position.Z), Matrix.CreateRotationZ(0.2f), new Vector3(Helper.AngleToVector(0.2f, 3f), 0f), 4f, 2000, false);
                            
                        }

                        if (powerupLevel >= 4)
                        {
                            if(fireswitch==0) ProjectileController.Instance.Spawn(ProjectileType.Laser4, this, new Vector3(Helper.PointOnCircle(ref v2Pos, 3f, -MathHelper.PiOver2), Position.Z), Matrix.CreateRotationZ(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.PiOver2), new Vector3(Helper.AngleToVector(-MathHelper.PiOver2, 3f), 0f), 5f, 2000, false);
                            else ProjectileController.Instance.Spawn(ProjectileType.Laser4, this, new Vector3(Helper.PointOnCircle(ref v2Pos, 3f, MathHelper.PiOver2), Position.Z), Matrix.CreateRotationZ(MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.PiOver2), new Vector3(Helper.AngleToVector(MathHelper.PiOver2, 3f), 0f), 5f, 2000, false);
                            
                        }

                        fireswitch = 1 - fireswitch;
                        // [SFX-LASER] _sfxLaser.Play(0.4f, 0.2f, 0f);
                    }
                    break;
            }

            if (powerupLevel >= 5)
            {
                if (rocketCooldown <= 0)
                {
                    ProjectileController.Instance.Spawn(ProjectileType.Rocket, this, Position + new Vector3(0f, 3f, 0f), Matrix.Identity, new Vector3(0f, 0.1f, 0f), 10f, 5000, false);

                    rocketCooldown = 2000;
                }
            }

            
        }

        void CheckLevelUp()
        {
            if (XP > 100f) XP = 100f;

            for(int i=1;i<=5;i++)
                if (powerupLevel < i && XP >= xpLevels[i-1]) Powerup();
        }

        private void Powerup()
        {
            powerupLevel++;
            if (powerupLevel >= 2) orbActive = true;
            // [SFX-POWERUP] _sfxPowerup.Play(1f, 0f, 0f);
        }

        void CheckCollisions(VoxelWorld world, ICamera gameCamera)
        {
            float radiusSweep = 0.1f;
            Vector2 v2Pos = new Vector2(Position.X, Position.Y);
            float checkHeight = Position.Z - 1f;
            Voxel checkVoxel;
            Vector3 checkPos;

            if (tempSpeed.Y < 0f)
            {
                for (float a = -MathHelper.PiOver2 - radiusSweep; a < -MathHelper.PiOver2 + radiusSweep; a += 0.02f)
                {
                    checkPos = new Vector3(Helper.PointOnCircle(ref v2Pos, collisionBoxSize.Y/2, a), checkHeight);
                    checkVoxel = world.GetVoxel(checkPos);
                    if ((checkVoxel.Active && world.CanCollideWith(checkVoxel.Type)))
                    {
                        tempSpeed.Y = 0f;
                    }
                    if (gameCamera.BoundingFrustum.Contains(checkPos) == ContainmentType.Disjoint) tempSpeed.Y = 0;
                }
            }
            if (tempSpeed.Y > 0f)
            {
                for (float a = MathHelper.PiOver2 - radiusSweep; a < MathHelper.PiOver2 + radiusSweep; a += 0.02f)
                {
                    checkPos = new Vector3(Helper.PointOnCircle(ref v2Pos, collisionBoxSize.Y / 2, a), checkHeight);
                    checkVoxel = world.GetVoxel(checkPos);
                    if ((checkVoxel.Active && world.CanCollideWith(checkVoxel.Type)))
                    {
                        tempSpeed.Y = 0f;
                    }
                    if (gameCamera.BoundingFrustum.Contains(checkPos) == ContainmentType.Disjoint) tempSpeed.Y = 0;                    
                }
            }
            if (tempSpeed.X < 0f)
            {
                for (float a = -MathHelper.Pi - radiusSweep; a < -MathHelper.Pi + radiusSweep; a += 0.02f)
                {
                    checkPos = new Vector3(Helper.PointOnCircle(ref v2Pos, collisionBoxSize.X / 2, a), checkHeight);
                    checkVoxel = world.GetVoxel(checkPos);
                    if ((checkVoxel.Active && world.CanCollideWith(checkVoxel.Type)))
                    {
                        tempSpeed.X = 0f;
                    }
                    if (gameCamera.BoundingFrustum.Contains(checkPos) == ContainmentType.Disjoint) { tempSpeed.X -= Speed.X; break; }
                    
                }
            }
            if (tempSpeed.X > 0f)
            {
                for (float a = -radiusSweep; a < radiusSweep; a += 0.02f)
                {
                    checkPos = new Vector3(Helper.PointOnCircle(ref v2Pos, collisionBoxSize.X / 2, a), checkHeight);
                    checkVoxel = world.GetVoxel(checkPos);
                    if ((checkVoxel.Active && world.CanCollideWith(checkVoxel.Type)))
                    {
                        tempSpeed.X = 0f;
                    }
                    if (gameCamera.BoundingFrustum.Contains(checkPos) == ContainmentType.Disjoint) { tempSpeed.X -= Speed.X; break; }
                    
                }
            }
        }

        
    }
}
