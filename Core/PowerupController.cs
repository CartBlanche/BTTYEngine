using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using BTTYEngine;

namespace VoxelShooter
{
    public class PowerupController
    {
        const int MAX_POWERUPS = 100;

        // Cube geometry constants, 24 verts / 36 indices per cube (6 faces × 4 verts / 6 faces × 6 indices)
        const int VERTS_PER_CUBE  = 24;
        const int INDICES_PER_CUBE = 36;

        public static PowerupController Instance;

        GraphicsDevice graphicsDevice;

        // Shared unit-cube centred at origin, built once; world matrix moves/rotates each pickup
        VertexPositionNormalColor[] unitCubeVerts   = new VertexPositionNormalColor[VERTS_PER_CUBE];
        short[]                     unitCubeIndices = new short[INDICES_PER_CUBE];

        List<Powerup> Powerups;

        BasicEffect drawEffect;

        // Camera matrices cached from last Update so Draw can use them
        Matrix cameraView;
        Matrix cameraProjection;

        // Global time for the pulse glow animation
        float pulseTime = 0f;

        public PowerupController(GraphicsDevice gd)
        {
            Instance = this;
            graphicsDevice = gd;

            Powerups = new List<Powerup>(MAX_POWERUPS);
            for (int i = 0; i < MAX_POWERUPS; i++) Powerups.Add(new Powerup());
        }

        public void LoadContent(ContentManager content)
        {
            // Build the unit cube once; we colour it white so DiffuseColor drives the glow tint
            ParticleCube.Create(ref unitCubeVerts, ref unitCubeIndices, Vector3.Zero, 0, 0.5f, Color.White);

            drawEffect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled    = false,
            };
        }

        public void Update(GameTime gameTime, ICamera gameCamera, VoxelWorld gameWorld, Hero gameHero, float scrollPos)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            pulseTime += dt;

            foreach (Powerup p in Powerups.Where(part => part.Active))
            {
                p.Update(gameTime, gameWorld, gameHero, scrollPos);
            }

            cameraView       = gameCamera.ViewMatrix;
            cameraProjection = gameCamera.ProjectionMatrix;
        }

        public void Draw()
        {
            var activePowerups = Powerups.Where(p => p.Active).ToList();
            if (activePowerups.Count == 0) return;

            // Pulse: Yellow (dim) → White (full brightness) for a real glow swing
            float pulse     = 0.5f + 0.5f * (float)Math.Sin(pulseTime * 4.0);
            Color glowColor = Color.Lerp(Color.Yellow, Color.White, pulse);

            // Rebuild the unit-cube verts with the current glow colour each frame
            ParticleCube.Create(ref unitCubeVerts, ref unitCubeIndices, Vector3.Zero, 0, 0.5f, glowColor);

            drawEffect.View       = cameraView;
            drawEffect.Projection = cameraProjection;

            // Additive blending makes bright colours accumulate to white, the real glow trick
            var prevBlend        = graphicsDevice.BlendState;
            var prevDepthStencil = graphicsDevice.DepthStencilState;
            graphicsDevice.BlendState        = BlendState.Additive;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead; // don't write depth so cubes don't punch holes

            // Draw each active powerup individually with its own World matrix (spin + translate)
            foreach (Powerup p in activePowerups)
            {
                drawEffect.World =
                    Matrix.CreateRotationY(p.Rotation) *
                    Matrix.CreateRotationX(p.Rotation * 0.6f) *
                    Matrix.CreateTranslation(p.Position);

                foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(
                        PrimitiveType.TriangleList,
                        unitCubeVerts,  0, VERTS_PER_CUBE,
                        unitCubeIndices, 0, INDICES_PER_CUBE / 3);
                }
            }

            graphicsDevice.BlendState        = prevBlend;
            graphicsDevice.DepthStencilState = prevDepthStencil;
        }

        public void Spawn(Vector3 pos)
        {
            Powerup p = Powerups.FirstOrDefault(part => !part.Active);
            if (p == null) return;
            p.Spawn(pos, new Vector3(Helper.RandomFloat(-0.01f, 0.01f), Helper.RandomFloat(-0.01f, 0.01f), 0f));
        }

        public void Spawn(Vector3 pos, Vector3 initialSpeed)
        {
            Powerup p = Powerups.FirstOrDefault(part => !part.Active);
            if (p == null) return;
            p.Spawn(pos, initialSpeed);
        }

        internal void Reset()
        {
            foreach (Powerup p in Powerups) p.Active = false;
        }
    }
}
