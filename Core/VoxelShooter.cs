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

        int      _activeCameraIndex = 1;   // 1=side, 2=iso, 3=fp, 4=topdown
        ICamera[] _cameras;                // populated in LoadContent (index 0 unused)

        static readonly string[] _cameraNames = { "", "Side-Scrolling", "Isometric", "First Person", "Top-Down" };
        double _fps;

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

        InputManager inputManager = new InputManager();

        SpriteFont font;

        // [MUS-BGM] Microsoft.Xna.Framework.Media.Song _bgm;

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

            // [MUS-BGM] _bgm = Content.Load<Microsoft.Xna.Framework.Media.Song>("Music/bgm");
            // [MUS-BGM] Microsoft.Xna.Framework.Media.MediaPlayer.IsRepeating = true;
            // [MUS-BGM] Microsoft.Xna.Framework.Media.MediaPlayer.Volume = 0.5f;
            // [MUS-BGM] Microsoft.Xna.Framework.Media.MediaPlayer.Play(_bgm);

            tilesSprite = new VoxelSprite(16, 16, 16);
            BvxLoader.LoadSprite(Path.Combine(Content.RootDirectory, "tiles.bvx"), ref tilesSprite);

            gameMap = Content.Load<Map>("1");
            tileLayer = (TileLayer)gameMap.GetLayer("tiles");
            MapObjectLayer spawnLayer = (MapObjectLayer)gameMap.GetLayer("spawns");

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

            _cameras = new ICamera[] { null, sideCamera, isoCamera, fpCamera, tdCamera };
            // index 0 unused so _activeCameraIndex maps 1-4 directly

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

            if (inputManager.IsExiting())
                this.Exit();

            if (!IsActive) return;

            // Camera selection: keys 1-4, RB=next, LB=prev
            {
                int requested = 0;
                for (int i = 1; i <= 4; i++)
                    if (inputManager.IsCameraSelectPressed(i)) { requested = i; break; }

                if (requested == 0 && inputManager.IsCameraNextPressed())
                    requested = (_activeCameraIndex % 4) + 1;   // 1→2→3→4→1

                if (requested == 0 && inputManager.IsCameraPrevPressed())
                    requested = ((_activeCameraIndex - 2 + 4) % 4) + 1;  // 1→4→3→2→1

                if (requested != 0 && requested != _activeCameraIndex)
                {
                    _activeCameraIndex = requested;
                    cameraManager.TransitionTo(_cameras[_activeCameraIndex], 1.5f);
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

            gameHero.Move(inputManager.MoveDirection());

            if (inputManager.IsFiring()) gameHero.Fire();

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
            if (elapsed > 0) _fps = MathHelper.Lerp((float)_fps, (float)(1.0 / elapsed), 0.05f);

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

                        if (c == null || c.VertexArray==null || c.VertexArray.Length == 0) continue;
                        if (!cameraManager.BoundingFrustum.Intersects(c.boundingSphere)) continue;
                        GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, c.VertexArray, 0, c.VertexArray.Length, c.IndexArray, 0, c.VertexArray.Length / 2);
                    }
                }
            }

            gameHero.Draw(GraphicsDevice);

            enemyController.Draw(cameraManager);
            projectileController.Draw(cameraManager);
            particleController.Draw();
            powerupController.Draw();

            Vector2 offset = new Vector2(0, GraphicsDevice.Viewport.Height - 720) / 2;

            spriteBatch.Begin();
            spriteBatch.Draw(hudTex, new Vector2(16, 16) + offset, new Rectangle(0, 0, 16, 688), Color.White * 0.2f);
            spriteBatch.Draw(hudTex, new Vector2(40, 16) + offset, new Rectangle(32, 0, 16, 688), Color.White * 0.2f);
            spriteBatch.Draw(hudTex, new Vector2(16, 16 + (int)((688f / 100f) * (100f - gameHero.Health))) + offset, new Rectangle(0, 0, 16, (int)((688f / 100f) * (gameHero.Health))), Color.White);
            spriteBatch.Draw(hudTex, new Vector2(40, 16 + (int)((688f / 100f) * (100f - gameHero.XP))) + offset, new Rectangle(32, 0, 16, (int)((688f / 100f) * (gameHero.XP))), Color.White);
            for (int i = 0; i < 5; i++)
            {
                spriteBatch.Draw(hudTex, new Vector2(40, 16 + (int)((688f / 100f) * (100f - gameHero.xpLevels[i]))) + offset, new Rectangle(64, 0, 16, 1), Color.White);

            }
            //spriteBatch.DrawString(font, gameHero.XP.ToString("0.00"), Vector2.One * 5, Color.White);

            // Camera indicator — bottom-left (viewport-anchored, no offset)
            string camLabel = $"{_cameraNames[_activeCameraIndex]}  [{_activeCameraIndex}]";
            spriteBatch.DrawString(font, camLabel,
                new Vector2(70f, GraphicsDevice.Viewport.Height - 70f),
                Color.White * 0.85f);

            // Controls strip — bottom-right, small and subtle
            string controls = "WASD Move   Z/RT Fire   1-4 Camera   LB/RB Cycle   Esc Quit";
            Vector2 ctrlSize = font.MeasureString(controls);
            spriteBatch.DrawString(font, controls,
                new Vector2(GraphicsDevice.Viewport.Width - ctrlSize.X * 0.6f - 16f,
                            GraphicsDevice.Viewport.Height - 60f),
                Color.White * 0.65f,
                0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

            // FPS counter — top-right
            spriteBatch.DrawString(font, $"{_fps:0} fps",
                new Vector2(GraphicsDevice.Viewport.Width - 110f, 8f),
                Color.White * 0.5f);

            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
