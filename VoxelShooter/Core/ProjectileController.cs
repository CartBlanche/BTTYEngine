using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using BTTYEngine;

namespace VoxelShooter
{
    public class ProjectileController
    {
        public static ProjectileController Instance;
        GraphicsDevice graphicsDevice;

        List<Projectile> Projectiles; 

        VoxelEffect _voxelEffect;

        VoxelSprite projectileStrip;

        static readonly Matrix _rotX90 = Matrix.CreateRotationX(MathHelper.PiOver2);

        public ProjectileController(GraphicsDevice gd, VoxelEffect voxelEffect)
        {
            Instance = this;
            graphicsDevice = gd;
            Projectiles = new List<Projectile>();
            _voxelEffect = voxelEffect;
        }

        public void LoadContent(ContentManager content)
        {
            projectileStrip = new VoxelSprite(5, 5, 5);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "projectiles.bvx"), ref projectileStrip);
        }

        public void Update(GameTime gameTime, ICamera gameCamera, Hero gameHero, VoxelWorld gameWorld, float scrollPos)
        {
            int writeIdx = 0;
            for (int i = 0; i < Projectiles.Count; i++)
            {
                Projectile p = Projectiles[i];
                if (!p.Active) continue;
                p.Update(gameTime, gameHero, gameWorld, scrollPos);
                if (p.Active) Projectiles[writeIdx++] = p;
            }
            while (Projectiles.Count > writeIdx) Projectiles.RemoveAt(Projectiles.Count - 1);
        }

        public void Draw(ICamera gameCamera)
        {
            for (int i = 0; i < Projectiles.Count; i++)
            {
                Projectile p = Projectiles[i];
                _voxelEffect.SetTint(Vector3.One);
                if (p.Type == ProjectileType.Rocket)
                {
                    var rWorld = gameCamera.WorldMatrix * _rotX90 * p.Rotation * Matrix.CreateScale(0.5f) * Matrix.CreateTranslation(p.Position);
                    _voxelEffect.Apply(rWorld, gameCamera.ViewMatrix, gameCamera.ProjectionMatrix);
                    var chunk = projectileStrip.AnimChunks[4];
                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, chunk.VertexArray, 0, chunk.VertexArray.Length, chunk.IndexArray, 0, chunk.VertexArray.Length / 2);
                }
                else
                {
                    var pWorld = gameCamera.WorldMatrix * _rotX90 * p.Rotation * Matrix.CreateTranslation(p.Position);
                    _voxelEffect.Apply(pWorld, gameCamera.ViewMatrix, gameCamera.ProjectionMatrix);
                    var chunk = projectileStrip.AnimChunks[(int)p.Type];
                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, chunk.VertexArray, 0, chunk.VertexArray.Length, chunk.IndexArray, 0, chunk.VertexArray.Length / 2);
                }
            }
        }

        public void Reset()
        {
            Projectiles.Clear();
        }

        public void RegisterPointLights(VoxelWorld world)
        {
            foreach (var p in Projectiles)
            {
                if (!p.Active) continue;
                if (p.Type == ProjectileType.Rocket)
                {
                    world.AddPointLight(new VoxelWorld.PointLight
                    {
                        Position  = p.Position,
                        Color     = new Color(255, 190, 80),
                        Radius    = 40f,
                        Intensity = 1.2f,
                    });
                }
                else
                {
                    world.AddPointLight(new VoxelWorld.PointLight
                    {
                        Position  = p.Position,
                        Color     = new Color(80, 180, 255),
                        Radius    = 15f,
                        Intensity = 0.6f,
                    });
                }
            }
        }

        public void Spawn(ProjectileType type, object owner, Vector3 pos, Matrix rot, Vector3 speed, float damage, double life, bool gravity)
        {
            Projectile p = null;
            switch(type)
            {
                case ProjectileType.Laser1:
                case ProjectileType.Laser2:
                case ProjectileType.Laser3:
                case ProjectileType.Laser4:
                case ProjectileType.Rocket:
                    p = new Projectile()
                    {
                        Type = type,
                        Owner = owner,
                        Active = true,
                        Position = pos,
                        Speed = speed,
                        Damage = damage,
                        Rotation = rot,
                        affectedByGravity = gravity,
                        Life = life,
                        Time = 0
                    };
                    break;
            }

            Projectiles.Add(p);
        }




        
    }
}
