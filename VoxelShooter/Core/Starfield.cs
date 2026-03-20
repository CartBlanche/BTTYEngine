using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

using BTTYEngine;

namespace VoxelShooter
{
    public class Starfield
    {
        const int MAX_PARTICLES = 500;

        public static Starfield Instance;

        GraphicsDevice graphicsDevice;

        VertexPositionNormalColor[] verts = new VertexPositionNormalColor[MAX_PARTICLES * 24];
        short[] indexes = new short[MAX_PARTICLES * 36];

        List<Particle> Particles; 

        int currentParticleCount = 0;

        BasicEffect drawEffect;

        double updateTime = 0;
        double updateTargetTime = 0;

        int parts = 0;

        public Starfield(GraphicsDevice gd)
        {
            Instance = this;

            graphicsDevice = gd;

            Particles = new List<Particle>(MAX_PARTICLES);
            for (int i = 0; i < MAX_PARTICLES; i++) Particles.Add(new Particle());

            drawEffect = new BasicEffect(gd)
            {
                VertexColorEnabled = true
            };


            drawEffect.View = Matrix.CreateLookAt(new Vector3(0, 0, -100), new Vector3(0, 0, 100), Vector3.Down);
            drawEffect.World = Matrix.Identity;
            drawEffect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, gd.Viewport.AspectRatio, 0.1f, 200f);

        }

        public void Update(GameTime gameTime, ICamera gameCamera, VoxelWorld gameWorld, float scrollSpeed)
        {
            
            int activeCount = 0;
            for (int i = 0; i < Particles.Count; i++)
            {
                Particle p = Particles[i];
                if (!p.Active) continue;
                p.UpdateStarField(gameTime, gameWorld, scrollSpeed);
                activeCount++;
            }
            currentParticleCount = activeCount;

            updateTime += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (updateTime >= updateTargetTime)
            {
                updateTime = 0;

                parts = 0;
                for (int i = 0; i < Particles.Count; i++)
                {
                    Particle p = Particles[i];
                    if (!p.Active) continue;
                    ParticleCube.Create(ref verts, ref indexes, p.Position, parts, p.Scale / 2, p.Color);
                    parts++;
                }
            }

            //drawEffect.World = Matrix.Identity;
            //drawEffect.View = gameCamera.viewMatrix;
            //drawEffect.Projection = gameCamera.projectionMatrix;
        }

        public void Draw()
        {
            if (currentParticleCount == 0) return;
            foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, verts, 0, currentParticleCount * 24, indexes, 0, currentParticleCount * 12);
            }
        }

        public void Spawn(Vector3 pos, Vector3 speed, float scale, Color col, double life, bool gravity)
        {
            Particle p = null;
            Particle oldest = null;
            double oldestTime = -1.0;
            for (int i = 0; i < Particles.Count; i++)
            {
                Particle candidate = Particles[i];
                if (!candidate.Active) { p = candidate; break; }
                if (candidate.Time > oldestTime) { oldestTime = candidate.Time; oldest = candidate; }
            }
            if (p == null) p = oldest;
            p.Spawn(pos, speed, scale, col, life, gravity);
        }

        internal void Reset()
        {
            foreach (Particle p in Particles) p.Active = false;
        }
    }
}
