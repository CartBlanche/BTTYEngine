using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TiledLib;

using BTTYEngine;

namespace VoxelShooter
{
	public enum EnemyType
	{
		Asteroid,
        Omega,
        Turret,
        Squid
	}

	public class EnemyController
	{
		public static EnemyController Instance;

		public List<Enemy> Enemies = new List<Enemy>();

        List<MapObject> Spawns = new List<MapObject>();

        List<Wave> Waves = new List<Wave>();

		Dictionary<string, VoxelSprite> spriteSheets = new Dictionary<string,VoxelSprite>();

		GraphicsDevice graphicsDevice;
		VoxelEffect _voxelEffect;

		public EnemyController(GraphicsDevice gd, VoxelEffect voxelEffect)
		{
			Instance = this;

			graphicsDevice = gd;
			_voxelEffect = voxelEffect;
		}

		public void LoadContent(ContentManager content, MapObjectLayer spawnLayer)
		{
			VoxelSprite asteroid = new VoxelSprite(16,16,16);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "enemies", "asteroids.bvx"), ref asteroid);
			spriteSheets.Add("Asteroid", asteroid);
            VoxelSprite omega = new VoxelSprite(15,15,15);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "enemies", "omega.bvx"), ref omega);
            spriteSheets.Add("Omega", omega);
            VoxelSprite turret = new VoxelSprite(15, 15, 15);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "enemies", "turret.bvx"), ref turret);
            spriteSheets.Add("Turret", turret);
            VoxelSprite squid = new VoxelSprite(15, 15, 15);
            BvxLoader.LoadSprite(Path.Combine(content.RootDirectory, "enemies", "squid.bvx"), ref squid);
            spriteSheets.Add("Squid", squid);

            // [SFX-EXPLODE] Enemy.SfxExplosion = content.Load<Microsoft.Xna.Framework.Audio.SoundEffect>("Sound/explosion");

            foreach (MapObject o in spawnLayer.Objects) Spawns.Add(o);
		}

		public Enemy Spawn(EnemyType type, Vector3 pos, PropertyCollection props)
		{
            Enemy e = null;
			switch (type)
			{
				case EnemyType.Asteroid:
                    e = new Asteroid(pos, spriteSheets["Asteroid"]);
				    break;
                case EnemyType.Omega:
                    e = new Omega(pos, spriteSheets["Omega"]);
                    break;
                case EnemyType.Turret:
                    e = new Turret(pos, spriteSheets["Turret"], props.Contains("Inverted"));
                    break;
                case EnemyType.Squid:
                    e = new Squid(pos, spriteSheets["Squid"]);
                    break;
			}

            // Turrets are stationary; skip Bepu for now (handled in a future pass).
            if (type != EnemyType.Turret && PhysicsManager.Instance != null)
                e.InitPhysics(PhysicsManager.Instance);

            Enemies.Add(e);
            return e;
		}

		
		public void Update(GameTime gameTime, ICamera gameCamera, Hero gameHero, VoxelWorld gameWorld, float scrollPos, float scrollSpeed)
		{
            for(int i=Spawns.Count-1;i>=0;i--)
            {
                if (gameWorld.ToScreenSpace(Spawns[i].Location.Center.X, Spawns[i].Location.Center.Y, 5).X < (int)scrollPos + 75)
                {
                    if (Spawns[i].Properties.Contains("IsWave"))
                    {
                        int spawnY = gameWorld.Y_SIZE - 1 - Spawns[i].Location.Center.Y;
                        Wave w;
                        switch (Spawns[i].Properties["IsWave"])
                        {
                            case "Line":
                                w = new Wave(gameWorld.ToScreenSpace(Spawns[i].Location.Center.X, spawnY, 10), WaveType.Line, (EnemyType)Enum.Parse(typeof(EnemyType), Spawns[i].Name), Convert.ToInt16(Spawns[i].Properties["Count"]), Spawns[i].Properties);

                                break;
                            default:
                                w = new Wave(gameWorld.ToScreenSpace(Spawns[i].Location.Center.X, spawnY, 10), WaveType.Circle, (EnemyType)Enum.Parse(typeof(EnemyType), Spawns[i].Name), Convert.ToInt16(Spawns[i].Properties["Count"]), Spawns[i].Properties);

                                break;
                        }
                        Waves.Add(w);
                    }
                    else
                    {
                        int spawnY = gameWorld.Y_SIZE - 1 - Spawns[i].Location.Center.Y;
                        Spawn((EnemyType)Enum.Parse(typeof(EnemyType), Spawns[i].Name), gameWorld.ToScreenSpace(Spawns[i].Location.Center.X, spawnY, 10), Spawns[i].Properties);
                    }
                    Spawns.RemoveAt(i);
                }
            }

			for(int i=Enemies.Count-1;i>=0;i--) Enemies[i].Update(gameTime, gameWorld, gameHero);

            // Destroy physics bodies for enemies scrolled off-screen (Die() handles the health=0 case).
            if (PhysicsManager.Instance != null)
                foreach (var en in Enemies)
                    if (en.Active && en.Position.X < scrollPos - 110f)
                        en.DestroyPhysics(PhysicsManager.Instance);

	        {
            int wIdx = 0;
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy en = Enemies[i];
                if (en.Active && en.Position.X >= scrollPos - 110f)
                    Enemies[wIdx++] = en;
            }
            while (Enemies.Count > wIdx) Enemies.RemoveAt(Enemies.Count - 1);
        }

            foreach (Wave w in Waves) w.Update(gameTime, scrollSpeed);

            // Wave.Update() assigns e.Position directly; sync those positions back to Bepu
            // bodies so that Enemy.Update() reads the correct formation position next frame.
            if (PhysicsManager.Instance != null)
                foreach (var en in Enemies)
                    en.SyncPhysicsToPosition();

		}

		public void Draw(ICamera gameCamera)
		{

			foreach (Enemy e in Enemies)
			{
				var eWorld = gameCamera.WorldMatrix *
					Matrix.CreateRotationX(e.Rotation.X) *
					Matrix.CreateRotationY(e.Rotation.Y) *
					Matrix.CreateRotationZ(e.Rotation.Z) *
					Matrix.CreateScale(e.Scale) *
					Matrix.CreateTranslation(e.Position);
				_voxelEffect.SetTint(new Vector3(1f, 1f - e.hitAlpha, 1f - e.hitAlpha));
				_voxelEffect.Apply(eWorld, gameCamera.ViewMatrix, gameCamera.ProjectionMatrix);
				var eChunk = e.spriteSheet.AnimChunks[e.CurrentFrame + e.offsetFrame];
				graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, eChunk.VertexArray, 0, eChunk.VertexArray.Length, eChunk.IndexArray, 0, eChunk.VertexArray.Length / 2);
			}

            for (int i = 0; i < Enemies.Count; i++)
            {
                if (!(Enemies[i] is Turret)) continue;
                Enemy e = Enemies[i];
                Turret t = (Turret)e;
                var tWorld = gameCamera.WorldMatrix *
                    Matrix.CreateRotationX(e.Rotation.X + (t.Inverted ? MathHelper.Pi : 0f)) *
                    Matrix.CreateRotationZ(e.Rotation.Z + (t.barrelRot + MathHelper.PiOver2)) *
                    Matrix.CreateRotationY(e.Rotation.Y) *
                    Matrix.CreateScale(e.Scale) *
                    Matrix.CreateTranslation(e.Position);
                _voxelEffect.SetTint(new Vector3(1f, 1f - e.hitAlpha, 1f - e.hitAlpha));
                _voxelEffect.Apply(tWorld, gameCamera.ViewMatrix, gameCamera.ProjectionMatrix);
                var tChunk = e.spriteSheet.AnimChunks[1];
                graphicsDevice.DrawUserIndexedPrimitives<VertexPositionNormalColor>(PrimitiveType.TriangleList, tChunk.VertexArray, 0, tChunk.VertexArray.Length, tChunk.IndexArray, 0, tChunk.VertexArray.Length / 2);
            }
		}

        /// <summary>
        /// Clears all active enemies, pending waves, and spawn queue, then repopulates
        /// the spawn queue from <paramref name="spawnLayer"/> so a full restart from the
        /// beginning of the level is possible.
        /// </summary>
        public void Reset(MapObjectLayer spawnLayer)
        {
            if (PhysicsManager.Instance != null)
                foreach (var e in Enemies)
                    e.DestroyPhysics(PhysicsManager.Instance);
            Enemies.Clear();
            Waves.Clear();
            Spawns.Clear();
            foreach (MapObject o in spawnLayer.Objects) Spawns.Add(o);
        }

	}
}