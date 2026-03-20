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

        BasicEffect drawEffect;

        VoxelSprite projectileStrip;

        static readonly Matrix _rotX90 = Matrix.CreateRotationX(MathHelper.PiOver2);

        public ProjectileController(GraphicsDevice gd)
        {
            Instance = this;
            graphicsDevice = gd;
            Projectiles = new List<Projectile>();

            drawEffect = new BasicEffect(gd)
            {
                VertexColorEnabled = true,
                LightingEnabled    = true,
            };
            drawEffect.DirectionalLight0.Enabled = true;

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

            drawEffect.World      = gameCamera.WorldMatrix;
            drawEffect.View       = gameCamera.ViewMatrix;
            drawEffect.Projection = gameCamera.ProjectionMatrix;
            drawEffect.AmbientLightColor              = gameWorld.AmbientColor.ToVector3();
            drawEffect.DirectionalLight0.DiffuseColor = gameWorld.SunColor.ToVector3();
            drawEffect.DirectionalLight0.Direction    = gameWorld.SunDirection;
        }

        public void Draw(ICamera gameCamera)
        {
            for (int i = 0; i < Projectiles.Count; i++)
            {
                Projectile p = Projectiles[i];
                if (p.Type == ProjectileType.Rocket)
                {
                    drawEffect.World = gameCamera.WorldMatrix *
                                       _rotX90 *
                                       p.Rotation *
                                       Matrix.CreateScale(0.5f) *
                                       Matrix.CreateTranslation(p.Position);
                    foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        var chunk = projectileStrip.AnimChunks[4];
                        graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, chunk.VertexArray, 0, chunk.VertexArray.Length, chunk.IndexArray, 0, chunk.VertexArray.Length / 2);
                    }
                }
                else
                {
                    drawEffect.World = gameCamera.WorldMatrix *
                                       _rotX90 *
                                       p.Rotation *
                                       Matrix.CreateTranslation(p.Position);
                    foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        var chunk = projectileStrip.AnimChunks[(int)p.Type];
                        graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, chunk.VertexArray, 0, chunk.VertexArray.Length, chunk.IndexArray, 0, chunk.VertexArray.Length / 2);
                    }
                }
            }
        }

        public void Reset()
        {
            Projectiles.Clear();
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
