using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using System.IO;
using TiledLib;

using BTTYEngine;

namespace VoxelShooter
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class VoxelShooterGame : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        VoxelSprite tilesSprite;
        VoxelWorld gameWorld;

        SideScrollingCamera  sideCamera;
        IsometricCamera      isoCamera;
        FirstPersonCamera    fpCamera;
        TopDownCamera        tdCamera;
        CameraTransitionManager cameraManager;

        int      activeCameraIndex = 1;   // 1=side, 2=iso, 3=fp, 4=topdown
        ICamera[] cameras;                // populated in LoadContent (index 0 unused)

        static readonly string[] cameraNames = { "", "Side-Scrolling", "Isometric", "First Person", "Top-Down" };
        double fps;

        BasicEffect drawEffect;

        EnemyController enemyController;
        ProjectileController projectileController;
        ParticleController particleController;
        PowerupController powerupController;
        Starfield gameStarfield;

        Map gameMap;
        TileLayer tileLayer;

        Hero gameHero;

        float scrollSpeed = 0.2f;
        float scrollDist = 0f;
        float scrollPos = -100f;

        int scrollColumn;

        PhysicsManager physicsManager;

        InputManager<VoxelAction> inputManager = new InputManager<VoxelAction>();

        MapObjectLayer spawnLayer;           // promoted from LoadContent local so RestartGame() can repopulate enemies

        // "You Died!" state — Demon's Souls style death penalty
        float youDiedTimer          = 0f;    // ms counting down from 3200 → 0; non-zero freezes gameplay
        float deathExplosionTimer   = 0f;    // 600ms ship-explosion pause before the overlay appears
        float nextStartHealth       = 100f;  // 100 on first run, fixed at 50 after the first death

        SpriteFont font;

        // [MUS-BGM] Microsoft.Xna.Framework.Media.Song bgm;

        Texture2D hudTex;

        public VoxelShooterGame()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;
            //graphics.IsFullScreen = true;
            graphics.ApplyChanges();

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            font = Content.Load<SpriteFont>("font");
            hudTex = Content.Load<Texture2D>("hud");

            // [MUS-BGM] bgm = Content.Load<Microsoft.Xna.Framework.Media.Song>("Music/bgm");
            // [MUS-BGM] Microsoft.Xna.Framework.Media.MediaPlayer.IsRepeating = true;
            // [MUS-BGM] Microsoft.Xna.Framework.Media.MediaPlayer.Volume = 0.5f;
            // [MUS-BGM] Microsoft.Xna.Framework.Media.MediaPlayer.Play(bgm);

            tilesSprite = new VoxelSprite(16, 16, 16);
            BvxLoader.LoadSprite(Path.Combine(Content.RootDirectory, "tiles.bvx"), ref tilesSprite);

            gameMap = Content.Load<Map>("1");
            tileLayer = (TileLayer)gameMap.GetLayer("tiles");
            spawnLayer = (MapObjectLayer)gameMap.GetLayer("spawns");

            gameWorld = new VoxelWorld(gameMap.Width, 11, 1);

            for(int yy=0;yy<11;yy++)
                for(int xx=0;xx<12;xx++)
                    if(tileLayer.Tiles[xx,yy]!=null) gameWorld.CopySprite(xx*Chunk.X_SIZE, yy*Chunk.Y_SIZE, 0, tilesSprite.AnimChunks[tileLayer.Tiles[xx,yy].Index-1]);

            gameWorld.UpdateWorldMeshes();

            scrollColumn = 12;

            sideCamera          = new SideScrollingCamera(GraphicsDevice, GraphicsDevice.Viewport);
            sideCamera.Position = new Vector3(0f, -(gameWorld.Y_SIZE * Voxel.HALF_SIZE), 0f);
            sideCamera.Target   = sideCamera.Position;

            isoCamera           = new IsometricCamera(GraphicsDevice, GraphicsDevice.Viewport);
            isoCamera.Position  = sideCamera.Position;
            isoCamera.Target    = sideCamera.Target;

            fpCamera          = new FirstPersonCamera(GraphicsDevice, GraphicsDevice.Viewport);
            fpCamera.Position = sideCamera.Position;
            fpCamera.Target   = sideCamera.Target;

            tdCamera          = new TopDownCamera(GraphicsDevice, GraphicsDevice.Viewport);
            tdCamera.Position = sideCamera.Position;
            tdCamera.Target   = sideCamera.Target;

            cameras = new ICamera[] { null, sideCamera, isoCamera, fpCamera, tdCamera };
            // index 0 unused so activeCameraIndex maps 1-4 directly

            cameraManager = new CameraTransitionManager(sideCamera);

            physicsManager = new PhysicsManager();
            physicsManager.Initialize();

            gameHero = new Hero();
            gameHero.LoadContent(Content, GraphicsDevice);
            gameHero.InitPhysics(physicsManager);

            enemyController = new EnemyController(GraphicsDevice);
            enemyController.LoadContent(Content, spawnLayer);
            projectileController = new ProjectileController(GraphicsDevice);
            projectileController.LoadContent(Content);
            particleController = new ParticleController(GraphicsDevice);
            powerupController = new PowerupController(GraphicsDevice);
            powerupController.LoadContent(Content);
            gameStarfield = new Starfield(GraphicsDevice);

            drawEffect = new BasicEffect(GraphicsDevice)
            {
                World = cameraManager.WorldMatrix,
                View = cameraManager.ViewMatrix,
                Projection = cameraManager.ProjectionMatrix,
                VertexColorEnabled = true,
            };

            // Input bindings
            inputManager.Bind(VoxelAction.MoveUp,     Keys.W, Keys.Up);
            inputManager.Bind(VoxelAction.MoveDown,   Keys.S, Keys.Down);
            inputManager.Bind(VoxelAction.MoveLeft,   Keys.A, Keys.Left);
            inputManager.Bind(VoxelAction.MoveRight,  Keys.D, Keys.Right);
            inputManager.Bind(VoxelAction.Fire,       Keys.Space);
            inputManager.Bind(VoxelAction.Fire,       Buttons.A);
            inputManager.Bind(VoxelAction.Quit,       Keys.Escape);
            inputManager.Bind(VoxelAction.Quit,       Buttons.Back);
            inputManager.Bind(VoxelAction.CameraNext, Buttons.RightShoulder);
            inputManager.Bind(VoxelAction.CameraPrev, Buttons.LeftShoulder);
            inputManager.Bind(VoxelAction.Camera1,    Keys.D1);
            inputManager.Bind(VoxelAction.Camera2,    Keys.D2);
            inputManager.Bind(VoxelAction.Camera3,    Keys.D3);
            inputManager.Bind(VoxelAction.Camera4,    Keys.D4);
            inputManager.BindAxis(VoxelAction.Fire,   GamePadAxis.RightTrigger, 1f, 0.1f);
            inputManager.BindAxis(VoxelAction.MoveRight, GamePadAxis.LeftStickX,  1f);
            inputManager.BindAxis(VoxelAction.MoveLeft,  GamePadAxis.LeftStickX, -1f);
            inputManager.BindAxis(VoxelAction.MoveUp,    GamePadAxis.LeftStickY,  1f);
            inputManager.BindAxis(VoxelAction.MoveDown,  GamePadAxis.LeftStickY, -1f);
        }

        /// <summary>
        /// Rebuilds the level from scratch and resets all entities and controllers.
        /// The hero's health is set to <see cref="nextStartHealth"/> (100 on the first
        /// run, 50 on every subsequent run after the first death).
        /// </summary>
        void RestartGame()
        {
            // Rebuild the voxel world from the original map data.
            gameWorld = new VoxelWorld(gameMap.Width, 11, 1);
            for (int yy = 0; yy < 11; yy++)
                for (int xx = 0; xx < 12; xx++)
                    if (tileLayer.Tiles[xx, yy] != null)
                        gameWorld.CopySprite(xx * Chunk.X_SIZE, yy * Chunk.Y_SIZE, 0, tilesSprite.AnimChunks[tileLayer.Tiles[xx, yy].Index - 1]);
            gameWorld.UpdateWorldMeshes();

            // Reset level-scroll state back to the beginning.
            scrollSpeed  = 0.2f;
            scrollDist   = 0f;
            scrollPos    = -100f;
            scrollColumn = 12;

            // Snap all cameras back to the world start position.
            Vector3 camStart = new Vector3(0f, -(gameWorld.Y_SIZE * Voxel.HALF_SIZE), 0f);
            cameraManager.Position = camStart;
            cameraManager.Target   = camStart;

            // Clear and repopulate all controllers.
            enemyController.Reset(spawnLayer);
            projectileController.Reset();
            powerupController.Reset();
            gameStarfield.Reset();

            // Restart the hero with the correct starting health (100 first time, 50 thereafter).
            gameHero.ResetForRestart(physicsManager, nextStartHealth);
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            physicsManager?.Dispose();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            inputManager.BeginInputProcessing();

            if (inputManager.IsHeld(VoxelAction.Quit))
                this.Exit();

            if (!IsActive) return;

            // Freeze all gameplay during the "You Died!" overlay — keep camera and FPS alive.
            if (youDiedTimer > 0f)
            {
                youDiedTimer -= (float)gameTime.ElapsedGameTime.TotalMilliseconds;
                if (youDiedTimer <= 0f) { youDiedTimer = 0f; RestartGame(); }
                inputManager.EndInputProcessing();
                cameraManager.Update(gameTime, gameWorld);
                drawEffect.View  = cameraManager.ViewMatrix;
                drawEffect.World = cameraManager.WorldMatrix;
                double deadSecs = gameTime.ElapsedGameTime.TotalSeconds;
                if (deadSecs > 0) fps = MathHelper.Lerp((float)fps, (float)(1.0 / deadSecs), 0.05f);
                base.Update(gameTime);
                return;
            }

            // Ship-explosion phase: gameplay frozen but particles keep simulating.
            if (deathExplosionTimer > 0f)
            {
                deathExplosionTimer -= (float)gameTime.ElapsedGameTime.TotalMilliseconds;
                if (deathExplosionTimer <= 0f)
                {
                    deathExplosionTimer = 0f;
                    nextStartHealth     = 50f;
                    youDiedTimer        = 3200f;
                }
                inputManager.EndInputProcessing();
                cameraManager.Update(gameTime, gameWorld);
                particleController.Update(gameTime, cameraManager, gameWorld);
                drawEffect.View  = cameraManager.ViewMatrix;
                drawEffect.World = cameraManager.WorldMatrix;
                double explodeSecs = gameTime.ElapsedGameTime.TotalSeconds;
                if (explodeSecs > 0) fps = MathHelper.Lerp((float)fps, (float)(1.0 / explodeSecs), 0.05f);
                base.Update(gameTime);
                return;
            }

            // Camera selection: keys 1-4, RB=next, LB=prev
            {
                int requested = 0;
                if      (inputManager.IsPressed(VoxelAction.Camera1)) requested = 1;
                else if (inputManager.IsPressed(VoxelAction.Camera2)) requested = 2;
                else if (inputManager.IsPressed(VoxelAction.Camera3)) requested = 3;
                else if (inputManager.IsPressed(VoxelAction.Camera4)) requested = 4;

                if (requested == 0 && inputManager.IsPressed(VoxelAction.CameraNext))
                    requested = (activeCameraIndex % 4) + 1;   // 1→2→3→4→1

                if (requested == 0 && inputManager.IsPressed(VoxelAction.CameraPrev))
                    requested = ((activeCameraIndex - 2 + 4) % 4) + 1;  // 1→4→3→2→1

                if (requested != 0 && requested != activeCameraIndex)
                {
                    activeCameraIndex = requested;
                    cameraManager.TransitionTo(cameras[activeCameraIndex], 1.5f);
                }
            }

            if (Helper.Random.Next(10) == 1)
            {
                Vector3 pos = new Vector3(100f, -(gameWorld.Y_SIZE * Voxel.HALF_SIZE) + (-50f+((float)Helper.Random.NextDouble()*100f)), 5f + ((float)Helper.Random.NextDouble()*10f));
                Vector3 col = (Vector3.One * 0.5f) + (Vector3.One*((float)Helper.Random.NextDouble()*0.5f));
                if(scrollSpeed>0f) gameStarfield.Spawn(pos, new Vector3((-0.1f-((float)Helper.Random.NextDouble()*1f)) * 5f, 0f, 0f), 0.5f, new Color(col), 20000, false);
            }

            if (scrollPos < (gameWorld.X_CHUNKS-11) * (Chunk.X_SIZE * Voxel.SIZE))
            {
                scrollDist += (scrollSpeed*1.5f);
                scrollPos += scrollSpeed;
                cameraManager.Target = new Vector3(scrollPos, cameraManager.Target.Y, cameraManager.Target.Z);

                if (scrollDist >= Chunk.X_SIZE * Voxel.SIZE && scrollColumn<gameWorld.X_CHUNKS-1)
                {
                    scrollDist = 0f;
                    for (int yy = 0; yy < 11; yy++)
                        if (tileLayer.Tiles[scrollColumn, yy] != null) gameWorld.CopySprite(scrollColumn * Chunk.X_SIZE, yy * Chunk.Y_SIZE, 0, tilesSprite.AnimChunks[tileLayer.Tiles[scrollColumn, yy].Index - 1]);
                    scrollColumn++;
                }
            }
            else if(scrollSpeed>0f) scrollSpeed -= 0.01f;

            gameHero.Move(inputManager.GetAxis2D(VoxelAction.MoveLeft, VoxelAction.MoveRight, VoxelAction.MoveDown, VoxelAction.MoveUp));

            if (inputManager.IsHeld(VoxelAction.Fire)) gameHero.Fire();

            inputManager.EndInputProcessing();

            physicsManager.Step((float)gameTime.ElapsedGameTime.TotalSeconds);

            // Dispatch entity-entity collision events (queued by NarrowPhaseCallbacks on Bepu threads).
            CollisionEventHandler.Instance.ProcessPending(evt =>
            {
                var a = EntityRegistry.Instance.FindByHandle(evt.A);
                var b = EntityRegistry.Instance.FindByHandle(evt.B);
                a?.OnCollision(b);
                b?.OnCollision(a);
            });

            // hitAlpha is set to 1.0 inside DoHit (called above via OnCollision).
            // Check here, before Hero.Update() decrements it, for a reliable single-frame trigger.
            if (gameHero.hitAlpha >= 0.95f)
                cameraManager.TriggerShake(5f);

            cameraManager.Update(gameTime, gameWorld);
            gameWorld.Update(gameTime, cameraManager);

            gameHero.Update(gameTime, cameraManager, gameWorld, scrollSpeed);

            // Trigger the ship-explosion phase the first frame health hits zero.
            if (gameHero.Health <= 0f && youDiedTimer == 0f && deathExplosionTimer == 0f)
            {
                gameHero.Dead = true;
                // Scatter several bursts across the ship's extent for a dramatic effect.
                for (int i = 0; i < 5; i++)
                    particleController.SpawnExplosion(gameHero.Position +
                        new Vector3(Helper.RandomFloat(-5f, 5f), Helper.RandomFloat(-4f, 4f), 0f));
                deathExplosionTimer = 600f;
            }

            enemyController.Update(gameTime, cameraManager, gameHero, gameWorld, scrollPos, scrollSpeed);
            projectileController.Update(gameTime, cameraManager, gameHero, gameWorld, scrollPos);

            // Catch laser/projectile hits that set hitAlpha during projectileController.Update()
            // (these fire after cameraManager.Update(), so we trigger shake here for next frame).
            if (gameHero.hitAlpha >= 0.95f)
                cameraManager.TriggerShake(5f);
            GamePad.SetVibration(PlayerIndex.One, gameHero.hitAlpha, gameHero.hitAlpha);
            particleController.Update(gameTime, cameraManager, gameWorld);
            powerupController.Update(gameTime, cameraManager, gameWorld, gameHero, scrollPos);
            gameStarfield.Update(gameTime, cameraManager, gameWorld, scrollSpeed);

            drawEffect.View = cameraManager.ViewMatrix;
            drawEffect.World = cameraManager.WorldMatrix;

            double elapsed = gameTime.ElapsedGameTime.TotalSeconds;
            if (elapsed > 0) fps = MathHelper.Lerp((float)fps, (float)(1.0 / elapsed), 0.05f);

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;

            gameStarfield.Draw();

            foreach (EffectPass pass in drawEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                for (int y = 0; y < gameWorld.Y_CHUNKS; y++)
                {
                    for (int x = 0; x < gameWorld.X_CHUNKS; x++)
                    {
                        Chunk c = gameWorld.Chunks[x, y, 0];
                        if (c == null) continue;
                        if (!c.Visible) continue;

                        if (c == null || c.VertexArray == null || c.QuadCount == 0) continue;
                        if (!cameraManager.BoundingFrustum.Intersects(c.boundingSphere)) continue;
                        GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, c.VertexArray, 0, c.QuadCount * 4, c.IndexArray, 0, c.QuadCount * 2);
                    }
                }
            }

            if (!gameHero.Dead) gameHero.Draw(GraphicsDevice);

            enemyController.Draw(cameraManager);
            projectileController.Draw(cameraManager);
            particleController.Draw();
            powerupController.Draw();

            Vector2 offset = new Vector2(0, GraphicsDevice.Viewport.Height - 720) / 2;

            spriteBatch.Begin();
            spriteBatch.Draw(hudTex, new Vector2(16, 16) + offset, new Rectangle(0, 0, 16, 688), Color.White * 0.2f);
            spriteBatch.Draw(hudTex, new Vector2(40, 16) + offset, new Rectangle(32, 0, 16, 688), Color.White * 0.2f);
            float clampedHealth = MathHelper.Clamp(gameHero.Health, 0f, 100f);
            spriteBatch.Draw(hudTex, new Vector2(16, 16 + (int)((688f / 100f) * (100f - clampedHealth))) + offset, new Rectangle(0, 0, 16, (int)((688f / 100f) * clampedHealth)), Color.White);
            spriteBatch.Draw(hudTex, new Vector2(40, 16 + (int)((688f / 100f) * (100f - gameHero.XP))) + offset, new Rectangle(32, 0, 16, (int)((688f / 100f) * (gameHero.XP))), Color.White);
            for (int i = 0; i < 5; i++)
            {
                spriteBatch.Draw(hudTex, new Vector2(40, 16 + (int)((688f / 100f) * (100f - gameHero.xpLevels[i]))) + offset, new Rectangle(64, 0, 16, 1), Color.White);

            }
            //spriteBatch.DrawString(font, gameHero.XP.ToString("0.00"), Vector2.One * 5, Color.White);

            // Camera indicator, bottom-left (viewport-anchored, no offset)
            string camLabel = $"{cameraNames[activeCameraIndex]}  [{activeCameraIndex}]";
            spriteBatch.DrawString(font, camLabel,
                new Vector2(70f, GraphicsDevice.Viewport.Height - 70f),
                Color.White * 0.85f);

            // Controls strip, bottom-right, small and subtle
            string controls = "WASD/LTS  Move   SPC/RT  Fire   1-4  Camera   LB/RB  Cycle   Esc  Quit";
            Vector2 ctrlSize = font.MeasureString(controls);
            spriteBatch.DrawString(font, controls,
                new Vector2(GraphicsDevice.Viewport.Width - ctrlSize.X * 0.6f - 16f,
                            GraphicsDevice.Viewport.Height - 60f),
                Color.White * 0.65f,
                0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

            // FPS counter, top-right
            spriteBatch.DrawString(font, $"{fps:0} fps",
                new Vector2(GraphicsDevice.Viewport.Width - 110f, 8f),
                Color.White * 0.5f);

            // ── "You Died!" overlay ──────────────────────────────────────────────────
            // Fades in over 0-800ms, holds 800-2400ms, fades out 2400-3200ms.
            if (youDiedTimer > 0f)
            {
                float diedElapsed = 3200f - youDiedTimer;
                float diedAlpha   = diedElapsed < 800f  ? diedElapsed / 800f          :
                                    diedElapsed < 2400f ? 1f                           :
                                                          (3200f - diedElapsed) / 800f;
                const string diedText  = "You Died!";
                const float  diedScale = 4f;
                Vector2 diedSize = font.MeasureString(diedText) * diedScale;
                Vector2 diedPos  = new Vector2(
                    (GraphicsDevice.Viewport.Width  - diedSize.X) / 2f,
                    (GraphicsDevice.Viewport.Height - diedSize.Y) / 2f);
                // Drop shadow
                spriteBatch.DrawString(font, diedText, diedPos + new Vector2(4f, 4f),
                    new Color(0f, 0f, 0f, diedAlpha * 0.75f),
                    0f, Vector2.Zero, diedScale, SpriteEffects.None, 0f);
                // Foreground — deep red
                spriteBatch.DrawString(font, diedText, diedPos,
                    new Color(0.85f, 0.05f, 0.05f, diedAlpha),
                    0f, Vector2.Zero, diedScale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
