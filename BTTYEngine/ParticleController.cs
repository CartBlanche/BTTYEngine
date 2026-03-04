using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BTTYEngine
{
    public class ParticleController
    {
        const int MAX_PARTICLES = 300;

        public static ParticleController Instance;

        GraphicsDevice graphicsDevice;

        VertexPositionNormalColor[] verts = new VertexPositionNormalColor[MAX_PARTICLES * 24];
        short[] indexes = new short[MAX_PARTICLES * 36];

        Particle[] Particles;

        int currentParticleCount = 0;

        BasicEffect drawEffect;

        double updateTime = 0;
        double updateTargetTime = 10;

        int parts = 0;

        public ParticleController(GraphicsDevice gd)
        {
            Instance = this;

            graphicsDevice = gd;

            Particles = new Particle[MAX_PARTICLES];
            for (int i = 0; i < MAX_PARTICLES; i++) Particles[i] = new Particle();

            drawEffect = new BasicEffect(gd)
            {
                VertexColorEnabled = true
            };

        }

        public void Update(GameTime gameTime, ICamera gameCamera, VoxelWorld gameWorld)
        {
            int activeCount = 0;
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                Particle p = Particles[i];
                if (!p.Active) continue;
                p.Update(gameTime, gameWorld);
                activeCount++;
            }
            currentParticleCount = activeCount;

            updateTime += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (updateTime >= updateTargetTime)
            {
                updateTime = 0;
                parts = 0;
                for (int i = 0; i < MAX_PARTICLES; i++)
                {
                    Particle p = Particles[i];
                    if (!p.Active) continue;
                    ParticleCube.Create(ref verts, ref indexes, p.Position, parts, p.Scale / 2, p.Color);
                    parts++;
                }
            }

            drawEffect.World = gameCamera.WorldMatrix;
            drawEffect.View = gameCamera.ViewMatrix;
            drawEffect.Projection = gameCamera.ProjectionMatrix;
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

        public void SpawnExplosion(Vector3 Position)
        {
            for (int i = 0; i < 20; i++)
            {
                Color c = new Color(new Vector3(1.0f, (float)Helper.Random.NextDouble(), 0.0f)) * (0.7f + ((float)Helper.Random.NextDouble() * 0.3f));
                Spawn(Position, new Vector3(-0.2f + ((float)Helper.Random.NextDouble() * 0.4f), -0.2f + ((float)Helper.Random.NextDouble() * 0.4f), -((float)Helper.Random.NextDouble() * 0.5f)), 0.25f, c, 2000, true);
            }
            for (int i = 0; i < 20; i++)
            {
                Color c = new Color(new Vector3(1.0f, (float)Helper.Random.NextDouble(), 0.0f)) * (0.7f + ((float)Helper.Random.NextDouble() * 0.3f));
                Spawn(Position, new Vector3(-0.05f + ((float)Helper.Random.NextDouble() * 0.1f), -0.05f + ((float)Helper.Random.NextDouble() * 0.1f), -((float)Helper.Random.NextDouble() * 1f)), 0.25f, c, 2000, true);
            }
        }

        public void Spawn(Vector3 pos, Vector3 speed, float scale, Color col, double life, bool gravity)
        {
            // Find first inactive slot; fall back to the oldest active particle.
            Particle p = null;
            Particle oldest = null;
            double oldestTime = -1.0;
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                Particle candidate = Particles[i];
                if (!candidate.Active)
                {
                    p = candidate;
                    break;
                }
                if (candidate.Time > oldestTime)
                {
                    oldestTime = candidate.Time;
                    oldest = candidate;
                }
            }
            if (p == null) p = oldest;
            p.Spawn(pos, speed, scale, col, life, gravity);
        }




        internal void Reset()
        {
            for (int i = 0; i < MAX_PARTICLES; i++) Particles[i].Active = false;
        }
    }
}
